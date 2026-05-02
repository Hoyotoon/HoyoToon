#if UNITY_EDITOR
using System.Collections.Generic;

namespace HoyoToon.Editor.Onboarding
{
    internal static class OnboardingStepDefinitions
    {
        public static IReadOnlyList<OnboardingStep> BuildDefaultFlow()
        {
            var steps = new List<OnboardingStep>();

            steps.Add(Dialog(
                "Welcome",
                "Welcome to HoyoToon",
                Md(
                    "### Your guided setup",
                    "Welcome to **HoyoToon**. You are using **" + OnboardingValidation.PackageVersionLabel + "**.",
                    "",
                    "This onboarding process will guide you through the key features of HoyoToon, and help you get started.",
                    "",
                    "**You will practice:**",
                    "- Checking that HoyoToon is up to date.",
                    "- Opening the HoyoToon scene.",
                    "- Opening and docking the Manager.",
                    "- Downloading and setting up two tutorial models.",
                    "- Switching active characters, tuning scene controls, and creating renders.",
                    "",
                    "During guided steps, unrelated UI is locked so the highlighted control stays clear."),
                "Dialog.ContinueButton",
                "Start the guided onboarding."));

            steps.Add(ActionStep(
                "UserProfile.Create",
                "Create local profile",
                Md(
                    "### Your HoyoToon profile",
                    "What should we call you?",
                    "",
                    "The HoyoToon API will assign the next numeric UID and create your user record.",
                    "",
                    "Only your local user profile is stored as a ScriptableObject; the API user table is not mirrored into project assets.",
                    "",
                    "If you already created a HoyoToon profile on this computer, onboarding will restore it before asking for a new username."),
                new string[0],
                new string[0],
                OnboardingValidation.HasLocalUserProfile,
                OnboardingValidation.GetLocalUserProfileStatus,
                OnboardingValidation.PromptForLocalUserProfileForOnboarding,
                OnboardingValidation.PromptForLocalUserProfileForOnboarding,
                "Enter your HoyoToon username."));

            steps.Add(ActionStep(
                "Updater.Check",
                "Check for HoyoToon updates",
                Md(
                    "### Start from the latest package",
                    "Before setup begins, HoyoToon checks whether this package is up to date.",
                    "",
                    "**If an update is available:**",
                    "- Choose **Update Now** in the updater dialog.",
                    "- Let the updater finish applying files.",
                    "- Continue onboarding after the status reports that HoyoToon is up to date.",
                    "",
                    "Just making sure you're up to date."),
                new string[0],
                new string[0],
                OnboardingValidation.UpdaterPassed,
                OnboardingValidation.GetUpdaterStatus,
                OnboardingValidation.RunUpdaterCheck,
                OnboardingValidation.RunUpdaterCheck,
                "Check for updates before continuing."));

            steps.Add(Dialog(
                "Prerequisites.Check",
                "Check required resources",
                Md(
                    "### Before the first model",
                    "For HoyoToon to work properly, required resource files and project settings need to be in place.",
                    "",
                    "**HoyoToon will do this next:**",
                    "- Download or update required resource files.",
                    "- Check the project settings needed for HoyoToon rendering and setup.",
                    "",
                    "**Do this:** click **Continue** to run the resource downloader."),
                "Dialog.ContinueButton",
                "Start the resource check."));

            steps.Add(ActionStep(
                "Prerequisites.Run",
                "Prepare resources and settings",
                Md(
                    "### Preparing HoyoToon",
                    "HoyoToon is downloading required resources, then checking project settings and applying safe setup fixes.",
                    "",
                    "Wait here until the status below finishes. If anything blocking remains, this step will explain what needs attention before onboarding continues."),
                new string[0],
                new string[0],
                OnboardingValidation.ResourcesAndPrerequisitesPassed,
                OnboardingValidation.GetResourceAndPrerequisiteStatus,
                OnboardingValidation.BeginResourceAndPrerequisiteCheck,
                OnboardingValidation.RetryResourceAndPrerequisiteCheck));

            steps.Add(ActionStep(
                "Scene.OpenHoyoToon",
                "Open HoyoToon Scene",
                Md(
                    "### Load the HoyoToon scene",
                    "The guided setup expects the **HoyoToon** scene to be open before the Manager starts.",
                    "",
                    "HoyoToon will open that scene automatically. If Unity asks about saving your current scene, save or discard your changes before continuing.",
                    "",
                    "The onboarding continues once the HoyoToon scene is loaded."),
                new string[0],
                new string[0],
                OnboardingValidation.IsHoyoToonSceneOpen,
                OnboardingValidation.GetHoyoToonSceneStatus,
                OnboardingValidation.OpenHoyoToonSceneForOnboarding,
                OnboardingValidation.OpenHoyoToonSceneForOnboarding,
                "Opening the HoyoToon scene."));

            steps.Add(ActionStep(
                "OpenManager",
                "Open HoyoToon Manager",
                Md(
                    "### The main workbench",
                    "The **HoyoToon Manager** is where the rest of the onboarding happens.",
                    "",
                    "You will use it for asset downloads, model setup, character settings, scene controls, and renders.",
                    "",
                    "HoyoToon will open the Manager automatically, then the onboarding will continue once the window is available."),
                new string[0],
                new string[0],
                OnboardingValidation.IsManagerOpen,
                OnboardingValidation.GetManagerOpenStatus,
                OnboardingValidation.OpenManagerForOnboarding,
                OnboardingValidation.OpenManagerForOnboarding,
                "Opening the HoyoToon Manager."));

            steps.Add(Confirm(
                "DockManager",
                "Dock HoyoToon Manager",
                Md(
                    "### Keep the Manager visible",
                    "Dock the **HoyoToon Manager** beside the Unity Scene or Inspector view.",
                    "",
                    "That keeps the guided controls visible while you work in the scene.",
                    "",
                    "**Do this:** dock the window, then click **Confirm**."),
                new[] { "Dialog.DockConfirmButton" },
                "Confirm once the Manager is docked."));

            steps.Add(ModuleClick(
                "Assets.OpenModule",
                "Open Assets module",
                Md(
                    "### Start with assets",
                    "The **Assets** module is where supported character models are searched and downloaded.",
                    "",
                    "**Do this:** click the highlighted **Assets** tab."),
                "Manager.Nav.AssetsTab",
                "assets"));

            AddDownloadCharacterSteps(steps, "Acheron", "Assets.DownloadAcheron", true);
            AddDownloadCharacterSteps(steps, "Cyrene", "Assets.DownloadCyrene", false);

            steps.Add(ModuleClick(
                "Setup.OpenModule",
                "Open Setup module",
                Md(
                    "### Prepare the downloaded models",
                    "The **Setup** module turns downloaded models into characters HoyoToon can use in the scene.",
                    "",
                    "It can configure materials, shaders, model import settings, hierarchy, and scene integration.",
                    "",
                    "Both tutorial models are downloaded now, so the next steps queue **Art_Acheron** and **Art_Cyrene** before Auto Setup runs.",
                    "",
                    "**Do this:** click the highlighted **Setup** tab."),
                "Manager.Nav.SetupTab",
                "setup"));

            OnboardingStep addAcheronStep = Field(
                "Setup.AddAcheron",
                "Add Acheron to Setup list",
                Md(
                    "### Choose the model to process",
                    "Add **Art_Acheron** to the setup list.",
                    "",
                    "HoyoToon will pre-fill the model field when the downloaded FBX is available.",
                    "",
                    "**Do this:** confirm **Art_Acheron** is queued, then click **Continue**."),
                new[] { "Setup.ModelInputField", "Setup.ModelList" },
                new[] { "Setup.ModelInputField", "Setup.ModelList", "Dialog.ContinueButton" },
                "Art_Acheron",
                () => OnboardingValidation.IsModelQueued("Acheron"),
                "Select the downloaded Art_Acheron FBX model.");
            addAcheronStep.OnEnter = () => OnboardingValidation.PrepareSetupModelSelection("Acheron");
            addAcheronStep.Retry = () => OnboardingValidation.PrepareSetupModelSelection("Acheron");
            addAcheronStep.ShowContinueButton = true;
            steps.Add(addAcheronStep);

            OnboardingStep addCyreneStep = Field(
                "Setup.AddCyrene",
                "Add Cyrene to Setup list",
                Md(
                    "### Select the second model",
                    "Add **Art_Cyrene** to the setup list.",
                    "",
                    "The setup list should contain both tutorial models before Auto Setup runs.",
                    "",
                    "**Do this:** confirm **Art_Cyrene** is also queued, then click **Continue**."),
                new[] { "Setup.ModelInputField", "Setup.ModelList" },
                new[] { "Setup.ModelInputField", "Setup.ModelList", "Dialog.ContinueButton" },
                "Art_Cyrene",
                () => OnboardingValidation.IsModelQueued("Cyrene"),
                "Select the downloaded Art_Cyrene FBX model.");
            addCyreneStep.OnEnter = () => OnboardingValidation.PrepareSetupModelSelection("Cyrene", false);
            addCyreneStep.Retry = () => OnboardingValidation.PrepareSetupModelSelection("Cyrene", false);
            addCyreneStep.ShowContinueButton = true;
            steps.Add(addCyreneStep);

            steps.Add(ActionStep(
                "Setup.AutoSetupTutorialModels",
                "Auto Setup tutorial models",
                Md(
                    "### Run Auto Setup once",
                    "**Auto Setup** processes the queued models so both are ready for usage.",
                    "",
                    "**Auto setup will do the following:**",
                    "- Convert the model.",
                    "- Generate Materials",
                    "- Configure the FBX Import Settings",
                    "- Set the Texture Import settings",
                    "- Add the model to the scene.",
                    "- Generate Tangents.",
                    "- Add the required components.",
                    "",
                    "**Do this:** click **Auto Setup** and wait until both tutorial characters are available."),
                new[] { "Setup.AutoSetupButton", "Setup.ProgressBar" },
                new[] { "Setup.AutoSetupButton" },
                OnboardingValidation.HasSetupTutorialModels,
                OnboardingValidation.GetTutorialModelsSetupStatus,
                null,
                OnboardingValidation.RetrySetup,
                "Click Auto Setup for both tutorial models."));

            steps.Add(ActionStep(
                "Scene.ActiveCharacter.SelectAcheron",
                "Select active character",
                Md(
                    "### Active character context",
                    "This control decides which character the Manager is currently editing.",
                    "",
                    "Many **Character** tab settings apply to the active character only, so this context matters before you tune anything.",
                    "",
                    "**Do this:** select **Acheron** as the active character before continuing."),
                new[] { "Scene.ActiveCharacterDropdown" },
                new[] { "Scene.ActiveCharacterDropdown" },
                () => OnboardingValidation.IsActiveCharacterSelectedAfterInteraction("Acheron"),
                () => OnboardingValidation.GetActiveCharacterStatus("Acheron"),
                () => OnboardingValidation.PrepareActiveCharacterSelection("Acheron"),
                () => OnboardingValidation.PrepareActiveCharacterSelection("Acheron"),
                "Select Acheron as the active character."));

            OnboardingStep placementSingleStep = Multi(
                "Scene.PlacementMode.Single",
                "Set placement: Single",
                Md(
                    "### Placement modes",
                    "The placement control changes how characters are arranged in the scene.",
                    "",
                    "**Do this:** set the highlighted control to **Single**.",
                    "",
                    "Single mode focuses the Manager on one active character."),
                new[] { "Scene.PlacementModeDropdown" },
                new[] { "Scene.PlacementModeDropdown" },
                OnboardingValidation.PlacementModeSingleSelectedAfterInteraction,
                "Set placement to Single.");
            placementSingleStep.OnEnter = OnboardingValidation.PreparePlacementSingleStep;
            placementSingleStep.Retry = OnboardingValidation.PreparePlacementSingleStep;
            steps.Add(placementSingleStep);

            steps.Add(Multi(
                "Scene.PlacementMode.Team",
                "Set placement: Team",
                Md(
                    "### Team arrangement",
                    "Team mode lets multiple characters be active together.",
                    "",
                    "**Do this:** set the highlighted placement control to **Team**."),
                new[] { "Scene.PlacementModeDropdown" },
                new[] { "Scene.PlacementModeDropdown" },
                OnboardingValidation.PlacementModeTeamSelectedAfterInteraction,
                "Set placement to Team."));

            steps.Add(Multi(
                "Scene.PlacementMode.Grid",
                "Set placement: Grid",
                Md(
                    "### Grid arrangement",
                    "Grid mode lays the tutorial characters out in a structured arrangement.",
                    "",
                    "**Do this:** set the highlighted placement control to **Grid**."),
                new[] { "Scene.PlacementModeDropdown" },
                new[] { "Scene.PlacementModeDropdown" },
                OnboardingValidation.PlacementModeGridSelectedAfterInteraction,
                "Set placement to Grid."));

            steps.Add(Multi(
                "Scene.PlacementMode.BackToSingle",
                "Return placement to Single",
                Md(
                    "### Return to editing one character",
                    "Most of the next controls edit the active character, so return the placement mode to **Single** before continuing.",
                    "",
                    "**Do this:** set the highlighted placement control back to **Single**."),
                new[] { "Scene.PlacementModeDropdown" },
                new[] { "Scene.PlacementModeDropdown" },
                OnboardingValidation.PlacementModeSingleSelectedAfterInteraction,
                "Set placement back to Single."));

            steps.Add(ModuleClick(
                "Character.OpenTab",
                "Open Character tab",
                Md(
                    "### Per-character controls",
                    "The **Character** tab contains settings for the currently active character.",
                    "",
                    "Changes here can affect one model without changing every model in the scene.",
                    "",
                    "**Do this:** click the highlighted **Character** tab."),
                "Manager.Nav.CharacterTab",
                "character"));

            steps.Add(ActionStep(
                "Character.EnsureController",
                "Prepare character controller",
                Md(
                    "### Use the active character",
                    "The **Character** tab edits the active model.",
                    "",
                    "HoyoToon is making sure **Acheron** is active so the controller fields below are available.",
                    "",
                    "If the active character field is empty, select **Acheron** there."),
                new[] { "Character.MainSettingsSection", "Scene.ActiveCharacterDropdown" },
                new[] { "Scene.ActiveCharacterDropdown" },
                OnboardingValidation.CharacterControllerReady,
                OnboardingValidation.GetCharacterControllerStatus,
                () => OnboardingValidation.EnsureActiveCharacterSelected("Acheron"),
                () => OnboardingValidation.EnsureActiveCharacterSelected("Acheron"),
                "Select an active character before changing controller settings."));

            OnboardingStep characterExplainStep = Dialog(
                "Character.ExplainSettings",
                "Character settings",
                Md(
                    "### Check context first",
                    "This section controls the **active character**.",
                    "",
                    "Use it when you want to adjust how one specific character looks or behaves.",
                    "",
                    "**Before changing values:** confirm the active character control points at the model you intend to edit.",
                    "",
                    "**Do this:** read this context, then click **Continue**."),
                new[] { "Character.MainSettingsSection", "Scene.ActiveCharacterDropdown" },
                new[] { "Dialog.ContinueButton" },
                "These controls affect the active character.");
            characterExplainStep.OnEnter = OnboardingValidation.RefreshOpenManagerForOnboarding;
            characterExplainStep.Retry = OnboardingValidation.RefreshOpenManagerForOnboarding;
            steps.Add(characterExplainStep);

            OnboardingStep selfShadowStep = Multi(
                "Character.SelfShadows.Toggle",
                "Toggle Self Shadows",
                Md(
                    "### Self Shadows",
                    "**Self Shadows** controls whether the character casts shadows onto itself.",
                    "",
                    "This is a quick way to see per-character visual settings in action.",
                    "",
                    "**Do this:** turn Self Shadows **off**, then turn it **on** again."),
                new[] { "Character.SelfShadowToggle", "Character.MainSettingsSection" },
                new[] { "Character.MainSettingsSection" },
                OnboardingValidation.SelfShadowToggledOffThenOn,
                "Turn Self Shadows off, then back on.");
            selfShadowStep.OnEnter = OnboardingValidation.PrepareSelfShadowToggleStep;
            selfShadowStep.Retry = OnboardingValidation.PrepareSelfShadowToggleStep;
            steps.Add(selfShadowStep);

            steps.Add(Dialog(
                "Character.LightSources.Explain",
                "Lighting Controls",
                Md(
                    "### Character lighting",
                    "The **Lighting Controls** section displays all available lighting options for the active character.",
                    "",
                    "Use it to adjust how one character is lit without changing unrelated parts of the scene.",
                    "",
                    "The exact fields can vary depending on the current HoyoToon setup.",
                    "",
                    "**Do this:** review this area, then click **Continue**."),
                "Character.LightSourcesBox",
                "This section controls character lighting."));

            steps.Add(Multi(
                "Character.Lighting.Adjust",
                "Adjust character lighting",
                Md(
                    "### Change and restore lighting",
                    "Modify one of the Light type strengths",
                    "",
                    "**Options include:**",
                    "- Key Light.",
                    "- Fill Light.",
                    "- Shadow Tint",
                    "",
                    "**Do this:** change the value once, then change it back before continuing."),
                new[] { "Character.LightSourcesBox" },
                new[] { "Character.LightSourcesBox" },
                OnboardingValidation.CharacterLightingChangedThenRestored,
                "Change one lighting value, then set it back."));

            steps.Add(ModuleClick(
                "Scene.OpenTab",
                "Open Scene tab",
                Md(
                    "### Global controls",
                    "The **Scene** tab contains shared scene controls.",
                    "",
                    "Unlike Character tab settings, these can affect the whole scene or render environment.",
                    "",
                    "**Do this:** click the highlighted **Scene** tab."),
                "Manager.Nav.SceneTab",
                "scene"));

            steps.Add(ActionStep(
                "Scene.EnsureController",
                "Prepare scene controller",
                Md(
                    "### Scene controller readiness",
                    "Scene controls need the HSR scene controller created by Auto Setup.",
                    "",
                    "HoyoToon is making sure the scene controller is ready so the controls below are available."),
                new[] { "Scene.GlobalControls" },
                new[] { "Scene.GlobalControls" },
                OnboardingValidation.SceneControllerReady,
                OnboardingValidation.GetSceneControllerStatus,
                null,
                null,
                "Wait for the scene controller check."));

            OnboardingStep sceneExplainStep = Dialog(
                "Scene.GlobalControls.Explain",
                "Global scene controls",
                Md(
                    "### Scene-wide visual behavior",
                    "These controls affect the broader HoyoToon scene.",
                    "",
                    "**Use them for:**",
                    "- Shared lighting or environment behavior.",
                    "- Outlines and visible style controls.",
                    "- Grading or other settings that are not limited to one character.",
                    "",
                    "**Do this:** read this context, then click **Continue**."),
                new[] { "Scene.GlobalControls" },
                new[] { "Dialog.ContinueButton" },
                "These controls affect the whole scene.");
            sceneExplainStep.OnEnter = OnboardingValidation.RefreshOpenManagerForOnboarding;
            sceneExplainStep.Retry = OnboardingValidation.RefreshOpenManagerForOnboarding;
            steps.Add(sceneExplainStep);

            OnboardingStep outlineScaleStep = Multi(
                "Scene.GlobalControls.Change",
                "Change Outline Scale",
                Md(
                    "### Change and restore Outline Scale",
                    "Outline Scale is a simple global control that visibly affects the scene style.",
                    "",
                    "The tutorial starts this value at **0.0149**, which is the default for Honkai Star Rail models.",
                    "",
                    "**Do this:** change **Outline Scale** to another value, then set it back to **0.0149**."),
                new[] { "Scene.OutlineScaleField" },
                new[] { "Scene.OutlineScaleField" },
                OnboardingValidation.GlobalSceneSettingChangedThenRestored,
                "Change Outline Scale, then set it back to 0.0149.");
            outlineScaleStep.OnEnter = OnboardingValidation.PrepareOutlineScaleStep;
            outlineScaleStep.Retry = OnboardingValidation.PrepareOutlineScaleStep;
            steps.Add(outlineScaleStep);

            OnboardingStep renderOpenStep = ModuleClick(
                "Render.OpenTab",
                "Open Render tab",
                Md(
                    "### Capture the result",
                    "The **Render** tab is HoyoToon's screenshot and render tool.",
                    "",
                    "it is a custom built tool to support all the unique rendering features Mihoyo's games require.",
                    "",
                    "**Do this:** click the highlighted **Render** tab."),
                "Manager.Nav.RenderTab",
                "renders");
            renderOpenStep.OnEnter = OnboardingValidation.EnableOpenAfterCaptureForOnboarding;
            renderOpenStep.Retry = OnboardingValidation.EnableOpenAfterCaptureForOnboarding;
            steps.Add(renderOpenStep);

            OnboardingStep renderSettingsStep = Dialog(
                "Render.Settings.Explain",
                "Render settings",
                Md(
                    "### Render settings",
                    "This section controls how screenshots are created and where files are saved.",
                    "",
                    "**Check this area for:**",
                    "- Output path.",
                    "- Scale, which the tutorial sets to **1x**.",
                    "- Open After Capture, which the tutorial enables for you.",
                    "- Screenshot settings.",
                    "- HoyoToon render feature options.",
                    "",
                    "Review the highlighted controls before creating the first render."),
                new[] { "Render.SettingsBox", "Render.OutputPathField", "Dialog.ContinueButton" },
                new[] { "Dialog.ContinueButton" },
                "Review the render output controls.");
            renderSettingsStep.OnEnter = OnboardingValidation.EnableOpenAfterCaptureForOnboarding;
            renderSettingsStep.Retry = OnboardingValidation.EnableOpenAfterCaptureForOnboarding;
            steps.Add(renderSettingsStep);

            steps.Add(ActionStep(
                "Render.CreateScreenshot",
                "Create screenshot render",
                Md(
                    "### First screenshot",
                    "Create a render of the current active character.",
                    "",
                    "**Do this:** click **Capture Screenshot** and wait for the your first render to open in your image viewer.",
                    "",
                    "The tutorial will continue once HoyoToon reports a successful render."),
                new[] { "Render.CreateRenderButton" },
                new[] { "Render.CreateRenderButton" },
                OnboardingValidation.ScreenshotRenderCreated,
                OnboardingValidation.GetRenderStatus,
                null,
                OnboardingValidation.RetryRender,
                "Click Capture Screenshot."));

            OnboardingStep turnaroundStep = Toggle(
                "Render.Turnaround.Enable",
                "Enable turnaround render",
                Md(
                    "### Turnaround output",
                    "Turnaround renders create a multi-perspective view of the active character.",
                    "",
                    "This is useful for concept art, model sheets, or any time you want to see the character from multiple angles at once.",
                    "",
                    "**Do this:** enable the highlighted **Turnaround** option."),
                new[] { "Render.TurnaroundToggle", "Render.TurnaroundSettingsBox" },
                new[] { "Render.TurnaroundToggle", "Render.TurnaroundSettingsBox" },
                "Enabled",
                OnboardingValidation.TurnaroundEnabledAfterInteraction,
                "Enable the Turnaround option.");
            turnaroundStep.OnEnter = OnboardingValidation.PrepareTurnaroundEnableStep;
            turnaroundStep.Retry = OnboardingValidation.PrepareTurnaroundEnableStep;
            steps.Add(turnaroundStep);

            steps.Add(ActionStep(
                "Render.CreateTurnaround",
                "Create turnaround render",
                Md(
                    "### Create the turnaround",
                    "Now capture the active character as a rotating render.",
                    "",
                    "**Do this:** click **Capture Turnaround** and wait for completion.",
                    "*Remember what I said about outlines? This is a good reminder how ass it'll look if you don't take the time for it :D*",
                    "If the render fails, use **Retry** after checking the status message."),
                new[] { "Render.CreateTurnaroundButton" },
                new[] { "Render.CreateTurnaroundButton" },
                OnboardingValidation.TurnaroundRenderCreated,
                OnboardingValidation.GetTurnaroundStatus,
                null,
                OnboardingValidation.RetryTurnaround,
                "Click Capture Turnaround."));

            OnboardingStep enterPlayModeStep = ActionStep(
                "Simulator.EnterPlayMode",
                "Enter Play Mode",
                Md(
                    "### Try the simulator camera",
                    "HoyoToon has a simulator that mirrors the ingame Character Screen. This will allow you to move the camera around, zoom, and interact with the character as if you were in the game.",
                    "",
                    "**Do this:** click **Confirm**.",
                    "",
                    "HoyoToon will switch Unity into Play Mode for you, then the tutorial will continue once the simulator is running."),
                new string[0],
                new string[0],
                OnboardingValidation.IsInPlayMode,
                OnboardingValidation.GetPlayModeStatus,
                null,
                OnboardingValidation.EnterPlayModeForOnboarding,
                "Click Confirm to enter Play Mode.");
            enterPlayModeStep.StepType = OnboardingStepType.Confirm;
            enterPlayModeStep.ShowContinueButton = true;
            enterPlayModeStep.RequireCompletionBeforeContinue = false;
            enterPlayModeStep.ContinueAction = OnboardingValidation.EnterPlayModeForOnboarding;
            enterPlayModeStep.AdvanceAfterContinueAction = false;
            enterPlayModeStep.AutoAdvanceWhenComplete = true;
            enterPlayModeStep.Retry = null;
            steps.Add(enterPlayModeStep);

            steps.Add(ActionStep(
                "Simulator.FocusGameView",
                "Focus Game view",
                Md(
                    "### Send input to the simulator",
                    "The simulator controls only work when the **Game** view has focus.",
                    "",
                    "**Do this:** click inside the Game view before using the camera controls."),
                new string[0],
                new string[0],
                OnboardingValidation.IsGameViewFocused,
                OnboardingValidation.GetGameViewFocusStatus,
                null,
                OnboardingValidation.EnterPlayModeForOnboarding,
                "Click inside the Game view."));

            steps.Add(ActionStep(
                "Simulator.MoveCamera",
                "Move around character",
                Md(
                    "### Move around the character",
                    "The simulator camera lets you inspect the active model interactively.",
                    "",
                    "**Do this:** left-click or drag in the Game view to move around the character."),
                new string[0],
                new string[0],
                OnboardingValidation.SimulatorLookDetected,
                OnboardingValidation.GetSimulatorLookStatus,
                OnboardingValidation.PrepareSimulatorInputStep,
                OnboardingValidation.EnterPlayModeForOnboarding,
                "Left-click or drag in Game view."));

            steps.Add(ActionStep(
                "Simulator.ZoomCamera",
                "Zoom camera",
                Md(
                    "### Zoom the simulator camera",
                    "Use zoom to inspect the model from close and distant views.",
                    "",
                    "**Do this:** use the mouse scroll wheel in the Game view."),
                new string[0],
                new string[0],
                OnboardingValidation.SimulatorZoomDetected,
                OnboardingValidation.GetSimulatorZoomStatus,
                OnboardingValidation.PrepareSimulatorInputStep,
                OnboardingValidation.EnterPlayModeForOnboarding,
                "Use the scroll wheel."));

            steps.Add(ActionStep(
                "Simulator.AutoRotate",
                "Toggle auto rotate",
                Md(
                    "### Auto rotate",
                    "Auto rotate is useful when you want to preview the character without manually moving the camera.",
                    "",
                    "**Do this:** press **R** in the Game view."),
                new string[0],
                new string[0],
                OnboardingValidation.SimulatorAutoRotateDetected,
                OnboardingValidation.GetSimulatorAutoRotateStatus,
                OnboardingValidation.PrepareSimulatorInputStep,
                OnboardingValidation.EnterPlayModeForOnboarding,
                "Press R."));

            steps.Add(ActionStep(
                "Simulator.SwitchCharacter",
                "Switch active character",
                Md(
                    "### Switch between tutorial characters",
                    "The simulator can cycle between the active characters you set up earlier.",
                    "",
                    "**Do this:** press **Q** or **E** in the Game view."),
                new string[0],
                new string[0],
                OnboardingValidation.SimulatorSwitchCharacterDetected,
                OnboardingValidation.GetSimulatorSwitchCharacterStatus,
                OnboardingValidation.PrepareSimulatorInputStep,
                OnboardingValidation.EnterPlayModeForOnboarding,
                "Press Q or E."));

            OnboardingStep updateBadgeStep = Dialog(
                "Updater.HeaderBadge.Explain",
                "Check for updates later",
                Md(
                    "### Keeping HoyoToon current",
                    "The header badge in the Manager is the quickest way to check package updates after onboarding.",
                    "",
                    "**Do this later:** click the highlighted version badge when you want to check for updates.",
                    "",
                    "**Badge statuses mean:**",
                    "- **Check updates:** no recent status is available yet.",
                    "- **Checking:** HoyoToon is looking for the latest package state.",
                    "- **Up to date:** your installed package matches the current release source.",
                    "- **Update available:** open the updater prompt, choose **Update Now**, and let Unity recompile/import after it finishes.",
                    "- **Applying:** the updater is writing files or waiting for Unity to import them.",
                    "- **Status error:** check the Console or connection, then try the badge again.",
                    "",
                    "**Do this now:** read this once, then click **Continue**."),
                new[] { "Manager.HeaderUpdateButton" },
                new[] { "Dialog.ContinueButton" },
                "This badge checks package updates.");
            updateBadgeStep.OnEnter = OnboardingValidation.FocusManagerHeaderForOnboarding;
            updateBadgeStep.Retry = OnboardingValidation.FocusManagerHeaderForOnboarding;
            steps.Add(updateBadgeStep);

            steps.Add(Dialog(
                "Completion.Support",
                "Onboarding complete",
                Md(
                    "### Congratulations!",
                    "You completed the HoyoToon onboarding.",
                    "",
                    "**You now know how to:**",
                    "- Open and navigate the Manager.",
                    "- Download and set up supported models.",
                    "- Switch active characters and adjust character or scene settings.",
                    "- Create screenshot and turnaround renders.",
                    "- Enter Play Mode and use the simulator camera controls.",
                    "- Use the Manager header badge to check for updates later.",
                    "- See your HoyoToon profile in the Manager header.",
                    "",
                    "Need help or want to report an issue?",
                    "",
                    "- [Discord server](https://discord.gg/hoyotoon)",
                    "- [GitHub issues](https://github.com/HoyoToon/HoyoToon/issues)"),
                "Dialog.ContinueButton",
                "Finish onboarding."));

            return steps;
        }

