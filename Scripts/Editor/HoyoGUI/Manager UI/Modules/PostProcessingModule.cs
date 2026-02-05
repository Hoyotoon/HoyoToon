#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using HoyoToon.EditorTools.Onboarding;

namespace HoyoToon.EditorTools.ManagerUI.Modules
{
	internal sealed class PostProcessingModule : HoyoToonManagerModule
	{
		public override string DisplayName => "Post Processing";

		private const string ResourceFolderMarker = "/Resources/Post Processing/";
		private static readonly string[] CustomProfileTokens = { "Custom Post Processing", "Custom Profile", "Custom" };
		private const string TourProfileName = "Genshin Impact";

		private readonly List<PostProcessProfile> _resourceProfiles = new List<PostProcessProfile>();
		private readonly List<string> _resourceProfileNames = new List<string>();
		private Vector2 _profileScroll;
		private bool _resourceProfilesDirty = true;
		private bool _disposed;
		private bool _showProfileSelection = true;
		private bool _showProfileOverrides = true;

		private PostProcessProfile _referenceProfile;
		private Editor _profileEditor;
		private int _resourceProfileIndex = -1;
		private PostProcessVolume _lastBoundVolume;
		private VolumeBindingState _bindingState = VolumeBindingState.Unknown;

		private enum VolumeBindingState
		{
			Unknown,
			MissingCamera,
			MissingVolume,
			Bound
		}

		public override void OnGUI(HoyoToonManager targetManager)
		{
			_showProfileSelection = DrawFoldoutSection("Profile Selection", _showProfileSelection, () =>
			{
				DrawProfileSelectionBlock(targetManager);
			});
			ApplyProfileToScene(targetManager);

			if (_referenceProfile != null && IsCustomProfileSelected())
			{
				EditorGUILayout.Space(8f);
				_showProfileOverrides = DrawFoldoutSection("Profile Overrides", _showProfileOverrides, DrawProfileInspector);
			}
		}

		private void DrawProfileSelectionBlock(HoyoToonManager targetManager)
		{
			EnsureResourceProfiles();
			SyncSelectionFromScene(targetManager);
			EnsureTourProfileSelection();
			DrawResourceProfileDropdown();

			if (_referenceProfile == null)
			{
				EditorGUILayout.HelpBox("Select a profile from Resources/Post Processing to continue.", MessageType.Info);
				return;
			}

			DrawSelectionDetails();
		}

		private void SyncSelectionFromScene(HoyoToonManager manager)
		{
			if (_resourceProfiles.Count == 0)
			{
				return;
			}

			var primaryCamera = ResolvePrimaryCamera(manager);
			if (primaryCamera == null)
			{
				return;
			}

			var volume = ResolvePrimaryVolume(manager, primaryCamera);
			if (volume == null)
			{
				return;
			}

			var profile = volume.sharedProfile;
			if (profile == null)
			{
				return;
			}

			int index = _resourceProfiles.IndexOf(profile);
			if (index < 0)
			{
				return;
			}

			if (index != _resourceProfileIndex || _referenceProfile != profile)
			{
				SelectResourceProfileByIndex(index);
			}
		}

		private void DrawResourceProfileDropdown()
		{
			EditorGUILayout.Space(2f);
			if (_resourceProfiles.Count == 0)
			{
				EditorGUILayout.HelpBox("No PostProcessProfile assets were found under Resources/Post Processing. Add profiles there or refresh once they exist.", MessageType.Info);
				if (GUILayout.Button("Refresh Resources Folder"))
				{
					ForceRefreshResourceProfiles();
				}

				EditorGUILayout.Space(4f);
				return;
			}

			DrawInlineCalloutIfNeeded("postprocessing_profile",
				"Click the Profile dropdown and select Honkai Star Rail.\n\nThis profile matches the tutorial look.");
			using (new EditorGUILayout.HorizontalScope())
			{
				var names = _resourceProfileNames.ToArray();
				var currentIndex = Mathf.Clamp(_resourceProfileIndex, 0, _resourceProfiles.Count - 1);
				var newIndex = EditorGUILayout.Popup(currentIndex, names);
				if (newIndex != _resourceProfileIndex)
				{
					SelectResourceProfileByIndex(newIndex);
					if (string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "postprocessing_profile", StringComparison.OrdinalIgnoreCase))
					{
						HoyoToonGuidedTourController.NotifyPostProcessingProfilePicked();
					}
				}
				var profileRect = GUILayoutUtility.GetLastRect();
				HoyoToonTourOverlay.DrawHighlightIfActive("tour.postprocessing.profile", profileRect, "Profile", onClick: () =>
				{
					if (string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "postprocessing_profile", StringComparison.OrdinalIgnoreCase))
					{
						HoyoToonGuidedTourController.NotifyPostProcessingProfilePicked();
					}
				});

