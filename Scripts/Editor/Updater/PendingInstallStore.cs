#if UNITY_EDITOR
using System;
using System.IO;
using Utf8Json;
using UnityEngine;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Updater
{
    internal static class PendingInstallStore
    {
        private static string PendingInstallPath => Path.Combine(Application.persistentDataPath, "HoyoToon_GitUpdater_PendingInstall.json");

        public static bool Exists()
        {
            return File.Exists(PendingInstallPath);
        }

        public static PendingInstallState Load()
        {
            try
            {
                if (!File.Exists(PendingInstallPath)) return null;

                var bytes = File.ReadAllBytes(PendingInstallPath);
                if (Api.Parser.TryParse<PendingInstallState>(bytes, out var state, out var _))
                    return state;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Updater.PendingInstall.Load", $"Failed to load pending install marker: {ex.Message}");
            }

            return null;
        }

        public static void Save(PendingInstallState state)
        {
            try
            {
                var bytes = JsonSerializer.PrettyPrintByteArray(JsonSerializer.Serialize(state));
                File.WriteAllBytes(PendingInstallPath, bytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save pending install marker: {ex.Message}", ex);
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(PendingInstallPath)) File.Delete(PendingInstallPath);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Updater.PendingInstall.Clear", $"Failed to clear pending install marker: {ex.Message}");
            }
        }
    }
}
#endif