        private static void AddDownloadCharacterSteps(List<OnboardingStep> steps, string characterName, string prefix, bool firstCharacter)
        {
            OnboardingStep selectGameStep = Field(
                prefix + ".SelectGame",
                "Select game: Honkai Star Rail",
                Md(
                    "### Choose the asset library",
                    "This field selects which game database HoyoToon searches.",
                    "",
                    "**Do this:** choose **Honkai Star Rail**.",
                    "",
                    "The onboarding uses Honkai Star Rail models because I said so and that's that."),
                new[] { "Assets.GameDropdown" },
                new[] { "Assets.GameDropdown" },
                "Honkai Star Rail",
                OnboardingValidation.IsTutorialGameSelectedAfterInteraction,
                "Select Honkai Star Rail here.");
            steps.Add(selectGameStep);

            OnboardingStep selectCharacterStep = Field(
                prefix + ".SelectCharacter",
                "Select character: " + characterName,
                Md(
                    "### Pick the tutorial character",
                    "This field selects the character model to download.",
                    "",
                    "The search field is pre-filled so only the tutorial model is easy to pick.",
                    "",
                    "**Do this:** choose **" + characterName + "**" + (firstCharacter ? " for the first tutorial model." : " for the second tutorial model."),
                    "",
                    "Using a fixed character lets the tutorial validate download and setup progress reliably."),
                new[] { "Assets.CharacterSearch", "Assets.CharacterDropdown" },
                new[] { "Assets.CharacterSearch", "Assets.CharacterDropdown" },
                characterName,
                () => OnboardingValidation.IsCharacterSelectedAfterInteraction(characterName),
                "Search for and select " + characterName + ".");
            selectCharacterStep.OnEnter = () => OnboardingValidation.PrepareAssetCharacterSelection(characterName);
            selectCharacterStep.Retry = () => OnboardingValidation.PrepareAssetCharacterSelection(characterName);
            steps.Add(selectCharacterStep);

            steps.Add(Field(
                prefix + ".SelectType",
                "Select type: FBX No Animations",
                Md(
                    "### Keep the download lightweight",
                    "This field controls which asset type HoyoToon downloads.",
                    "",
                    "**Do this:** select **FBX No Animations**.",
                    "",
                    "This will prevent you downloading a 30mb model with animations. Trust me you'll be here for a while.."),
                new[] { "Assets.ModelTypeDropdown" },
                new[] { "Assets.ModelTypeDropdown" },
                "FBX No Animations",
                () => OnboardingValidation.IsNoAnimationsSelectedAfterInteraction(characterName),
                "Select No Anims."));

            steps.Add(Field(
                prefix + ".SelectVariant",
                "Select variant: Default",
                Md(
                    "### Use the base variant",
                    "Some characters include multiple variants.",
                    "",
                    "**Do this:** select **Default** so the tutorial uses the expected base model."),
                new[] { "Assets.VariantDropdown" },
                new[] { "Assets.VariantDropdown" },
                "Default",
                () => OnboardingValidation.IsVariantSelectedAfterInteraction(characterName, "Default"),
                "Select the Default variant."));

            OnboardingStep disableAutoSetupStep = Toggle(
                prefix + ".DisableAutoSetup",
                "Disable Auto Setup",
                Md(
                    "### Separate download from setup",
                    "**Auto Setup** can process a model immediately after download.",
                    "",
                    "For the onboarding you will not get a wheelchair. **You will do something!**, anywho remember this toggle for the future.",
                    "",
                    "**Do this:** turn **Auto Setup** off before downloading."),
                new[] { "Assets.AutoSetupToggle" },
                new[] { "Assets.AutoSetupToggle" },
                "Off",
                OnboardingValidation.IsAutoSetupDisabledAfterInteraction,
                "Turn Auto Setup off.");
            disableAutoSetupStep.OnEnter = OnboardingValidation.PrepareAutoSetupDisableStep;
            disableAutoSetupStep.Retry = OnboardingValidation.PrepareAutoSetupDisableStep;
            steps.Add(disableAutoSetupStep);

            steps.Add(ActionStep(
                prefix + ".Download",
                "Download " + characterName,
                Md(
                    "### Download " + characterName,
                    "The selection is ready.",
                    "",
                    "**Do this:** click **Download Selected Assets** and wait for completion.",
                    "",
                    "The tutorial will continue once the selected model is available locally."),
                new[] { "Assets.DownloadButton" },
                new[] { "Assets.DownloadButton" },
                () => OnboardingValidation.HasDownloadedCharacter(characterName),
                () => OnboardingValidation.GetDownloadStatus(characterName),
                OnboardingValidation.BeginDownloadStep,
                OnboardingValidation.RetryDownload,
                "Click Download Selected Assets."));
        }