				if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
				{
					ForceRefreshResourceProfiles();
				}
			}

			if (_referenceProfile == null && _resourceProfiles.Count > 0)
			{
				SelectResourceProfileByIndex(Mathf.Clamp(_resourceProfileIndex, 0, _resourceProfiles.Count - 1));
			}

			EditorGUILayout.Space(6f);
		}

		private static void DrawInlineCalloutIfNeeded(string stepId, string body)
		{
			if (!HoyoToonGuidedTourController.IsActive)
			{
				return;
			}

			if (!string.Equals(HoyoToonGuidedTourController.CurrentStep.id, stepId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			HoyoToonTourCallout.Draw(
				$"Guided Tour: {HoyoToonGuidedTourController.CurrentStep.title}",
				body,
				null,
				null);
		}

		private void EnsureTourProfileSelection()
		{
			if (!HoyoToonGuidedTourController.IsActive)
			{
				return;
			}

			if (!string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "postprocessing_profile", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (_resourceProfiles.Count == 0)
			{
				return;
			}

			int desiredIndex = _resourceProfileNames.FindIndex(name =>
				name.IndexOf(TourProfileName, StringComparison.OrdinalIgnoreCase) >= 0);
			if (desiredIndex >= 0 && desiredIndex != _resourceProfileIndex)
			{
				SelectResourceProfileByIndex(desiredIndex);
			}
		}

		private void DrawSelectionDetails()
		{
			if (IsCustomProfileSelected())
			{
				EditorGUILayout.LabelField("Custom profile edits apply immediately.", EditorStyles.miniLabel);
				return;
			}

			EditorGUILayout.HelpBox("Packaged profiles are read-only. Select the Custom profile to tweak overrides.", MessageType.Info);
		}


		private void DrawProfileInspector()
		{
			if (!IsCustomProfileSelected())
			{
				EnsureProfileEditor(null);
				EditorGUILayout.HelpBox("Select the Custom profile to edit overrides.", MessageType.Info);
				return;
			}

			if (_bindingState == VolumeBindingState.MissingCamera)
			{
				EditorGUILayout.HelpBox("No camera was found in the scene. Add or enable a camera so post-processing previews can update.", MessageType.Warning);
			}
			else if (_bindingState == VolumeBindingState.MissingVolume)
			{
				EditorGUILayout.HelpBox("Unable to locate a PostProcessVolume near the main camera. Add a global volume so the selected profile is actually used.", MessageType.Warning);
			}

			var target = GetEditableTarget();
			if (target == null)
			{
				EnsureProfileEditor(null);
				EditorGUILayout.HelpBox("Custom profile missing or invalid.", MessageType.Info);
				return;
			}

			EnsureProfileEditor(target);
			if (_profileEditor == null)
			{
				EditorGUILayout.HelpBox("Unable to create an inspector for the selected profile.", MessageType.Error);
				return;
			}

			_profileScroll = EditorGUILayout.BeginScrollView(_profileScroll, GUILayout.MinHeight(220f));
			_profileEditor.OnInspectorGUI();
			EditorGUILayout.EndScrollView();
		}

		private PostProcessProfile GetEditableTarget()
		{
			if (_referenceProfile == null)
			{
				return null;
			}

			if (IsCustomProfileSelected())
			{
				return _referenceProfile;
			}

			return null;
		}

		private bool IsCustomProfileSelected()
		{
			return IsCustomProfile(_referenceProfile);
		}

		private static bool IsCustomProfile(PostProcessProfile profile)
		{
			if (profile == null)
			{
				return false;
			}

			var name = profile.name ?? string.Empty;
			if (ContainsCustomToken(name))
			{
				return true;
			}

			var path = AssetDatabase.GetAssetPath(profile);
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}

			var fileName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
			if (ContainsCustomToken(fileName))
			{
				return true;
			}

			var normalizedPath = path.Replace('\\', '/');
			return normalizedPath.IndexOf("/Resources/Post Processing/", StringComparison.OrdinalIgnoreCase) >= 0
				&& normalizedPath.IndexOf("/Custom", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool ContainsCustomToken(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}

			foreach (var token in CustomProfileTokens)
			{
				if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private void EnsureProfileEditor(PostProcessProfile target)
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

			_profileEditor = Editor.CreateEditor(target);
		}

		private void EnsureResourceProfiles()
		{
			if (_resourceProfilesDirty)
			{
				RefreshResourceProfiles();
			}
		}

		private void ForceRefreshResourceProfiles()
		{
			_resourceProfilesDirty = true;
			RefreshResourceProfiles();
		}

		private void RefreshResourceProfiles()
		{
			_resourceProfilesDirty = false;

			_resourceProfiles.Clear();
			_resourceProfileNames.Clear();

			var guids = AssetDatabase.FindAssets("t:PostProcessProfile");
			var discovered = new List<PostProcessProfile>();
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
				if (!path.Contains(ResourceFolderMarker, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
				if (!profile)
				{
					continue;
				}

				discovered.Add(profile);
			}

			var orderedProfiles = discovered.OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase).ToList();
			foreach (var profile in orderedProfiles.Where(p => !IsCustomProfile(p)))
			{
				_resourceProfiles.Add(profile);
				_resourceProfileNames.Add(profile.name);
			}

			foreach (var profile in orderedProfiles.Where(IsCustomProfile))
			{
				_resourceProfiles.Add(profile);
				_resourceProfileNames.Add(profile.name);
			}

			if (_resourceProfiles.Count == 0)
			{
				_resourceProfileIndex = -1;
				AssignReferenceProfile(null);
				return;
			}

			if (_resourceProfileIndex < 0 || _resourceProfileIndex >= _resourceProfiles.Count)
			{
				_resourceProfileIndex = 0;
			}

			AssignReferenceProfile(_resourceProfiles[_resourceProfileIndex]);
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

		private void AssignReferenceProfile(PostProcessProfile profile)
		{
			if (_referenceProfile == profile)
			{
				return;
			}

			_referenceProfile = profile;
			if (_profileEditor != null)
			{
				UnityEngine.Object.DestroyImmediate(_profileEditor);
				_profileEditor = null;
			}
		}

		private void ApplyProfileToScene(HoyoToonManager manager)
		{
			var profile = _referenceProfile;
			if (manager == null || profile == null)
			{
				_bindingState = VolumeBindingState.Unknown;
				return;
			}

			var primaryCamera = ResolvePrimaryCamera(manager);
			if (primaryCamera == null)
			{
				_bindingState = VolumeBindingState.MissingCamera;
				return;
			}

			var volume = ResolvePrimaryVolume(manager, primaryCamera);
			if (volume == null)
			{
				_bindingState = VolumeBindingState.MissingVolume;
				return;
			}

			_bindingState = VolumeBindingState.Bound;

			if (volume.sharedProfile == profile)
			{
				_lastBoundVolume = volume;
				return;
			}

			Undo.RecordObject(volume, "Apply Post Processing Profile");
			volume.sharedProfile = profile;
			if (!volume.isGlobal)
			{
				volume.isGlobal = true;
			}
			EditorUtility.SetDirty(volume);

			_lastBoundVolume = volume;
		}

		private static Camera ResolvePrimaryCamera(HoyoToonManager manager)
		{
			if (manager != null)
			{
				var managerCamera = manager.GetComponentInChildren<Camera>(true);
				if (managerCamera != null)
				{
					return managerCamera;
				}

				var activeModel = manager.ActiveModel;
				if (activeModel != null)
				{
					var modelCamera = activeModel.GetComponentInChildren<Camera>(true);
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

			var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
			return cameras.Length > 0 ? cameras[0] : null;
		}

		private PostProcessVolume ResolvePrimaryVolume(HoyoToonManager manager, Camera primaryCamera)
		{
			if (_lastBoundVolume != null)
			{
				return _lastBoundVolume;
			}

			if (manager != null)
			{
				var managerVolume = manager.GetComponentInChildren<PostProcessVolume>(true);
				if (managerVolume != null)
				{
					return managerVolume;
				}
			}

			if (primaryCamera != null)
			{
				var cameraVolume = primaryCamera.GetComponent<PostProcessVolume>();
				if (cameraVolume != null)
				{
					return cameraVolume;
				}

				var childVolume = primaryCamera.GetComponentInChildren<PostProcessVolume>(true);
				if (childVolume != null)
				{
					return childVolume;
				}
			}

			var allVolumes = UnityEngine.Object.FindObjectsOfType<PostProcessVolume>();
			return allVolumes.Length > 0 ? allVolumes[0] : null;
		}

		public override void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			if (_profileEditor != null)
			{
				UnityEngine.Object.DestroyImmediate(_profileEditor);
				_profileEditor = null;
			}
		}
	}
}
#endif
