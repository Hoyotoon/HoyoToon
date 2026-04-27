#if UNITY_EDITOR
using System;
using System.Globalization;
using HoyoToon.Editor.Utilities.API;
using HoyoToon.Editor.Utilities.Editor;
using UnityEditor;

namespace HoyoToon.Editor.Resources
{
    [InitializeOnLoad]
    public static class ResourceSyncAutoCheckService
    {
        private static readonly TimeSpan AutoCheckInterval = TimeSpan.FromMinutes(10);
        private const double AutoCheckHeartbeatSeconds = 60d;
        private const string AutoCheckEnabledKey = "HoyoToon.Resources.AutoCheckEnabled";
        private const string LastAutoCheckTicksKey = "HoyoToon.Resources.LastAutoCheckUtcTicks";

        private static bool startupReadyHandled;
        private static double nextAutoCheckHeartbeat;

        static ResourceSyncAutoCheckService()
        {
            EditorApplication.delayCall += TryHandleStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        public static bool AutoCheckEnabled
        {
            get => !EditorPrefs.HasKey(PrefsKey(AutoCheckEnabledKey)) || EditorPrefs.GetBool(PrefsKey(AutoCheckEnabledKey), true);
            set => EditorPrefs.SetBool(PrefsKey(AutoCheckEnabledKey), value);
        }

        private static void OnEditorUpdate()
        {
            TryHandleStartup();

            if (!startupReadyHandled || !AutoCheckEnabled)
            {
                return;
            }

            if (!HoyoToonApiSyncUtility.ShouldRunHeartbeat(ref nextAutoCheckHeartbeat, AutoCheckHeartbeatSeconds))
            {
                return;
            }

            TryRunAutomaticCheck(force: false);
        }

        private static void TryHandleStartup()
        {
            if (startupReadyHandled || !EditorReadinessUtility.IsReadyForEditorWork())
            {
                return;
            }

            startupReadyHandled = true;
            nextAutoCheckHeartbeat = EditorApplication.timeSinceStartup + AutoCheckHeartbeatSeconds;
            TryRunAutomaticCheck(force: false);
        }

        private static void TryRunAutomaticCheck(bool force)
        {
            if (ResourceSyncService.IsBusy())
            {
                return;
            }

            if (!force && !HoyoToonApiSyncUtility.IsRefreshDue(LastAutoCheckTicksKey, AutoCheckInterval))
            {
                return;
            }

            EditorPrefs.SetString(PrefsKey(LastAutoCheckTicksKey), DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            _ = ResourceSyncService.CheckAllGamesInBackgroundAsync();
        }

        private static string PrefsKey(string key)
        {
            return HoyoToonApiSyncUtility.PrefsKey(key);
        }
    }
}
#endif
