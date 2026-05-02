#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HoyoToon.Editor.API.Users;
using HoyoToon.Editor.Assets;
using HoyoToon.Editor.Prerequisites;
using HoyoToon.Editor.Prerequisites.RenderPipeline;
using HoyoToon.Editor.Resources;
using HoyoToon.Editor.UI.Manager;
using HoyoToon.Editor.Updater;
using HoyoToon.Editor.Utilities.Editor;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Scene.HSR;
using HoyoToon.Runtime.Scene.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SimulatorCameraInputController = HoyoToon.Runtime.Simulator.Camera.CameraInputController;

namespace HoyoToon.Editor.Onboarding
{
    internal static class OnboardingValidation
    {
        private const string TutorialGame = "Honkai Star Rail";
        private const string DownloadRoot = "Assets/HoyoToon/Characters";
        private const string HoyoToonScenePath = "Packages/com.hoyotoon.hoyotoon/HoyoToon.unity";
        private const string HoyoToonSceneName = "HoyoToon";
        private const string RenderOpenAfterCaptureKey = "HoyoToon.Editor.ScreenshotTool.OpenAfter";
        private const string RenderScaleKey = "HoyoToon.Editor.ScreenshotTool.Scale";
        private const float TutorialOutlineScale = 0.0149f;
        private const float OutlineScaleRestoreTolerance = 0.00005f;
        private const float OutlineScaleChangedAwayTolerance = 0.0005f;
        private const double OutlineScaleRestoreStableSeconds = 0.35d;
        private static readonly Dictionary<string, string> ResourceSyncTargetLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static PrerequisiteReport prerequisiteReport;
        private static bool resourceSyncRequested;
        private static bool resourceSyncCompleted;
        private static bool prerequisiteCheckAfterResourceSyncStarted;
        private static string resourceSyncFailureMessage = string.Empty;
        private static Task resourceSyncTask;
        private static string[] resourceSyncTargetKeys = Array.Empty<string>();
        private static string[] resourceSyncConfigurationErrors = Array.Empty<string>();
        private static bool updaterCheckRequested;
        private static bool userProfilePromptOpen;
        private static bool userProfileRestoreRequested;
        private static bool completedOnboardingUserProfilePromptRequested;
        private static string userProfileCreationError = string.Empty;
        private static Task userProfileRestoreTask;
        private static Task userProfileCreationTask;
        private static bool sceneOpenRequested;
        private static string sceneOpenError = string.Empty;
        private static bool outlineScaleTrackingReady;
        private static bool outlineScaleChangedAway;
        private static bool outlineScaleHasObservedValue;
        private static float outlineScaleBaseline = TutorialOutlineScale;
        private static float outlineScaleLastObserved = TutorialOutlineScale;
        private static double outlineScaleLastChangeTime;
        private static bool simulatorLookDetected;
        private static bool simulatorZoomDetected;
        private static bool simulatorAutoRotateDetected;
        private static bool simulatorSwitchCharacterDetected;
        private static bool simulatorHasMousePosition;
        private static Vector2 simulatorLastMousePosition;
        private static bool simulatorHasCameraBaseline;
        private static float simulatorBaselineCameraOrbitX;
        private static float simulatorBaselineZoomRadius;
        private static bool simulatorBaselineAutoRotateEnabled;
        private static bool simulatorHasActiveModelIndex;
        private static int simulatorBaselineActiveModelIndex = -1;
        private static bool simulatorHasSwitchVersion;
        private static int simulatorBaselineSwitchVersion;
        private static readonly Dictionary<string, double> LastModelImportRetryByCharacter =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private struct SimulatorInputSnapshot
        {
            public bool LeftMousePressed;
            public bool LeftMouseHeld;
            public Vector2 MousePosition;
            public Vector2 MouseDelta;
            public float Scroll;
            public bool AutoRotatePressed;
            public bool SwitchCharacterPressed;
        }

        private struct SimulatorCameraState
        {
            public float OrbitX;
            public float ZoomRadius;
            public bool AutoRotateEnabled;
        }