        private static string Md(params string[] lines)
        {
            return string.Join("\n", lines);
        }

        private static OnboardingStep Dialog(string id, string title, string text, string target, string hint)
        {
            return Dialog(id, title, text, new[] { target }, new[] { "Dialog.ContinueButton" }, hint);
        }

        private static OnboardingStep Dialog(string id, string title, string text, string[] highlights, string[] allowed, string hint)
        {
            OnboardingStep step = Base(id, title, text, OnboardingStepType.Dialog, highlights, allowed, hint);
            step.ShowContinueButton = true;
            step.RequireCompletionBeforeContinue = false;
            step.IsComplete = () => true;
            return step;
        }

        private static OnboardingStep Confirm(string id, string title, string text, string[] highlights, string hint)
        {
            OnboardingStep step = Base(id, title, text, OnboardingStepType.Confirm, highlights, new[] { "Dialog.DockConfirmButton" }, hint);
            step.ShowContinueButton = true;
            step.RequireCompletionBeforeContinue = false;
            step.IsComplete = () => true;
            return step;
        }

        private static OnboardingStep Click(string id, string title, string text, string target, string hint, System.Func<bool> isComplete)
        {
            OnboardingStep step = Base(id, title, text, OnboardingStepType.Click, new[] { target }, new[] { target }, hint);
            step.IsComplete = isComplete;
            step.CorrectionText = "Use the highlighted control first.";
            return step;
        }

