#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HoyoToon;
using HoyoToon.Editor.Onboarding;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class PostProcessingModule : ManagerModule
    {
        private enum VolumeBindingState
        {
            Unknown,
            MissingCamera,
            MissingVolume,
            Bound
        }

        private readonly struct ProfileEntry
        {
            public ProfileEntry(VolumeProfile profile, bool isCustom)
            {
                Profile = profile;
                IsCustom = isCustom;
            }

            public VolumeProfile Profile { get; }

            public bool IsCustom { get; }
        }

        private const string ResourceFolderMarker = "/Resources/Post Processing/";
        private const float ProfileInspectorMinHeight = 220f;
        private static readonly string[] CustomProfileTokens = { "Custom Post Processing", "Custom Profile", "Custom" };
        private const string TutorialStartProfileName = GameConstants.GenshinImpact;
        private const string TutorialTargetProfileName = GameConstants.HonkaiStarRail;

        private readonly List<VolumeProfile> _resourceProfiles = new List<VolumeProfile>();
        private readonly List<string> _resourceProfileNames = new List<string>();

        private UnityEditor.Editor _profileEditor;
        private Vector2 _profileScroll;
        private bool _showProfileSelection = true;
        private bool _showProfileInspector = true;
        private bool _resourceProfilesDirty = true;
        private bool _disposed;
        private int _resourceProfileIndex = -1;
        private VolumeProfile _referenceProfile;
        private Volume _lastBoundVolume;
        private VolumeBindingState _bindingState = VolumeBindingState.Unknown;

        public override string DisplayName => "Post Processing";

        internal override string NavbarTourTarget => "tour.modules.postprocessing";

        internal override string NavbarTourLabel => "Post FX";

        public override void OnGUI(HoyoToonManager targetManager)
        {
            if (targetManager == null)
            {
                EditorGUILayout.HelpBox("Assign a HoyoToon Manager to edit post-processing profiles.", MessageType.Info);
                return;
            }

            EnsureResourceProfiles(targetManager);
            PrepareTourProfileIfNeeded(targetManager);
            GuidedTourController.NotifyPostProcessingProfileSelected(GetSelectedProfileName());
            ApplyProfileToScene(targetManager);

            _showProfileSelection = DrawFoldoutSection("Profile Selection", _showProfileSelection, () =>
            {
                DrawProfileSelectionBlock(targetManager);
            });

            if (IsCustomProfileSelected())
            {
                EditorGUILayout.Space(4f);
                _showProfileInspector = DrawFoldoutSection("Profile Overrides", _showProfileInspector, DrawProfileInspector);
            }
            else
            {
                EnsureProfileEditor(null);
            }
        }

        private void DrawProfileSelectionBlock(HoyoToonManager targetManager)
        {
            DrawInlineCalloutIfNeeded(
                StepIds.PostProcessingProfile,
                "The profile is currently set to Genshin Impact. Change it to Honkai Star Rail to continue.");

            if (_resourceProfiles.Count == 0)
            {
                EditorGUILayout.HelpBox("No URP volume profiles were found under Resources/Post Processing.", MessageType.Warning);
                if (GUILayout.Button("Refresh Profiles"))
                {
                    ForceRefreshResourceProfiles(targetManager);
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                int nextIndex = EditorGUILayout.Popup("Profile", _resourceProfileIndex, _resourceProfileNames.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    SelectResourceProfileByIndex(nextIndex);
                    GuidedTourController.NotifyPostProcessingProfileSelected(GetSelectedProfileName());
                    if (string.Equals(GetSelectedProfileName(), TutorialTargetProfileName, StringComparison.OrdinalIgnoreCase))
                    {
                        GuidedTourController.NotifyPostProcessingProfilePicked();
                    }
                    ApplyProfileToScene(targetManager);
                }

                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(64f)))
                {
                    ForceRefreshResourceProfiles(targetManager);
                }
            }

            if (IsCustomProfileSelected())
            {
                EditorGUILayout.HelpBox("Changes are written back to the Custom volume profile asset.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Packaged profiles are read-only. Select the Custom profile to tweak overrides.", MessageType.Info);
            }

            DrawBindingStateHelp();
        }

        private void DrawProfileInspector()
        {
            if (!IsCustomProfileSelected())
            {
                EnsureProfileEditor(null);
                EditorGUILayout.HelpBox("Select the Custom profile to edit overrides.", MessageType.Info);
                return;
            }

            DrawBindingStateHelp();

            VolumeProfile target = _referenceProfile;
            if (target == null)
            {
                EnsureProfileEditor(null);
                EditorGUILayout.HelpBox("Custom profile missing or invalid.", MessageType.Info);
                return;
            }

            EnsureProfileEditor(target);
            if (_profileEditor == null)
            {
                EditorGUILayout.HelpBox("Unable to create an inspector for the selected volume profile.", MessageType.Error);
                return;
            }

            _profileScroll = EditorGUILayout.BeginScrollView(_profileScroll, GUILayout.MinHeight(ProfileInspectorMinHeight));
            _profileEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void DrawBindingStateHelp()
        {
            if (_bindingState == VolumeBindingState.MissingCamera)
            {
                EditorGUILayout.HelpBox("No camera was found in the scene. Add or enable a camera so post-processing previews can update.", MessageType.Warning);
            }
            else if (_bindingState == VolumeBindingState.MissingVolume)
            {
                EditorGUILayout.HelpBox("Unable to locate a URP Volume near the main camera. Add a global volume so the selected profile is actually used.", MessageType.Warning);
            }
        }

        private bool IsCustomProfileSelected()
        {
            return IsCustomProfile(_referenceProfile);
        }

        private static bool IsCustomProfile(VolumeProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            string name = profile.name ?? string.Empty;
            if (ContainsCustomToken(name))
            {
                return true;
            }

            string path = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            if (ContainsCustomToken(fileName))
            {
                return true;
            }

            string normalizedPath = path.Replace('\\', '/');
            return normalizedPath.IndexOf(ResourceFolderMarker, StringComparison.OrdinalIgnoreCase) >= 0
                && normalizedPath.IndexOf("/Custom", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsCustomToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (string token in CustomProfileTokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureProfileEditor(VolumeProfile target)
        {
            if (target == null)
            {
                if (_profileEditor != null)
                {
                    UnityEngine.Object.DestroyImmediate(_profileEditor);
                    _profileEditor = null;
                }

                return;
            }

            if (_profileEditor != null && _profileEditor.target == target)
            {
                return;
            }

            if (_profileEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(_profileEditor);
            }

            _profileEditor = UnityEditor.Editor.CreateEditor(target);
        }

        private void EnsureResourceProfiles(HoyoToonManager manager)
        {
            if (_resourceProfilesDirty)
            {
                RefreshResourceProfiles(manager);
            }
        }

        private void ForceRefreshResourceProfiles(HoyoToonManager manager)
        {
            _resourceProfilesDirty = true;
            RefreshResourceProfiles(manager);
        }

        private void PrepareTourProfileIfNeeded(HoyoToonManager manager)
        {
            if (!GuidedTourController.IsActive
                || !string.Equals(GuidedTourController.CurrentStep.id, StepIds.PostProcessingProfile, StringComparison.OrdinalIgnoreCase)
                || GuidedTourController.IsPostProcessingPrepared())
            {
                return;
            }

            int tutorialIndex = ResolveProfileIndexByName(TutorialStartProfileName);
            if (tutorialIndex >= 0)
            {
                SelectResourceProfileByIndex(tutorialIndex);
                ApplyProfileToScene(manager);
            }

            GuidedTourController.MarkPostProcessingPrepared();
        }

        private void RefreshResourceProfiles(HoyoToonManager manager)
        {
            _resourceProfilesDirty = false;

            VolumeProfile previousProfile = _referenceProfile;
            VolumeProfile sceneProfile = ResolveSceneProfile(manager);

            _resourceProfiles.Clear();
            _resourceProfileNames.Clear();

            string[] guids = AssetDatabase.FindAssets("t:VolumeProfile");
            var sortedProfiles = new List<ProfileEntry>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (path.IndexOf(ResourceFolderMarker, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null)
                {
                    continue;
                }

                sortedProfiles.Add(new ProfileEntry(profile, IsCustomProfile(profile)));
            }

            sortedProfiles.Sort((a, b) =>
            {
                int customGroup = a.IsCustom.CompareTo(b.IsCustom);
                if (customGroup != 0)
                {
                    return customGroup;
                }

                return string.Compare(a.Profile.name, b.Profile.name, StringComparison.OrdinalIgnoreCase);
            });

            foreach (ProfileEntry entry in sortedProfiles)
            {
                _resourceProfiles.Add(entry.Profile);
                _resourceProfileNames.Add(entry.Profile.name);
            }

            if (_resourceProfiles.Count == 0)
            {
                _resourceProfileIndex = -1;
                AssignReferenceProfile(null);
                return;
            }

            int resolvedIndex = ResolveProfileIndex(previousProfile);
            if (resolvedIndex < 0)
            {
                resolvedIndex = ResolveProfileIndex(sceneProfile);
            }

            if (resolvedIndex < 0)
            {
                resolvedIndex = Mathf.Clamp(_resourceProfileIndex, 0, _resourceProfiles.Count - 1);
            }

            _resourceProfileIndex = resolvedIndex;
            AssignReferenceProfile(_resourceProfiles[_resourceProfileIndex]);
        }

        private int ResolveProfileIndex(VolumeProfile profile)
        {
            if (profile == null)
            {
                return -1;
            }

            for (int i = 0; i < _resourceProfiles.Count; i++)
            {
                if (_resourceProfiles[i] == profile)
                {
                    return i;
                }
            }

            return -1;
        }

        private int ResolveProfileIndexByName(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                return -1;
            }

            for (int i = 0; i < _resourceProfiles.Count; i++)
            {
                VolumeProfile profile = _resourceProfiles[i];
                if (profile == null)
                {
                    continue;
                }

                if (string.Equals(profile.name, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void SelectResourceProfileByIndex(int index)
        {
            if (_resourceProfiles.Count == 0)
            {
                _resourceProfileIndex = -1;
                AssignReferenceProfile(null);
                return;
            }

            _resourceProfileIndex = Mathf.Clamp(index, 0, _resourceProfiles.Count - 1);
            AssignReferenceProfile(_resourceProfiles[_resourceProfileIndex]);
        }

        private void AssignReferenceProfile(VolumeProfile profile)
        {
            if (_referenceProfile == profile)
            {
                return;
            }

            _referenceProfile = profile;
            EnsureProfileEditor(null);
        }

        private string GetSelectedProfileName()
        {
            return _referenceProfile != null ? _referenceProfile.name ?? string.Empty : string.Empty;
        }

        private void ApplyProfileToScene(HoyoToonManager manager)
        {
            VolumeProfile profile = _referenceProfile;
            if (manager == null || profile == null)
            {
                _bindingState = VolumeBindingState.Unknown;
                return;
            }

            Camera primaryCamera = ResolvePrimaryCamera(manager);
            if (primaryCamera == null)
            {
                _bindingState = VolumeBindingState.MissingCamera;
                return;
            }

            Volume volume = ResolvePrimaryVolume(manager, primaryCamera);
            if (volume == null)
            {
                _bindingState = VolumeBindingState.MissingVolume;
                return;
            }

            _bindingState = VolumeBindingState.Bound;

            bool needsProfile = volume.sharedProfile != profile;
            bool needsGlobal = !volume.isGlobal;
            if (!needsProfile && !needsGlobal)
            {
                _lastBoundVolume = volume;
                return;
            }

            Undo.RecordObject(volume, "Apply Post Processing Profile");
            volume.sharedProfile = profile;
            if (needsGlobal)
            {
                volume.isGlobal = true;
            }

            EditorUtility.SetDirty(volume);
            _lastBoundVolume = volume;
        }

        private Volume ResolveVolumeForDisplay(HoyoToonManager manager)
        {
            Camera primaryCamera = ResolvePrimaryCamera(manager);
            return ResolvePrimaryVolume(manager, primaryCamera);
        }

        private VolumeProfile ResolveSceneProfile(HoyoToonManager manager)
        {
            Volume volume = ResolveVolumeForDisplay(manager);
            return volume != null ? volume.sharedProfile : null;
        }

        private static Camera ResolvePrimaryCamera(HoyoToonManager manager)
        {
            if (manager != null)
            {
                Camera managerCamera = manager.GetComponentInChildren<Camera>(true);
                if (managerCamera != null)
                {
                    return managerCamera;
                }

                GameObject activeModel = manager.ActiveModel;
                if (activeModel != null)
                {
                    Camera modelCamera = activeModel.GetComponentInChildren<Camera>(true);
                    if (modelCamera != null)
                    {
                        return modelCamera;
                    }
                }
            }

            if (Camera.main != null)
            {
                return Camera.main;
            }

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return cameras.Length > 0 ? cameras[0] : null;
        }

        private Volume ResolvePrimaryVolume(HoyoToonManager manager, Camera primaryCamera)
        {
            if (_lastBoundVolume != null)
            {
                return _lastBoundVolume;
            }

            if (manager != null)
            {
                Volume managerVolume = manager.GetComponentInChildren<Volume>(true);
                if (managerVolume != null)
                {
                    return managerVolume;
                }
            }

            if (primaryCamera != null)
            {
                Volume cameraVolume = primaryCamera.GetComponent<Volume>();
                if (cameraVolume != null)
                {
                    return cameraVolume;
                }

                Volume childVolume = primaryCamera.GetComponentInChildren<Volume>(true);
                if (childVolume != null)
                {
                    return childVolume;
                }
            }

            Volume[] allVolumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return allVolumes.Length > 0 ? allVolumes[0] : null;
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EnsureProfileEditor(null);
        }
    }
}
#endif