        public static string PackageVersionLabel
        {
            get
            {
                try
                {
                    UnityEditor.PackageManager.PackageInfo packageInfo =
                        UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HoyoToonManagerWindow).Assembly);
                    if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.version))
                    {
                        return packageInfo.version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                            ? packageInfo.version
                            : "v" + packageInfo.version;
                    }
                }
                catch
                {
                }

                return "version unavailable";
            }
        }

        public static bool HasLocalUserProfile()
        {
            return HoyoToonUserProfileService.HasCompleteLocalProfile;
        }

        public static void PromptForMissingLocalUserProfileAfterCompletedOnboarding(bool force = false)
        {
            if (!OnboardingPersistence.IsCompleted
                || (!force && completedOnboardingUserProfilePromptRequested)
                || HasLocalUserProfile()
                || userProfilePromptOpen
                || userProfileRestoreRequested
                || HoyoToonUserProfileService.IsCreating)
            {
                return;
            }

            completedOnboardingUserProfilePromptRequested = true;
            PromptForLocalUserProfileForOnboarding();
        }

        public static void PromptForLocalUserProfileForOnboarding()
        {
            if (HasLocalUserProfile()
                || userProfilePromptOpen
                || userProfileRestoreRequested
                || HoyoToonUserProfileService.IsCreating)
            {
                return;
            }

            userProfileCreationError = string.Empty;
            if (HoyoToonUserProfileGlobalStore.TryLoad(out _))
            {
                userProfileRestoreRequested = true;
                userProfileRestoreTask = RestoreLocalUserProfileForOnboardingAsync();
                OnboardingManager.RefreshDialog();
                return;
            }

            userProfilePromptOpen = true;
            HoyoToonUserProfilePrompt.Show(
                BeginLocalUserProfileCreationForOnboarding,
                () =>
                {
                    userProfilePromptOpen = false;
                    OnboardingManager.RefreshDialog();
                    RefreshOpenManagerForOnboarding();
                });
        }

        public static OnboardingAsyncStatus GetLocalUserProfileStatus()
        {
            if (HasLocalUserProfile())
            {
                var profile = HoyoToonUserProfileService.LocalProfile;
                string username = profile != null && !string.IsNullOrWhiteSpace(profile.Username)
                    ? profile.Username
                    : "your HoyoToon user";
                return OnboardingAsyncStatus.Succeeded("HoyoToon profile is ready for " + username + ".");
            }

            if (userProfileRestoreRequested
                || (userProfileRestoreTask != null && !userProfileRestoreTask.IsCompleted))
            {
                return OnboardingAsyncStatus.Running("Restoring your HoyoToon profile from the API...");
            }

            if (HoyoToonUserProfileService.IsCreating
                || (userProfileCreationTask != null && !userProfileCreationTask.IsCompleted))
            {
                return OnboardingAsyncStatus.Running("Creating your HoyoToon profile and reserving a unique UID...");
            }

            if (!string.IsNullOrWhiteSpace(userProfileCreationError))
            {
                return OnboardingAsyncStatus.Failed(userProfileCreationError);
            }

            return userProfilePromptOpen
                ? OnboardingAsyncStatus.Idle("Enter your username in the profile dialog.")
                : OnboardingAsyncStatus.Idle("Create a HoyoToon profile before continuing.");
        }

        private static void BeginLocalUserProfileCreationForOnboarding(string username)
        {
            userProfileCreationError = string.Empty;
            userProfileCreationTask = CreateLocalUserProfileForOnboardingAsync(username);
            OnboardingManager.RefreshDialog();
        }

        private static async Task RestoreLocalUserProfileForOnboardingAsync()
        {
            try
            {
                await HoyoToonUserProfileService.RestoreLocalUserProfileAsync(System.Threading.CancellationToken.None);
            }
            catch (Exception exception)
            {
                userProfileCreationError = exception.Message;
            }
            finally
            {
                userProfileRestoreRequested = false;
                EditorApplication.delayCall += () =>
                {
                    if (!HasLocalUserProfile() && string.IsNullOrWhiteSpace(userProfileCreationError))
                    {
                        PromptForLocalUserProfileForOnboarding();
                    }

                    RefreshOpenManagerForOnboarding();
                    OnboardingManager.RefreshDialog();
                };
            }
        }

        private static async Task CreateLocalUserProfileForOnboardingAsync(string username)
        {
            try
            {
                await HoyoToonUserProfileService.CreateLocalUserProfileAsync(username, System.Threading.CancellationToken.None);
            }
            catch (Exception exception)
            {
                userProfileCreationError = exception.Message;
            }
            finally
            {
                EditorApplication.delayCall += () =>
                {
                    RefreshOpenManagerForOnboarding();
                    OnboardingManager.RefreshDialog();
                };
            }
        }

        public static void RunPrerequisiteCheck()
        {
            prerequisiteReport = PrerequisiteService.Evaluate(PrerequisiteFixPolicy.SafeOnly);
            if (RenderPipelineStartupPrompt.PromptForSelectionIfNeeded(prerequisiteReport))
            {
                prerequisiteReport = PrerequisiteService.Evaluate(PrerequisiteFixPolicy.SafeOnly);
            }
        }

        public static void BeginResourceAndPrerequisiteCheck()
        {
            if (resourceSyncRequested && !IsResourceSyncFinished())
            {
                return;
            }

            ResetResourceAndPrerequisiteCheck();
            resourceSyncRequested = true;

            if (ResourceSyncService.IsBusy())
            {
                resourceSyncCompleted = true;
                resourceSyncFailureMessage = "A HoyoToon resource sync is already running. Wait for it to finish, then retry this onboarding step.";
                return;
            }

            try
            {
                ResourceRegistry.Initialize();
                if (!ResourceSyncTargetResolver.TryResolveAllTargets(out var targets, out var messages))
                {
                    resourceSyncCompleted = true;
                    resourceSyncFailureMessage = BuildResourceResolutionFailureMessage(messages);
                    return;
                }

                resourceSyncTargetKeys = targets
                    .Where(target => target != null && !string.IsNullOrWhiteSpace(target.GameKey))
                    .Select(target => target.GameKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                ResourceSyncTargetLabels.Clear();
                foreach (ResourceSyncTarget target in targets.Where(target => target != null && !string.IsNullOrWhiteSpace(target.GameKey)))
                {
                    ResourceSyncTargetLabels[target.GameKey] = target.OperationLabel;
                }

                resourceSyncConfigurationErrors = (messages ?? new List<string>())
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                resourceSyncTask = RunResourceSyncForOnboardingAsync();
            }
            catch (Exception exception)
            {
                resourceSyncCompleted = true;
                resourceSyncFailureMessage = string.IsNullOrWhiteSpace(exception.Message)
                    ? "The HoyoToon resource sync failed before it could start."
                    : exception.Message;
                Debug.LogException(exception);
            }
        }

        public static void RetryResourceAndPrerequisiteCheck()
        {
            ResetResourceAndPrerequisiteCheck();
            BeginResourceAndPrerequisiteCheck();
        }

        public static bool ResourcesAndPrerequisitesPassed()
        {
            if (!ResourceSyncPassed())
            {
                return false;
            }

            EnsurePrerequisiteCheckAfterResourceSync();
            return PrerequisitesPassed();
        }

        public static OnboardingAsyncStatus GetResourceAndPrerequisiteStatus()
        {
            if (!resourceSyncRequested)
            {
                return OnboardingAsyncStatus.Running("Preparing to download required HoyoToon resources...");
            }

            if (!IsResourceSyncFinished())
            {
                return OnboardingAsyncStatus.Running("Downloading and syncing required HoyoToon resources...");
            }

            string resourceFailure = GetResourceSyncFailureMessage();
            if (!string.IsNullOrWhiteSpace(resourceFailure))
            {
                return OnboardingAsyncStatus.Failed(resourceFailure);
            }

            EnsurePrerequisiteCheckAfterResourceSync();
            if (prerequisiteReport == null)
            {
                return OnboardingAsyncStatus.Running("Checking required HoyoToon project settings...");
            }

            return GetPrerequisiteStatus();
        }

        public static void RunUpdaterCheck()
        {
            updaterCheckRequested = true;
            PackageUpdaterStorage.EnsureDefaults();
            if (!string.Equals(PackageUpdaterStorage.CurrentBranch, PackageUpdaterStorage.DefaultBranch, StringComparison.OrdinalIgnoreCase))
            {
                PackageUpdaterStorage.CurrentBranch = PackageUpdaterStorage.DefaultBranch;
                PackageUpdaterService.ResetCachedStatus();
            }

            if (PackageUpdaterService.IsBusy())
            {
                return;
            }

            PackageUpdaterService.CheckForUpdates(showUpToDateDialog: false, automatic: false, cleanMissingFiles: true);
        }

        public static bool UpdaterPassed()
        {
            UpdaterStatusSnapshot snapshot = PackageUpdaterService.GetStatusSnapshot();
            UpdateAvailabilityState state = GetUpdaterState(snapshot);
            return state == UpdateAvailabilityState.UpToDate
                || state == UpdateAvailabilityState.LocalAhead;
        }

        public static OnboardingAsyncStatus GetUpdaterStatus()
        {
            UpdaterStatusSnapshot snapshot = PackageUpdaterService.GetStatusSnapshot();
            UpdateAvailabilityState state = GetUpdaterState(snapshot);
            string branch = string.IsNullOrWhiteSpace(snapshot?.branch) ? PackageUpdaterStorage.CurrentBranch : snapshot.branch;
            string message = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.statusMessage)
                ? snapshot.statusMessage
                : "Checking the HoyoToon package for updates...";

            if (!updaterCheckRequested)
            {
                return OnboardingAsyncStatus.Running("Preparing to check the HoyoToon package for updates...");
            }

            if (PackageUpdaterService.IsBusy() || state == UpdateAvailabilityState.Checking || state == UpdateAvailabilityState.Applying)
            {
                return OnboardingAsyncStatus.Running(message);
            }

            if (state == UpdateAvailabilityState.UpToDate)
            {
                return OnboardingAsyncStatus.Succeeded(message);
            }

            if (state == UpdateAvailabilityState.LocalAhead)
            {
                return OnboardingAsyncStatus.Succeeded(message);
            }

            if (state == UpdateAvailabilityState.UpdateAvailable)
            {
                return OnboardingAsyncStatus.Failed(
                    "An update is available on the '" + branch + "' branch. Choose Update Now in the updater dialog, let it finish, then continue onboarding.");
            }

            if (state == UpdateAvailabilityState.Error)
            {
                return OnboardingAsyncStatus.Failed(
                    string.IsNullOrWhiteSpace(message)
                        ? "The HoyoToon update check failed. Retry this step before continuing."
                        : message);
            }

            return OnboardingAsyncStatus.Running("Starting the HoyoToon update check...");
        }

        private static UpdateAvailabilityState GetUpdaterState(UpdaterStatusSnapshot snapshot)
        {
            return snapshot == null
                ? UpdateAvailabilityState.Unknown
                : (UpdateAvailabilityState)snapshot.state;
        }

        public static bool PrerequisitesPassed()
        {
            return prerequisiteReport != null && prerequisiteReport.BlockingCount <= 0;
        }

        public static OnboardingAsyncStatus GetPrerequisiteStatus()
        {
            if (prerequisiteReport == null)
            {
                return OnboardingAsyncStatus.Running("Checking required HoyoToon project resources...");
            }

            if (prerequisiteReport.BlockingCount > 0)
            {
                if (HasBlockingRenderPipelineIssue(prerequisiteReport))
                {
                    return OnboardingAsyncStatus.Failed(
                        "HoyoToon URP still needs to be set up before onboarding can continue. Click Retry and choose Set Up HoyoToon URP.");
                }

                return OnboardingAsyncStatus.Failed(
                    "HoyoToon still found " + prerequisiteReport.BlockingCount + " blocking prerequisite issue(s). Check the Console for details, then retry this step.");
            }

            if (prerequisiteReport.FailedCount > 0)
            {
                return OnboardingAsyncStatus.Succeeded(
                    "The blocking checks passed. " + prerequisiteReport.FailedCount + " non-blocking warning(s) remain.");
            }

            return OnboardingAsyncStatus.Succeeded("All required resources are ready.");
        }

        private static async Task RunResourceSyncForOnboardingAsync()
        {
            try
            {
                await ResourceSyncService.SyncAllGamesForOnboardingAsync();
            }
            catch (Exception exception)
            {
                resourceSyncFailureMessage = string.IsNullOrWhiteSpace(exception.Message)
                    ? "The HoyoToon resource sync failed."
                    : exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                resourceSyncCompleted = true;
            }
        }

        private static bool ResourceSyncPassed()
        {
            return resourceSyncRequested
                && IsResourceSyncFinished()
                && string.IsNullOrWhiteSpace(GetResourceSyncFailureMessage());
        }

        private static bool IsResourceSyncFinished()
        {
            if (!resourceSyncRequested)
            {
                return false;
            }

            if (resourceSyncCompleted)
            {
                return true;
            }

            if (resourceSyncTask != null && resourceSyncTask.IsCompleted)
            {
                resourceSyncCompleted = true;
                return true;
            }

            return false;
        }

        private static void EnsurePrerequisiteCheckAfterResourceSync()
        {
            if (prerequisiteCheckAfterResourceSyncStarted || !ResourceSyncPassed())
            {
                return;
            }

            prerequisiteCheckAfterResourceSyncStarted = true;
            try
            {
                RunPrerequisiteCheck();
            }
            catch (Exception exception)
            {
                resourceSyncFailureMessage = string.IsNullOrWhiteSpace(exception.Message)
                    ? "The HoyoToon prerequisite check failed."
                    : exception.Message;
                Debug.LogException(exception);
            }
        }

        private static string GetResourceSyncFailureMessage()
        {
            if (!string.IsNullOrWhiteSpace(resourceSyncFailureMessage))
            {
                return resourceSyncFailureMessage;
            }

            if (!resourceSyncRequested || !IsResourceSyncFinished())
            {
                return string.Empty;
            }

            var failures = new List<string>();
            failures.AddRange(resourceSyncConfigurationErrors ?? Array.Empty<string>());

            if (resourceSyncTargetKeys == null || resourceSyncTargetKeys.Length <= 0)
            {
                failures.Add("No HoyoToon resource sets were available for onboarding.");
            }
            else
            {
                foreach (string gameKey in resourceSyncTargetKeys)
                {
                    ResourceSyncStatusSnapshot status = ResourceSyncStorage.ReadStatus(gameKey);
                    string label = GetResourceSyncTargetLabel(gameKey);
                    if (status == null)
                    {
                        failures.Add("No resource sync status was recorded for '" + label + "'.");
                        continue;
                    }

                    var state = (ResourceSyncOutcomeState)status.state;
                    if (state == ResourceSyncOutcomeState.UpToDate || state == ResourceSyncOutcomeState.Succeeded)
                    {
                        continue;
                    }

                    string message = string.IsNullOrWhiteSpace(status.statusMessage)
                        ? "'" + label + "' resources did not finish syncing. Current state: " + state + "."
                        : status.statusMessage;
                    failures.Add(message);
                }
            }

            return string.Join("\n", failures.Where(message => !string.IsNullOrWhiteSpace(message)));
        }

        private static string GetResourceSyncTargetLabel(string gameKey)
        {
            if (!string.IsNullOrWhiteSpace(gameKey)
                && ResourceSyncTargetLabels.TryGetValue(gameKey, out string label)
                && !string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            return string.IsNullOrWhiteSpace(gameKey) ? "Unknown" : gameKey;
        }

        private static string BuildResourceResolutionFailureMessage(IReadOnlyList<string> messages)
        {
            var lines = new List<string>
            {
                "No valid HoyoToon resource definitions were available for onboarding."
            };

            foreach (string message in messages ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    lines.Add("- " + message);
                }
            }

            return string.Join("\n", lines);
        }

        private static void ResetResourceAndPrerequisiteCheck()
        {
            prerequisiteReport = null;
            resourceSyncRequested = false;
            resourceSyncCompleted = false;
            prerequisiteCheckAfterResourceSyncStarted = false;
            resourceSyncFailureMessage = string.Empty;
            resourceSyncTask = null;
            resourceSyncTargetKeys = Array.Empty<string>();
            resourceSyncConfigurationErrors = Array.Empty<string>();
            ResourceSyncTargetLabels.Clear();
        }

        private static bool HasBlockingRenderPipelineIssue(PrerequisiteReport report)
        {
            if (report == null || report.Results == null)
            {
                return false;
            }

            foreach (PrerequisiteCheckResult result in report.Results)
            {
                if (string.Equals(result.Id, "render-pipeline", StringComparison.Ordinal)
                    && !result.FinalEvaluation.Passed
                    && result.FinalEvaluation.IsBlocking)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsHoyoToonSceneOpen()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded)
                {
                    continue;
                }

                if (string.Equals(scene.path, HoyoToonScenePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(scene.name, HoyoToonSceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static void OpenHoyoToonSceneForOnboarding()
        {
            sceneOpenRequested = true;
            sceneOpenError = string.Empty;

            if (IsHoyoToonSceneOpen() || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(HoyoToonScenePath);
            if (sceneAsset == null)
            {
                sceneOpenError = "The HoyoToon scene could not be found at " + HoyoToonScenePath + ".";
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                sceneOpenError = "Unity did not open the HoyoToon scene because the current scene save prompt was cancelled.";
                return;
            }

            try
            {
                EditorSceneManager.OpenScene(HoyoToonScenePath, OpenSceneMode.Single);
            }
            catch (Exception exception)
            {
                sceneOpenError = "Failed to open the HoyoToon scene: " + exception.Message;
            }
        }

        public static OnboardingAsyncStatus GetHoyoToonSceneStatus()
        {
            if (IsHoyoToonSceneOpen())
            {
                return OnboardingAsyncStatus.Succeeded("The HoyoToon scene is open.");
            }

            if (!string.IsNullOrWhiteSpace(sceneOpenError))
            {
                return OnboardingAsyncStatus.Failed(sceneOpenError);
            }

            if (EditorApplication.isCompiling)
            {
                return OnboardingAsyncStatus.Running("Waiting for Unity to finish compiling before opening the HoyoToon scene...");
            }

            if (EditorApplication.isUpdating)
            {
                return OnboardingAsyncStatus.Running("Waiting for Unity to finish importing before opening the HoyoToon scene...");
            }

            if (!sceneOpenRequested)
            {
                return OnboardingAsyncStatus.Idle("Open the HoyoToon scene before continuing.");
            }

            OpenHoyoToonSceneForOnboarding();
            if (IsHoyoToonSceneOpen())
            {
                return OnboardingAsyncStatus.Succeeded("The HoyoToon scene is open.");
            }

            return !string.IsNullOrWhiteSpace(sceneOpenError)
                ? OnboardingAsyncStatus.Failed(sceneOpenError)
                : OnboardingAsyncStatus.Running("Opening the HoyoToon scene...");
        }

        public static bool IsManagerOpen()
        {
            return HoyoToonManagerWindow.TryGetOpenWindow(out _);
        }

        public static void OpenManagerForOnboarding()
        {
            HoyoToonManagerWindow.ShowWindowForOnboarding();
        }

        public static void RefreshOpenManagerForOnboarding()
        {
            HoyoToonManagerWindow window;
            if (HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null)
            {
                window.RefreshManagerContext();
            }
        }

        public static void PrepareManagerNavigationForOnboarding()
        {
            HoyoToonManagerWindow window;
            if (!HoyoToonManagerWindow.TryGetOpenWindow(out window) || window == null)
            {
                window = HoyoToonManagerWindow.ShowWindowForOnboarding();
            }

            window.RefreshManagerContext();
            window.Focus();
        }

        public static void FocusManagerHeaderForOnboarding()
        {
            HoyoToonManagerWindow window;
            if (!HoyoToonManagerWindow.TryGetOpenWindow(out window) || window == null)
            {
                window = HoyoToonManagerWindow.ShowWindowForOnboarding();
            }

            window.RefreshManagerContext();
            window.Focus();
        }

        public static OnboardingAsyncStatus GetManagerOpenStatus()
        {
            return IsManagerOpen()
                ? OnboardingAsyncStatus.Succeeded("The HoyoToon Manager is open.")
                : OnboardingAsyncStatus.Running("Opening the HoyoToon Manager...");
        }

        public static bool IsActiveModule(string moduleId)
        {
            HoyoToonManagerWindow window;
            return HoyoToonManagerWindow.TryGetOpenWindow(out window)
                && window != null
                && string.Equals(window.ActiveModuleId, moduleId, StringComparison.Ordinal);
        }

        public static bool IsTutorialGameSelected()
        {
            AssetDownloadWindow backend = GetBackend();
            return string.Equals(backend.GetSelectedGameDisplayNameForManager(), TutorialGame, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTutorialGameSelectedAfterInteraction()
        {
            return IsTutorialGameSelected()
                && OnboardingSignals.GetActionCountSinceStepStart("Assets.GameDropdown") > 0;
        }

        public static void PrepareAssetCharacterSelection(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            AssetDownloadWindow backend = GetBackend();
            bool targetAlreadySelected = backend.GetSelectedCharacterNamesForManager()
                .Any(name => string.Equals(name, characterName, StringComparison.OrdinalIgnoreCase));
            if (!targetAlreadySelected && backend.GetSelectedCharacterNamesForManager().Count > 0)
            {
                backend.ClearSelectionForManager();
            }

            if (!string.Equals(backend.GetCharacterSearchForManager(), characterName, StringComparison.Ordinal))
            {
                backend.SetCharacterSearchForManager(characterName);
            }
        }

        public static bool IsCharacterSelected(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            return GetBackend().GetSelectedCharacterNamesForManager()
                .Any(name => string.Equals(name, characterName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsCharacterSelectedAfterInteraction(string characterName)
        {
            return IsCharacterSelected(characterName)
                && OnboardingSignals.GetActionCountSinceStepStart("Assets.CharacterDropdown") > 0;
        }

        public static bool IsVariantSelected(string characterName, string variantName)
        {
            if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(variantName))
            {
                return false;
            }

            return GetBackend().IsVariantSelectedForManager(characterName, variantName);
        }

        public static bool IsVariantSelectedAfterInteraction(string characterName, string variantName)
        {
            return IsVariantSelected(characterName, variantName)
                && OnboardingSignals.GetActionCountSinceStepStart("Assets.VariantDropdown") > 0;
        }

        public static bool IsNoAnimationsSelected(string characterName)
        {
            AssetDownloadWindow backend = GetBackend();
            if (!backend.ShouldShowHsrChoiceForManager())
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(characterName))
            {
                return backend.GetHsrChoiceIndexForCharacterForManager(characterName) == 1;
            }

            return backend.GetHsrChoiceIndexForManager() == 1;
        }

        public static bool IsNoAnimationsSelectedAfterInteraction(string characterName)
        {
            return IsNoAnimationsSelected(characterName)
                && OnboardingSignals.GetActionCountSinceStepStart("Assets.ModelTypeDropdown") > 0;
        }

        public static bool IsAutoSetupDisabled()
        {
            return !GetBackend().GetAutoSetupAfterDownloadForManager();
        }

        public static bool IsAutoSetupDisabledAfterInteraction()
        {
            return IsAutoSetupDisabled()
                && OnboardingSignals.GetActionCountSinceStepStart("Assets.AutoSetupToggle") > 0;
        }

        public static void PrepareAutoSetupDisableStep()
        {
            AssetDownloadWindow backend = GetBackend();
            if (!backend.GetAutoSetupAfterDownloadForManager())
            {
                backend.SetAutoSetupAfterDownloadForManager(true);
            }
        }

        public static bool HasDownloadedCharacter(string characterName)
        {
            if (!WasDownloadButtonClickedThisStep())
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            if (OnboardingSignals.HasOperationFailed(OnboardingOperationKind.Download))
            {
                return false;
            }

            AssetDownloadWindow backend = GetBackend();
            if (backend.IsBusyForManager()
                || backend.IsImportingDownloadedAssetsForManager()
                || EditorApplication.isUpdating)
            {
                return false;
            }

            if (backend.WasLastManagerDownloadFailed())
            {
                return false;
            }

            return FindDownloadedModelAsset(characterName) != null;
        }

        public static OnboardingAsyncStatus GetDownloadStatus(string characterName)
        {
            if (!WasDownloadButtonClickedThisStep())
            {
                return OnboardingAsyncStatus.Idle("Click Download Selected Assets to start this step.");
            }

            if (OnboardingSignals.HasOperationFailed(OnboardingOperationKind.Download))
            {
                return OnboardingAsyncStatus.Failed(OnboardingSignals.GetOperationError(OnboardingOperationKind.Download));
            }

            if (HasDownloadedCharacter(characterName))
            {
                return OnboardingAsyncStatus.Succeeded(characterName + " is downloaded.");
            }

            AssetDownloadWindow backend = GetBackend();
            if (backend.IsImportingDownloadedAssetsForManager())
            {
                return OnboardingAsyncStatus.Running("Importing downloaded assets into Unity...");
            }

            string status = backend.GetStatusMessageForManager();
            if (backend.IsBusyForManager())
            {
                return OnboardingAsyncStatus.Running(
                    string.IsNullOrWhiteSpace(status) ? "Downloading selected assets..." : status);
            }

            if (backend.WasLastManagerDownloadFailed())
            {
                return OnboardingAsyncStatus.Failed(
                    string.IsNullOrWhiteSpace(status) ? "Download failed." : status);
            }

            if (backend.WasManagerDownloadSuccessfulFor(characterName))
            {
                if (EditorApplication.isUpdating)
                {
                    return OnboardingAsyncStatus.Running("Unity is still importing " + characterName + "...");
                }

                return OnboardingAsyncStatus.Running("Download finished. Waiting for Unity to register " + characterName + " as an imported model...");
            }

            if (!string.IsNullOrWhiteSpace(status)
                && status.StartsWith("Download failed", StringComparison.OrdinalIgnoreCase))
            {
                return OnboardingAsyncStatus.Failed(status);
            }

            return OnboardingAsyncStatus.Running(
                string.IsNullOrWhiteSpace(status) ? "Waiting for the selected model to download and import..." : status);
        }

        public static void BeginDownloadStep()
        {
            OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.Download);
        }

        public static bool IsModelQueued(string characterName)
        {
            HoyoToonManagerWindow window;
            return HoyoToonManagerWindow.TryGetOpenWindow(out window)
                && window != null
                && window.HasQueuedModelNamed(characterName);
        }

        public static bool HasSetupCharacter(string characterName)
        {
            HoyoToonManagerWindow window;
            return HoyoToonManagerWindow.TryGetOpenWindow(out window)
                && window != null
                && window.HasPlacementCharacterNamed(characterName);
        }

        public static OnboardingAsyncStatus GetSetupStatus(string characterName)
        {
            if (OnboardingSignals.HasOperationFailed(OnboardingOperationKind.Setup))
            {
                return OnboardingAsyncStatus.Failed(OnboardingSignals.GetOperationError(OnboardingOperationKind.Setup));
            }

            return HasSetupCharacter(characterName)
                ? OnboardingAsyncStatus.Succeeded(characterName + " is ready in the scene.")
                : OnboardingAsyncStatus.Idle("Click Auto Setup and wait for the model to be prepared.");
        }

        public static bool HasSetupTutorialModels()
        {
            return HasSetupCharacter("Acheron")
                && HasSetupCharacter("Cyrene")
                && OnboardingSignals.GetActionCountSinceStepStart("Setup.AutoSetupButton") > 0;
        }

        public static OnboardingAsyncStatus GetTutorialModelsSetupStatus()
        {
            if (OnboardingSignals.HasOperationFailed(OnboardingOperationKind.Setup))
            {
                return OnboardingAsyncStatus.Failed(OnboardingSignals.GetOperationError(OnboardingOperationKind.Setup));
            }

            if (HasSetupTutorialModels())
            {
                return OnboardingAsyncStatus.Succeeded("Acheron and Cyrene are ready in the scene.");
            }

            if (OnboardingSignals.GetActionCountSinceStepStart("Setup.AutoSetupButton") > 0)
            {
                return OnboardingAsyncStatus.Running("Auto Setup is preparing the tutorial models...");
            }

            return OnboardingAsyncStatus.Idle("Click Auto Setup and wait for both tutorial models to be prepared.");
        }

        public static void PrepareSetupModelSelection(string characterName)
        {
            PrepareSetupModelSelection(characterName, true);
        }

        public static void PrepareSetupModelSelection(string characterName, bool replaceExistingQueue)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            GameObject modelAsset = FindDownloadedModelAsset(characterName);
            if (modelAsset == null)
            {
                return;
            }

            HoyoToonManagerWindow window;
            if (HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null)
            {
                if (replaceExistingQueue)
                {
                    window.SetSingleSelectedModelAssetForOnboarding(modelAsset);
                }
                else
                {
                    window.SetSelectedModelAsset(modelAsset);
                }
            }
        }

        public static bool ActiveCharacterChanged()
        {
            return OnboardingSignals.GetActionCountSinceStepStart("Scene.ActiveCharacterDropdown") > 0
                || OnboardingSignals.GetValueChangeCountSinceStepStart("Scene.ActiveCharacterDropdown") > 0;
        }

        public static void EnsureActiveCharacterSelected(string characterName)
        {
            HoyoToonManagerWindow window;
            if (!HoyoToonManagerWindow.TryGetOpenWindow(out window) || window == null)
            {
                return;
            }

            window.TrySelectPlacementCharacterForOnboarding(characterName);
        }

        public static void PrepareActiveCharacterSelection(string characterName)
        {
            HoyoToonManagerWindow window;
            if (!HoyoToonManagerWindow.TryGetOpenWindow(out window) || window == null)
            {
                return;
            }

            if (window.IsActivePlacementCharacterNamed(characterName))
            {
                window.TryClearActivePlacementCharacterForOnboarding(characterName);
            }
        }

        public static bool IsActiveCharacterSelected(string characterName)
        {
            HoyoToonManagerWindow window;
            return HoyoToonManagerWindow.TryGetOpenWindow(out window)
                && window != null
                && window.IsActivePlacementCharacterNamed(characterName);
        }

        public static bool IsActiveCharacterSelectedAfterInteraction(string characterName)
        {
            return IsActiveCharacterSelected(characterName)
                && ActiveCharacterChanged();
        }

        public static OnboardingAsyncStatus GetActiveCharacterStatus(string characterName)
        {
            if (IsActiveCharacterSelected(characterName))
            {
                return ActiveCharacterChanged()
                    ? OnboardingAsyncStatus.Succeeded(characterName + " is the active character.")
                    : OnboardingAsyncStatus.Idle("Select " + characterName + " as the active character.");
            }

            HoyoToonManagerWindow window;
            if (!HoyoToonManagerWindow.TryGetOpenWindow(out window) || window == null)
            {
                return OnboardingAsyncStatus.Running("Opening the HoyoToon Manager...");
            }

            return window.HasPlacementCharacterNamed(characterName)
                ? OnboardingAsyncStatus.Idle("Select " + characterName + " as the active character.")
                : OnboardingAsyncStatus.Failed(characterName + " is not available in the placement controller. Run Auto Setup for that model first.");
        }

        public static void PreparePlacementSingleStep()
        {
            HoyoToonManagerWindow window;
            if (HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null)
            {
                window.TrySetPlacementModeForOnboarding(ManagerPlacementMode.Grid);
            }
        }

        public static bool PlacementModeSingleSelectedAfterInteraction()
        {
            return IsPlacementMode(ManagerPlacementMode.Single)
                && OnboardingSignals.GetActionCountSinceStepStart("Scene.PlacementMode.Single") > 0
                && HasActivePlacementModel();
        }

        public static bool PlacementModeTeamSelectedAfterInteraction()
        {
            return IsPlacementMode(ManagerPlacementMode.Team)
                && OnboardingSignals.GetActionCountSinceStepStart("Scene.PlacementMode.Team") > 0
                && HasActivePlacementModel()
                && AreAllTeamModelsActive();
        }

        public static bool PlacementModeGridSelectedAfterInteraction()
        {
            return IsPlacementMode(ManagerPlacementMode.Grid)
                && OnboardingSignals.GetActionCountSinceStepStart("Scene.PlacementMode.Grid") > 0
                && HasActivePlacementModel();
        }

        private static bool IsPlacementMode(ManagerPlacementMode placementMode)
        {
            CharacterPlacementController controller = GetPlacementController();
            return controller != null && controller.PlacementMode == placementMode;
        }

        private static bool HasActivePlacementModel()
        {
            CharacterPlacementController controller = GetPlacementController();
            return controller != null && controller.ActiveModel != null;
        }

        private static bool AreAllTeamModelsActive()
        {
            CharacterPlacementController controller = GetPlacementController();
            if (controller == null || controller.ManagedModels.Count <= 0)
            {
                return false;
            }

            return controller.ManagedModels.All(model =>
                model != null && controller.TeamActiveModels.Contains(model));
        }

        private static CharacterPlacementController GetPlacementController()
        {
            if (EditorApplication.isPlaying)
            {
                CharacterPlacementController playModeController = CharacterPlacementController.GetPrimaryCachedOrFind();
                if (playModeController != null)
                {
                    return playModeController;
                }
            }

            HoyoToonManagerWindow window;
            return HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null
                ? window.CurrentContext?.PlacementController
                : CharacterPlacementController.GetPrimaryCachedOrFind();
        }

        public static bool SelfShadowToggledOffThenOn()
        {
            return OnboardingSignals.GetActionCountSinceStepStart("Character.SelfShadowToggle.False") > 0
                && OnboardingSignals.GetActionCountSinceStepStart("Character.SelfShadowToggle.True") > 0;
        }

        public static void PrepareSelfShadowToggleStep()
        {
            HSRCharacterController controller = GetActiveCharacterController();
            if (controller != null && !controller.EnableCharacterSelfShadow)
            {
                Undo.RecordObject(controller, "HoyoToon Prepare Self Shadow Tutorial");
                controller.EnableCharacterSelfShadow = true;
                EditorUtility.SetDirty(controller);
            }

            RefreshOpenManagerForOnboarding();
        }

        public static bool CharacterLightingChangedThenRestored()
        {
            return OnboardingSignals.WasValueChangedAndRestoredSinceStepStart(
                "Character.LightIntensityField",
                "Character.LightColorField");
        }

        public static bool GlobalSceneSettingChangedThenRestored()
        {
            HSRSceneController controller = GetSceneController();
            if (controller == null)
            {
                return false;
            }

            if (!outlineScaleTrackingReady)
            {
                CaptureOutlineScaleBaseline(controller._OutlineScale);
            }

            double now = EditorApplication.timeSinceStartup;
            float currentValue = controller._OutlineScale;
            if (!outlineScaleHasObservedValue
                || Mathf.Abs(currentValue - outlineScaleLastObserved) > OutlineScaleRestoreTolerance)
            {
                outlineScaleHasObservedValue = true;
                outlineScaleLastObserved = currentValue;
                outlineScaleLastChangeTime = now;
            }

            if (Mathf.Abs(currentValue - outlineScaleBaseline) > OutlineScaleChangedAwayTolerance)
            {
                outlineScaleChangedAway = true;
            }

            if (!outlineScaleChangedAway)
            {
                return false;
            }

            if (Mathf.Abs(currentValue - outlineScaleBaseline) > OutlineScaleRestoreTolerance)
            {
                return false;
            }

            return now - outlineScaleLastChangeTime >= OutlineScaleRestoreStableSeconds;
        }

        public static void PrepareOutlineScaleStep()
        {
            HSRSceneController controller = GetSceneController();
            ResetOutlineScaleTracking(TutorialOutlineScale);

            if (controller != null)
            {
                if (Mathf.Abs(controller._OutlineScale - TutorialOutlineScale) > OutlineScaleRestoreTolerance)
                {
                    Undo.RecordObject(controller, "HoyoToon Prepare Outline Scale Tutorial");
                    controller._OutlineScale = TutorialOutlineScale;
                    EditorUtility.SetDirty(controller);
                    if (!EditorApplication.isPlaying && controller.gameObject.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                    }
                }

                CaptureOutlineScaleBaseline(controller._OutlineScale);
            }

            RefreshOpenManagerForOnboarding();
        }

        public static bool CharacterControllerReady()
        {
            HoyoToonManagerWindow window;
            GameObject activeModel = HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null
                ? window.CurrentContext?.PlacementActiveModel
                : null;
            return activeModel != null && activeModel.GetComponentInChildren<HSRCharacterController>(true) != null;
        }

        public static OnboardingAsyncStatus GetCharacterControllerStatus()
        {
            if (CharacterControllerReady())
            {
                return OnboardingAsyncStatus.Succeeded("The active character controller is ready.");
            }

            return OnboardingAsyncStatus.Idle("Select an active tutorial character before changing Character tab settings.");
        }

        public static bool SceneControllerReady()
        {
            return GetSceneController() != null;
        }

        public static OnboardingAsyncStatus GetSceneControllerStatus()
        {
            return SceneControllerReady()
                ? OnboardingAsyncStatus.Succeeded("The scene controller is ready.")
                : OnboardingAsyncStatus.Failed("No HSR scene controller was found. Run Auto Setup on the tutorial model before using Scene tab controls.");
        }

        private static HSRSceneController GetSceneController()
        {
            return UnityEngine.Object.FindObjectsByType<HSRSceneController>(FindObjectsSortMode.None)
                .FirstOrDefault(controller => controller != null && controller.gameObject != null && controller.gameObject.scene.IsValid());
        }

        private static void ResetOutlineScaleTracking(float baseline)
        {
            outlineScaleTrackingReady = false;
            outlineScaleChangedAway = false;
            outlineScaleHasObservedValue = false;
            outlineScaleBaseline = baseline;
            outlineScaleLastObserved = baseline;
            outlineScaleLastChangeTime = EditorApplication.timeSinceStartup;
        }

        private static void CaptureOutlineScaleBaseline(float baseline)
        {
            outlineScaleTrackingReady = true;
            outlineScaleChangedAway = false;
            outlineScaleHasObservedValue = true;
            outlineScaleBaseline = baseline;
            outlineScaleLastObserved = baseline;
            outlineScaleLastChangeTime = EditorApplication.timeSinceStartup;
        }

        public static bool ScreenshotRenderCreated()
        {
            return OnboardingSignals.HasOperationSucceeded(OnboardingOperationKind.Render);
        }

        public static OnboardingAsyncStatus GetRenderStatus()
        {
            if (OnboardingSignals.HasOperationFailed(OnboardingOperationKind.Render))
            {
                return OnboardingAsyncStatus.Failed(OnboardingSignals.GetOperationError(OnboardingOperationKind.Render));
            }

            return ScreenshotRenderCreated()
                ? OnboardingAsyncStatus.Succeeded("The screenshot render was created.")
                : OnboardingAsyncStatus.Idle("Click Capture Screenshot and wait for the file to be saved.");
        }

        public static bool TurnaroundEnabled()
        {
            return EditorPrefs.GetBool(PrefsKey("HoyoToon.Editor.ScreenshotTool.TurnaroundEnabled"), false);
        }

        public static bool TurnaroundEnabledAfterInteraction()
        {
            return TurnaroundEnabled()
                && OnboardingSignals.GetActionCountSinceStepStart("Render.TurnaroundToggle") > 0;
        }

        public static void PrepareTurnaroundEnableStep()
        {
            EditorPrefs.SetBool(PrefsKey("HoyoToon.Editor.ScreenshotTool.TurnaroundEnabled"), false);
            HoyoToonManagerWindow window;
            if (HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null)
            {
                window.RefreshManagerContext();
            }
        }

        public static void EnableOpenAfterCaptureForOnboarding()
        {
            EditorPrefs.SetBool(PrefsKey(RenderOpenAfterCaptureKey), true);
            EditorPrefs.SetInt(PrefsKey(RenderScaleKey), 1);
            HoyoToonManagerWindow window;
            if (HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null)
            {
                window.RefreshManagerContext();
            }
        }

        private static string PrefsKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
        }

        public static bool TurnaroundRenderCreated()
        {
            return OnboardingSignals.HasOperationSucceeded(OnboardingOperationKind.TurnaroundRender);
        }

        public static OnboardingAsyncStatus GetTurnaroundStatus()
        {
            if (OnboardingSignals.HasOperationFailed(OnboardingOperationKind.TurnaroundRender))
            {
                return OnboardingAsyncStatus.Failed(OnboardingSignals.GetOperationError(OnboardingOperationKind.TurnaroundRender));
            }

            return TurnaroundRenderCreated()
                ? OnboardingAsyncStatus.Succeeded("The turnaround render was created.")
                : OnboardingAsyncStatus.Idle("Click Capture Turnaround and wait for the file to be saved.");
        }

        public static bool IsInPlayMode()
        {
            return EditorApplication.isPlaying;
        }

        public static OnboardingAsyncStatus GetPlayModeStatus()
        {
            if (EditorApplication.isPlaying)
            {
                return OnboardingAsyncStatus.Succeeded("Play Mode is running.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return OnboardingAsyncStatus.Running("Unity is entering Play Mode...");
            }

            return OnboardingAsyncStatus.Idle("Click Confirm to start the simulator in Play Mode.");
        }

        public static void EnterPlayModeForOnboarding()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.isPlaying = true;
        }

        public static bool IsGameViewFocused()
        {
            if (!EditorApplication.isPlaying || !Application.isFocused)
            {
                return false;
            }

            EditorWindow activeWindow = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
            return activeWindow != null
                && string.Equals(activeWindow.GetType().Name, "GameView", StringComparison.Ordinal);
        }

        public static OnboardingAsyncStatus GetGameViewFocusStatus()
        {
            if (!EditorApplication.isPlaying)
            {
                return OnboardingAsyncStatus.Idle("Enter Play Mode before focusing the Game view.");
            }

            return IsGameViewFocused()
                ? OnboardingAsyncStatus.Succeeded("The Game view is focused.")
                : OnboardingAsyncStatus.Idle("Click inside the Game view so the simulator receives input.");
        }

        public static void PrepareSimulatorInputStep()
        {
            simulatorLookDetected = false;
            simulatorZoomDetected = false;
            simulatorAutoRotateDetected = false;
            simulatorSwitchCharacterDetected = false;
            simulatorHasMousePosition = false;
            simulatorLastMousePosition = Vector2.zero;
            simulatorHasCameraBaseline = TryGetSimulatorCameraState(out SimulatorCameraState cameraState);
            simulatorBaselineCameraOrbitX = cameraState.OrbitX;
            simulatorBaselineZoomRadius = cameraState.ZoomRadius;
            simulatorBaselineAutoRotateEnabled = cameraState.AutoRotateEnabled;
            simulatorBaselineActiveModelIndex = GetSimulatorActiveModelIndex();
            simulatorHasActiveModelIndex = simulatorBaselineActiveModelIndex >= 0;
            simulatorHasSwitchVersion = TryGetSimulatorSwitchVersion(out simulatorBaselineSwitchVersion);
        }

        public static bool SimulatorLookDetected()
        {
            UpdateSimulatorInputDetections();
            return simulatorLookDetected;
        }

        public static bool SimulatorZoomDetected()
        {
            UpdateSimulatorInputDetections();
            return simulatorZoomDetected;
        }

        public static bool SimulatorAutoRotateDetected()
        {
            UpdateSimulatorInputDetections();
            return simulatorAutoRotateDetected;
        }

        public static bool SimulatorSwitchCharacterDetected()
        {
            UpdateSimulatorInputDetections();
            return simulatorSwitchCharacterDetected;
        }

        public static OnboardingAsyncStatus GetSimulatorLookStatus()
        {
            return GetSimulatorInputStatus(
                SimulatorLookDetected(),
                "Camera movement detected.",
                "Left-click or drag in the Game view to move around the character.");
        }

        public static OnboardingAsyncStatus GetSimulatorZoomStatus()
        {
            return GetSimulatorInputStatus(
                SimulatorZoomDetected(),
                "Camera zoom detected.",
                "Use the mouse scroll wheel in the Game view to zoom.");
        }

        public static OnboardingAsyncStatus GetSimulatorAutoRotateStatus()
        {
            return GetSimulatorInputStatus(
                SimulatorAutoRotateDetected(),
                "Auto rotate input detected.",
                "Press R in the Game view to toggle auto rotate.");
        }

        public static OnboardingAsyncStatus GetSimulatorSwitchCharacterStatus()
        {
            return GetSimulatorInputStatus(
                SimulatorSwitchCharacterDetected(),
                "Character switch input detected.",
                "Press Q or E in the Game view to switch active characters.");
        }

        private static OnboardingAsyncStatus GetSimulatorInputStatus(bool detected, string successMessage, string waitingMessage)
        {
            if (detected)
            {
                return OnboardingAsyncStatus.Succeeded(successMessage);
            }

            if (!EditorApplication.isPlaying)
            {
                return OnboardingAsyncStatus.Failed("Play Mode stopped. Click Retry to re-enter Play Mode, then continue the simulator steps.");
            }

            if (!IsGameViewFocused())
            {
                return OnboardingAsyncStatus.Idle("Click inside the Game view so the simulator receives input.");
            }

            return detected
                ? OnboardingAsyncStatus.Succeeded(successMessage)
                : OnboardingAsyncStatus.Idle(waitingMessage);
        }

        private static void UpdateSimulatorInputDetections()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            UpdateSimulatorCameraStateDetections();
            UpdateSimulatorCharacterSwitchDetection();

            bool hasSnapshot = TryReadSimulatorInput(out SimulatorInputSnapshot snapshot);
            if (hasSnapshot && snapshot.SwitchCharacterPressed)
            {
                simulatorSwitchCharacterDetected = true;
            }

            if (!IsGameViewFocused() || !hasSnapshot)
            {
                return;
            }

            if (snapshot.LeftMousePressed
                || (snapshot.LeftMouseHeld && snapshot.MouseDelta.sqrMagnitude > 4f))
            {
                simulatorLookDetected = true;
            }

            if (Mathf.Abs(snapshot.Scroll) > 0.01f)
            {
                simulatorZoomDetected = true;
            }

            if (snapshot.AutoRotatePressed)
            {
                simulatorAutoRotateDetected = true;
            }

            UpdateSimulatorCharacterSwitchDetection();

            simulatorLastMousePosition = snapshot.MousePosition;
            simulatorHasMousePosition = true;
        }

        private static void UpdateSimulatorCameraStateDetections()
        {
            if (!TryGetSimulatorCameraState(out SimulatorCameraState cameraState))
            {
                return;
            }

            if (!simulatorHasCameraBaseline)
            {
                simulatorHasCameraBaseline = true;
                simulatorBaselineCameraOrbitX = cameraState.OrbitX;
                simulatorBaselineZoomRadius = cameraState.ZoomRadius;
                simulatorBaselineAutoRotateEnabled = cameraState.AutoRotateEnabled;
                return;
            }

            if (Mathf.Abs(Mathf.DeltaAngle(simulatorBaselineCameraOrbitX, cameraState.OrbitX)) > 0.25f)
            {
                simulatorLookDetected = true;
            }

            if (Mathf.Abs(cameraState.ZoomRadius - simulatorBaselineZoomRadius) > 0.01f)
            {
                simulatorZoomDetected = true;
            }

            if (cameraState.AutoRotateEnabled != simulatorBaselineAutoRotateEnabled)
            {
                simulatorAutoRotateDetected = true;
            }
        }

        private static void UpdateSimulatorCharacterSwitchDetection()
        {
            if (TryGetSimulatorSwitchVersion(out int switchVersion))
            {
                if (!simulatorHasSwitchVersion)
                {
                    simulatorHasSwitchVersion = true;
                    simulatorBaselineSwitchVersion = switchVersion;
                }
                else if (switchVersion != simulatorBaselineSwitchVersion)
                {
                    simulatorSwitchCharacterDetected = true;
                }
            }

            int currentActiveModelIndex = GetSimulatorActiveModelIndex();
            if (currentActiveModelIndex < 0)
            {
                return;
            }

            if (!simulatorHasActiveModelIndex)
            {
                simulatorHasActiveModelIndex = true;
                simulatorBaselineActiveModelIndex = currentActiveModelIndex;
                return;
            }

            if (currentActiveModelIndex != simulatorBaselineActiveModelIndex)
            {
                simulatorSwitchCharacterDetected = true;
            }
        }

        private static int GetSimulatorActiveModelIndex()
        {
            CharacterPlacementController controller = GetPlacementController();
            return controller != null ? controller.ActiveModelIndex : -1;
        }

        private static bool TryGetSimulatorSwitchVersion(out int switchVersion)
        {
            switchVersion = 0;
            CharacterPlacementController controller = GetPlacementController();
            if (controller == null)
            {
                return false;
            }

            switchVersion = controller.InputSwitchVersion;
            return true;
        }

        private static bool TryGetSimulatorCameraState(out SimulatorCameraState state)
        {
            state = default;
            SimulatorCameraInputController[] controllers =
                UnityEngine.Object.FindObjectsByType<SimulatorCameraInputController>(FindObjectsSortMode.None);
            foreach (SimulatorCameraInputController controller in controllers)
            {
                if (controller == null || !controller.isActiveAndEnabled || !controller.HasValidCameraState)
                {
                    continue;
                }

                state = new SimulatorCameraState
                {
                    OrbitX = controller.CurrentOrbitX,
                    ZoomRadius = controller.CurrentZoomRadius,
                    AutoRotateEnabled = controller.AutoRotateEnabled
                };
                return true;
            }

            return false;
        }

        private static bool TryReadSimulatorInput(out SimulatorInputSnapshot snapshot)
        {
            bool hasInputSystemSnapshot = TryReadInputSystemSimulatorInput(out SimulatorInputSnapshot inputSystemSnapshot);
            bool hasLegacySnapshot = TryReadLegacySimulatorInput(out SimulatorInputSnapshot legacySnapshot);
            if (!hasInputSystemSnapshot && !hasLegacySnapshot)
            {
                snapshot = default;
                return false;
            }

            snapshot = hasInputSystemSnapshot ? inputSystemSnapshot : legacySnapshot;
            if (hasLegacySnapshot)
            {
                snapshot.LeftMousePressed |= legacySnapshot.LeftMousePressed;
                snapshot.LeftMouseHeld |= legacySnapshot.LeftMouseHeld;
                snapshot.AutoRotatePressed |= legacySnapshot.AutoRotatePressed;
                snapshot.SwitchCharacterPressed |= legacySnapshot.SwitchCharacterPressed;
                if (Mathf.Abs(legacySnapshot.Scroll) > Mathf.Abs(snapshot.Scroll))
                {
                    snapshot.Scroll = legacySnapshot.Scroll;
                }

                if (legacySnapshot.MouseDelta.sqrMagnitude > snapshot.MouseDelta.sqrMagnitude)
                {
                    snapshot.MouseDelta = legacySnapshot.MouseDelta;
                }

                if (legacySnapshot.MousePosition != Vector2.zero)
                {
                    snapshot.MousePosition = legacySnapshot.MousePosition;
                }
            }

            return true;
        }

        private static bool TryReadLegacySimulatorInput(out SimulatorInputSnapshot snapshot)
        {
            snapshot = default;
            try
            {
                Vector2 mousePosition = Input.mousePosition;
                snapshot.LeftMousePressed = Input.GetMouseButtonDown(0);
                snapshot.LeftMouseHeld = Input.GetMouseButton(0);
                snapshot.MousePosition = mousePosition;
                snapshot.MouseDelta = simulatorHasMousePosition ? mousePosition - simulatorLastMousePosition : Vector2.zero;
                snapshot.Scroll = Input.mouseScrollDelta.y;
                snapshot.AutoRotatePressed = Input.GetKeyDown(KeyCode.R);
                snapshot.SwitchCharacterPressed = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadInputSystemSimulatorInput(out SimulatorInputSnapshot snapshot)
        {
            snapshot = default;
            try
            {
                object mouse = GetInputSystemCurrent("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
                object keyboard = GetInputSystemCurrent("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
                if (mouse == null && keyboard == null)
                {
                    return false;
                }

                object leftButton = GetMemberValue(mouse, "leftButton");
                object positionControl = GetMemberValue(mouse, "position");
                object deltaControl = GetMemberValue(mouse, "delta");
                object scrollControl = GetMemberValue(mouse, "scroll");
                Vector2 mousePosition = ReadVector2Control(positionControl);
                snapshot.LeftMousePressed = WasPressedThisFrame(leftButton);
                snapshot.LeftMouseHeld = IsPressed(leftButton);
                snapshot.MousePosition = mousePosition;
                snapshot.MouseDelta = ReadVector2Control(deltaControl);
                if (snapshot.MouseDelta == Vector2.zero && simulatorHasMousePosition)
                {
                    snapshot.MouseDelta = mousePosition - simulatorLastMousePosition;
                }

                snapshot.Scroll = ReadVector2Control(scrollControl).y;
                snapshot.AutoRotatePressed = WasPressedThisFrame(GetMemberValue(keyboard, "rKey"));
                snapshot.SwitchCharacterPressed =
                    WasPressedThisFrame(GetMemberValue(keyboard, "qKey"))
                    || WasPressedThisFrame(GetMemberValue(keyboard, "eKey"));
                return true;
            }
            catch
            {
                snapshot = default;
                return false;
            }
        }

        private static object GetInputSystemCurrent(string typeName)
        {
            Type type = Type.GetType(typeName, false);
            PropertyInfo property = type != null
                ? type.GetProperty("current", BindingFlags.Public | BindingFlags.Static)
                : null;
            return property != null ? property.GetValue(null) : null;
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return property.GetValue(instance);
            }

            FieldInfo field = type.GetField(memberName, flags);
            return field != null ? field.GetValue(instance) : null;
        }

        private static bool WasPressedThisFrame(object buttonControl)
        {
            return ReadBoolProperty(buttonControl, "wasPressedThisFrame");
        }

        private static bool IsPressed(object buttonControl)
        {
            return ReadBoolProperty(buttonControl, "isPressed");
        }

        private static bool ReadBoolProperty(object instance, string propertyName)
        {
            if (instance == null)
            {
                return false;
            }

            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            return property != null
                && property.PropertyType == typeof(bool)
                && (bool)property.GetValue(instance);
        }

        private static Vector2 ReadVector2Control(object control)
        {
            if (control == null)
            {
                return Vector2.zero;
            }

            MethodInfo method = control.GetType().GetMethod(
                "ReadValueAsObject",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            object value = method != null ? method.Invoke(control, null) : null;
            return value is Vector2 vector ? vector : Vector2.zero;
        }

        private static HSRCharacterController GetActiveCharacterController()
        {
            HoyoToonManagerWindow window;
            GameObject activeModel = HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null
                ? window.CurrentContext?.PlacementActiveModel
                : null;
            return activeModel != null ? activeModel.GetComponentInChildren<HSRCharacterController>(true) : null;
        }

        public static GameObject FindDownloadedModelAsset(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return null;
            }

            EnsureDownloadRootImported();

            GameObject recentModel = FindRecentlyDownloadedModelAsset(characterName);
            if (recentModel != null)
            {
                return recentModel;
            }

            if (AssetDatabase.IsValidFolder(DownloadRoot))
            {
                string tutorialModelName = GetTutorialModelSearchName(characterName);
                if (!string.Equals(tutorialModelName, characterName, StringComparison.OrdinalIgnoreCase))
                {
                    GameObject tutorialNamedModel = FindModelAssetByQuery(tutorialModelName + " t:GameObject", tutorialModelName, requireNameMatch: true);
                    if (tutorialNamedModel != null)
                    {
                        return tutorialNamedModel;
                    }
                }

                GameObject exactModel = FindModelAssetByQuery(characterName + " t:GameObject", characterName, requireNameMatch: true);
                if (exactModel != null)
                {
                    return exactModel;
                }

                GameObject pathMatchedModel = FindModelAssetByQuery("t:GameObject", characterName, requireNameMatch: false);
                if (pathMatchedModel != null)
                {
                    return pathMatchedModel;
                }
            }

            return TryImportModelAssetFromDisk(characterName);
        }

        private static GameObject FindRecentlyDownloadedModelAsset(string characterName)
        {
            IReadOnlyList<string> recentPaths = GetBackend().GetLastManagerDownloadedModelAssetPathsFor(characterName);
            foreach (string assetPath in recentPaths ?? Array.Empty<string>())
            {
                GameObject model = LoadModelAssetAtPath(assetPath, forceImport: true);
                if (model != null)
                {
                    return model;
                }
            }

            return null;
        }

        private static GameObject LoadModelAssetAtPath(string assetPath, bool forceImport)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            string normalizedPath = assetPath.Replace('\\', '/');
            if (forceImport && !EditorApplication.isUpdating)
            {
                AssetDatabase.ImportAsset(normalizedPath, ImportAssetOptions.ForceSynchronousImport);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(normalizedPath);
        }

        private static string GetTutorialModelSearchName(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return string.Empty;
            }

            string compactName = characterName.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            if (string.Equals(compactName, "Acheron", StringComparison.OrdinalIgnoreCase))
            {
                return "Art_Acheron";
            }

            if (string.Equals(compactName, "Cyrene", StringComparison.OrdinalIgnoreCase))
            {
                return "Art_Cyrene";
            }

            return characterName;
        }

        private static GameObject FindModelAssetByQuery(string query, string characterName, bool requireNameMatch)
        {
            string comparableCharacterName = NormalizeAssetName(characterName);
            string[] guids = AssetDatabase.FindAssets(query, new[] { DownloadRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(path);
                bool fileNameMatches = fileName.IndexOf(characterName, StringComparison.OrdinalIgnoreCase) >= 0
                    || NormalizeAssetName(fileName).IndexOf(comparableCharacterName, StringComparison.OrdinalIgnoreCase) >= 0;
                bool pathMatches = NormalizeAssetName(path).IndexOf(comparableCharacterName, StringComparison.OrdinalIgnoreCase) >= 0;
                if (requireNameMatch && !fileNameMatches)
                {
                    continue;
                }

                if (!requireNameMatch && !fileNameMatches && !pathMatches)
                {
                    continue;
                }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    return model;
                }
            }

            return null;
        }

        private static string NormalizeAssetName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        }

        private static void EnsureDownloadRootImported()
        {
            if (AssetDatabase.IsValidFolder(DownloadRoot) || EditorApplication.isUpdating)
            {
                return;
            }

            string absoluteDownloadRoot = GetAbsoluteDownloadRoot();
            if (!Directory.Exists(absoluteDownloadRoot))
            {
                return;
            }

            AssetDatabase.ImportAsset(DownloadRoot, ImportAssetOptions.ForceSynchronousImport);
        }

        private static GameObject TryImportModelAssetFromDisk(string characterName)
        {
            string absoluteDownloadRoot = GetAbsoluteDownloadRoot();
            if (string.IsNullOrWhiteSpace(absoluteDownloadRoot)
                || !Directory.Exists(absoluteDownloadRoot)
                || EditorApplication.isUpdating
                || !CanRetryModelImport(characterName))
            {
                return null;
            }

            string normalizedCharacterName = NormalizeAssetName(characterName);
            string normalizedModelName = NormalizeAssetName(GetTutorialModelSearchName(characterName));
            foreach (string assetPath in EnumerateModelAssetPaths(absoluteDownloadRoot, normalizedCharacterName))
            {
                GameObject model = LoadModelAssetAtPath(assetPath, forceImport: true);
                if (model != null)
                {
                    return model;
                }
            }

            if (!string.Equals(normalizedModelName, normalizedCharacterName, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string assetPath in EnumerateModelAssetPaths(absoluteDownloadRoot, normalizedModelName))
                {
                    GameObject model = LoadModelAssetAtPath(assetPath, forceImport: true);
                    if (model != null)
                    {
                        return model;
                    }
                }
            }

            return null;
        }

        private static bool CanRetryModelImport(string characterName)
        {
            double now = EditorApplication.timeSinceStartup;
            string key = string.IsNullOrWhiteSpace(characterName) ? string.Empty : characterName.Trim();
            double lastTime;
            if (LastModelImportRetryByCharacter.TryGetValue(key, out lastTime) && now - lastTime < 1d)
            {
                return false;
            }

            LastModelImportRetryByCharacter[key] = now;
            return true;
        }

        private static IEnumerable<string> EnumerateModelAssetPaths(string absoluteDownloadRoot, string normalizedCharacterName)
        {
            return Directory.EnumerateFiles(absoluteDownloadRoot, "*.*", SearchOption.AllDirectories)
                .Where(path => IsModelFile(path))
                .Where(path => NormalizeAssetName(path).IndexOf(normalizedCharacterName, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(ConvertAbsolutePathToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .OrderBy(path => path.IndexOf("/Default/", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsModelFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetAbsoluteDownloadRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "HoyoToon", "Characters"));
        }

        private static string ConvertAbsolutePathToAssetPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(absolutePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string relativePath = fullPath.Substring(assetsRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            return string.IsNullOrWhiteSpace(relativePath) ? string.Empty : "Assets/" + relativePath;
        }

        public static void RetryDownload()
        {
            OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.Download);
            if (GetBackend().CanDownloadSelectionForManager())
            {
                OnboardingSignals.RecordAction("Assets.DownloadButton");
                GetBackend().StartDownloadForManager();
            }
        }

        private static bool WasDownloadButtonClickedThisStep()
        {
            return OnboardingSignals.GetActionCountSinceStepStart("Assets.DownloadButton") > 0;
        }

        public static void RetrySetup()
        {
            OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.Setup);
            HoyoToonManagerWindow window;
            if (HoyoToonManagerWindow.TryGetOpenWindow(out window))
            {
                window.RunAutoSetupForSelectedModel();
            }
        }

        public static void RetryRender()
        {
            OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.Render);
        }

        public static void RetryTurnaround()
        {
            OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.TurnaroundRender);
        }

        private static AssetDownloadWindow GetBackend()
        {
            AssetDownloadWindow backend = AssetDownloadWindow.GetOrCreateSharedBackend();
            backend.EnsureInitializedForManager();
            return backend;
        }
    }
}
#endif