        private static OnboardingStep ModuleClick(string id, string title, string text, string target, string moduleId)
        {
            OnboardingStep step = Click(id, title, text, target, "Click this tab.", () => OnboardingValidation.IsActiveModule(moduleId));
            step.OnEnter = OnboardingValidation.PrepareManagerNavigationForOnboarding;
            step.Retry = OnboardingValidation.PrepareManagerNavigationForOnboarding;
            return step;
        }

        private static OnboardingStep Field(
            string id,
            string title,
            string text,
            string[] highlights,
            string[] allowed,
            string requiredValue,
            System.Func<bool> isComplete,
            string hint)
        {
            OnboardingStep step = Base(id, title, text, OnboardingStepType.FieldSelection, highlights, allowed, hint);
            step.RequiredValue = requiredValue;
            step.IsComplete = isComplete;
            step.CorrectionText = "For this tutorial, choose " + requiredValue + " so the next steps match the guided setup.";
            return step;
        }

        private static OnboardingStep Toggle(
            string id,
            string title,
            string text,
            string[] highlights,
            string[] allowed,
            string requiredValue,
            System.Func<bool> isComplete,
            string hint)
        {
            OnboardingStep step = Base(id, title, text, OnboardingStepType.Toggle, highlights, allowed, hint);
            step.RequiredValue = requiredValue;
            step.IsComplete = isComplete;
            step.CorrectionText = "Set the highlighted toggle to " + requiredValue + ".";
            return step;
        }

