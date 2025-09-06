#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Updater
{
    [Obsolete("Deprecated. Use HoyoToon/Updater/Run Updater... (UpdaterDialogFlow).")]
    public class OptimizedGitUpdaterWindow : EditorWindow
    {
        private UpdaterController _controller;
        private UpdaterSettings _settings;

        private PackageInfo _local;
        private PackageInfo _remote;
        private LocalPackageTracker _tracker;
        private UpdateBatch _batch;
    private string _changelog;

        private bool _isChecking;
        private bool _isUpdating;
        private float _progress;
        private string _status = string.Empty;
        private Vector2 _scroll;

        // Deprecated entry; forward to single-dialog flow if invoked.
        // No menu items; deprecated.

        // Switch Branch shortcut removed; use HoyoToon/Updater/Branch submenu instead.

        [InitializeOnLoadMethod]
        private static void InitCategoryColor()
        {
            // Ensure we have a distinct color for Updater category
            HoyoToonLogCore.SetCategoryColor("Updater", "#80FFE6");
        }

        private void OnEnable()
        {
            _settings = UpdaterSettings.FindOrCreate();
            _controller = new UpdaterController(_settings);
            _local = _controller.LoadLocalPackage();
            _tracker = PackageTrackerStore.Load();
            titleContent = new GUIContent("Git Updater", EditorGUIUtility.IconContent("d_CloudConnect").image);
            _lastBranch = BranchSelector.GetCurrentBranch();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("This window is deprecated. The new single-dialog updater is now used.", MessageType.Info);
            if (GUILayout.Button("Open New Updater", GUILayout.Height(28))) UpdaterDialogFlow.Run();
        }

        private void DrawPackageInfo()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                var branch = BranchSelector.GetCurrentBranch();
                if (string.Equals(branch, "Beta", StringComparison.OrdinalIgnoreCase))
                {
                    EditorGUILayout.HelpBox("Beta branch selected: experimental builds may be unstable and are not officially supported.", MessageType.Warning);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Local Version", GUILayout.Width(100));
                    EditorGUILayout.LabelField(_local?.version ?? "Unknown");
                    GUILayout.FlexibleSpace();
                    if (_remote != null)
                    {
                        EditorGUILayout.LabelField("Remote", GUILayout.Width(60));
                        EditorGUILayout.LabelField(_remote.version);
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Repo", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"{_settings.repoOwner}/{_settings.repoName}@{branch}");
                }
            }
        }

        private string _lastBranch;

        private void DrawControls() {}

        private void DrawBatch() {}

        private void DrawProgress() {}

        private async System.Threading.Tasks.Task CheckAsync() { await System.Threading.Tasks.Task.CompletedTask; }

        private async System.Threading.Tasks.Task ApplyAsync() { await System.Threading.Tasks.Task.CompletedTask; }

        private async System.Threading.Tasks.Task<string> FetchChangelogAsync(PackageInfo remote) { return null; }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "\n...";
        }
    }
}
#endif
