using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HoyoToon.Editor.Utilities.Editor;
using UnityEditor;

namespace HoyoToon.Editor.Utilities.API
{
    internal static class HoyoToonApiSyncUtility
    {
        internal static bool IsEditorReadyForRefresh()
        {
            return EditorReadinessUtility.IsReadyForEditorWork();
        }

        internal static bool ShouldRunHeartbeat(ref double nextHeartbeatTime, double intervalSeconds)
        {
            if (EditorApplication.timeSinceStartup < nextHeartbeatTime)
            {
                return false;
            }

            nextHeartbeatTime = EditorApplication.timeSinceStartup + intervalSeconds;
            return true;
        }

        internal static bool IsRefreshDue(string lastCheckTicksKey, TimeSpan pollInterval)
        {
            string rawTicks = EditorPrefs.GetString(PrefsKey(lastCheckTicksKey), "0");
            if (!long.TryParse(rawTicks, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lastTicks)
                || lastTicks <= 0)
            {
                return true;
            }

            DateTime lastCheck = new DateTime(lastTicks, DateTimeKind.Utc);
            return DateTime.UtcNow - lastCheck >= pollInterval;
        }

        internal static void SaveLastCheckMetadata(
            string lastCheckTicksKey,
            string lastPayloadHashKey,
            string lastSchemaVersionKey,
            string schemaVersion,
            string payloadHash)
        {
            EditorPrefs.SetString(PrefsKey(lastCheckTicksKey), DateTime.UtcNow.Ticks.ToString());
            EditorPrefs.SetString(PrefsKey(lastPayloadHashKey), payloadHash ?? string.Empty);
            EditorPrefs.SetString(PrefsKey(lastSchemaVersionKey), schemaVersion);
        }

        internal static string GetSavedPayloadHash(string lastPayloadHashKey)
        {
            return EditorPrefs.GetString(PrefsKey(lastPayloadHashKey), string.Empty);
        }

        internal static string GetSavedSchemaVersion(string lastSchemaVersionKey)
        {
            return EditorPrefs.GetString(PrefsKey(lastSchemaVersionKey), string.Empty);
        }

        internal static string ComputeSha256(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        internal static string PrefsKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
        }
    }
}