        private static OnboardingStep Multi(
            string id,
            string title,
            string text,
            string[] highlights,
            string[] allowed,
            System.Func<bool> isComplete,
            string hint)
        {
            OnboardingStep step = Base(id, title, text, OnboardingStepType.MultiAction, highlights, allowed, hint);
            step.IsComplete = isComplete;
            step.CorrectionText = "Use the highlighted control until the requested change has happened.";
            return step;
        }

        private static OnboardingStep ActionStep(
            string id,
            string title,
            string text,
            string[] highlights,
            string[] allowed,
            System.Func<bool> isComplete,
            System.Func<OnboardingAsyncStatus> status,
            System.Action onEnter,
            System.Action retry,
            string hint = null)
        {
            OnboardingStep step = Base(id, title, text, OnboardingStepType.Action, highlights, allowed, hint ?? "Run the highlighted action.");
            step.IsComplete = isComplete;
            step.GetAsyncStatus = status;
            step.OnEnter = onEnter;
            step.Retry = retry;
            step.HasFailed = () =>
            {
                OnboardingAsyncStatus asyncStatus = status != null ? status() : null;
                return asyncStatus != null && asyncStatus.HasFailed;
            };
            return step;
        }

        private static OnboardingStep Base(string id, string title, string text, OnboardingStepType type, string[] highlights, string[] allowed, string hint)
        {
            OnboardingStep step = new OnboardingStep
            {
                Id = id,
                Title = title,
                InstructionText = text,
                InlineHintText = hint,
                StepType = type,
                BlockAllOtherUI = true,
                AutoFocusTargets = true,
                ShowContinueButton = false,
                RequireCompletionBeforeContinue = true
            };

            if (highlights != null)
            {
                step.HighlightTargets.AddRange(highlights);
            }

            if (allowed != null)
            {
                step.AllowedTargets.AddRange(allowed);
            }

            return step;
        }
    }
}
#endif
