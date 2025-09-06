#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Updater
{
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

        [MenuItem("HoyoToon/Updater/Optimized Git Updater", priority = 100)]
        public static void ShowWindow()
        {
            var w = GetWindow<OptimizedGitUpdaterWindow>(true, "HoyoToon Git Updater");
            w.minSize = new Vector2(520, 360);
            w.Show();
        }

        [MenuItem("HoyoToon/Updater/Switch Branch...", priority = 101)]
        public static void SwitchBranchShortcut()
        {
            var curr = BranchSelector.GetCurrentBranch();
            HoyoToonDialogWindow.ShowCustom(
                "Select Branch",
                $"Current branch: {curr}\n\nUse the menu: HoyoToon → Updater → Branch to switch between Stable (main) and Beta.",
                MessageType.Info,
                new[] { "Open Updater" },
                0, 0,
                _ => ShowWindow());
        }

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
            // Detect branch change while window is open and reload tracker
            var currentBranch = BranchSelector.GetCurrentBranch();
            if (_lastBranch != currentBranch)
            {
                _lastBranch = currentBranch;
                _tracker = PackageTrackerStore.Load();
                _batch = null; _remote = null; // reset any previous check data
            }
            EditorGUILayout.LabelField("HoyoToon Git Updater", EditorStyles.boldLabel);
            GUILayout.Space(6);

            DrawPackageInfo();
            GUILayout.Space(8);
            DrawControls();
            GUILayout.Space(8);
            DrawBatch();
            GUILayout.Space(8);
            DrawProgress();

            if (!string.IsNullOrEmpty(_status))
            {
                var type = (_status.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || _status.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? MessageType.Error : MessageType.Info;
                EditorGUILayout.HelpBox(_status, type);
            }
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

        private void DrawControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(_isChecking || _isUpdating);
                if (GUILayout.Button("Check for Updates", GUILayout.Height(28))) _ = CheckAsync();
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(_isChecking || _isUpdating || _batch == null || _batch.totalOperations == 0);
                if (GUILayout.Button("Apply Update", GUILayout.Height(28))) _ = ApplyAsync();
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Force Refresh", GUILayout.Height(28)))
                {
                    _tracker = new LocalPackageTracker();
                    PackageTrackerStore.Save(_tracker);
                    _ = CheckAsync();
                }
            }

            if (_isChecking)
            {
                EditorGUILayout.LabelField("Analyzing changes...");
                var r = EditorGUILayout.GetControlRect();
                EditorGUI.ProgressBar(r, 0.5f, "Checking...");
            }
        }

        private void DrawBatch()
        {
            if (_batch == null || _batch.totalOperations == 0) return;
            EditorGUILayout.LabelField($"Update Batch ({_batch.totalOperations} operations)", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(150));
            foreach (var f in _batch.fileUpdates)
            {
                var c = GUI.color; GUI.color = f.isNew ? Color.green : Color.yellow;
                EditorGUILayout.LabelField($"{(f.isNew ? "+" : "~")} {f.path}", EditorStyles.miniLabel);
                GUI.color = c;
            }
            foreach (var d in _batch.filesToDelete)
            {
                var c = GUI.color; GUI.color = Color.red;
                EditorGUILayout.LabelField($"- {d}", EditorStyles.miniLabel);
                GUI.color = c;
            }
            EditorGUILayout.EndScrollView();

            var summary = $"Changes: {_batch.fileUpdates.FindAll(u => u.isNew).Count} new, {_batch.fileUpdates.FindAll(u => !u.isNew).Count} modified, {_batch.filesToDelete.Count} deleted";
            EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(_changelog))
            {
                GUILayout.Space(6);
                EditorGUILayout.LabelField("Changelog", EditorStyles.boldLabel);
                var chRect = EditorGUILayout.GetControlRect(false, 100f);
                GUI.Box(chRect, "", EditorStyles.helpBox);
                var inner = new Rect(chRect.x + 6, chRect.y + 4, chRect.width - 12, chRect.height - 8);
                GUI.BeginGroup(inner);
                var style = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
                GUI.Label(new Rect(0, 0, inner.width, inner.height), _changelog, style);
                GUI.EndGroup();
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorGUILayout.HelpBox("Unity is compiling or updating assets. Wait before applying updates.", MessageType.Warning);
            }
        }

        private void DrawProgress()
        {
            if (!_isUpdating) return;
            EditorGUILayout.LabelField("Applying Updates...", EditorStyles.boldLabel);
            var r = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(r, _progress, $"{_progress * 100f:0}%");
            Repaint();
        }

        private async System.Threading.Tasks.Task CheckAsync()
        {
            _isChecking = true; _status = "Checking for updates..."; _batch = null; _remote = null;
            try
            {
                var res = await _controller.CheckAsync();
                _local = res.localPackage;
                _remote = res.remotePackage;
                _tracker = res.tracker;
                _batch = res.batch;
                _status = res.message;
                HoyoToonLogger.Always("Updater", _status, LogType.Log);

                _changelog = await FetchChangelogAsync(_remote);
            }
            catch (Exception ex)
            {
                _status = $"Error checking for updates: {ex.Message}";
                HoyoToonLogger.Always("Updater", _status, LogType.Error);
                HoyoToonDialogWindow.ShowError("Update Check Failed", _status);
            }
            finally { _isChecking = false; }
        }

        private async System.Threading.Tasks.Task ApplyAsync()
        {
            if (_batch == null || _batch.totalOperations == 0) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                HoyoToonDialogWindow.ShowInfo("Unity Busy", "Unity is compiling or updating assets. Please try again shortly.");
                return;
            }

            _isUpdating = true; _progress = 0f; _status = "Applying update...";
            try
            {
                int total = _batch.totalOperations; int completed = 0;
                // Progress will be driven by controller via progress bar, but update our own indicator periodically
                var progressTimer = new System.Threading.CancellationTokenSource();
                var token = progressTimer.Token;
                var tick = System.Threading.Tasks.Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        _progress = Mathf.Clamp01(completed / Mathf.Max(1f, total));
                        await System.Threading.Tasks.Task.Delay(100);
                    }
                }, token);

                // Wrap controller apply to update completed counter hooks by intercepting progress bar updates via EditorUtility
                // We'll approximate by incrementing after controller writes each file/deletion due to DisplayProgressBar calls already made there.
                await _controller.ApplyAsync(_batch, _remote);
                completed = total;
                progressTimer.Cancel(); await tick;

                _status = $"Update complete! Updated to version {_remote?.version}. package.json refreshed.";
                HoyoToonLogger.Always("Updater", _status, LogType.Log);
                HoyoToonDialogWindow.ShowInfo("Update Complete", _status);
                _batch = new UpdateBatch();
                _local = _controller.LoadLocalPackage();
                _changelog = null;
            }
            catch (Exception ex)
            {
                _status = $"Update failed: {ex.Message}";
                HoyoToonLogger.Always("Updater", _status, LogType.Error);
                HoyoToonDialogWindow.ShowError("Update Failed", _status + "\n\nSome files may have been partially updated.");
            }
            finally
            {
                _isUpdating = false; _progress = 0f;
            }
        }

        private async System.Threading.Tasks.Task<string> FetchChangelogAsync(PackageInfo remote)
        {
            if (remote == null || string.IsNullOrEmpty(remote.version)) return null;
            var branch = BranchSelector.GetCurrentBranch();
            using (var api = new GitHubApiClient(_settings.repoOwner, _settings.repoName, branch, _settings.githubToken))
            {
                try
                {
                    // Prefer GitHub Release matching the version (e.g., tag == version)
                    var rel = await api.GetReleaseByTagAsync(remote.version) ?? await api.GetReleaseByTagAsync("v" + remote.version);
                    if (rel != null && !string.IsNullOrEmpty(rel.body))
                    {
                        return $"Release Notes for {remote.version}\n\n" + rel.body;
                    }
                }
                catch { }

                // Fallback: try CHANGELOG.md at repo root (current branch)
                try
                {
                    var text = await api.GetRawTextAsync("changelog.md");
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Heuristic: show the first section for the version if present
                        var header = $"## {remote.version}";
                        int idx = text.IndexOf(header, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            int next = text.IndexOf("## ", idx + header.Length, StringComparison.OrdinalIgnoreCase);
                            string section = next > idx ? text.Substring(idx, next - idx) : text.Substring(idx);
                            return section.Trim();
                        }
                        // Otherwise show a truncated top of the changelog
                        return Truncate(text.Trim(), 2000);
                    }
                }
                catch { }

                // No changelog available
                return null;
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "\n...";
        }
    }
}
#endif
