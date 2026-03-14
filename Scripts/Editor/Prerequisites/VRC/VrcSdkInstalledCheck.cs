#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Prerequisites
{
    public sealed class VRCSDKInstalledCheck : IPrerequisiteCheck
    {
        public string Name => "VRChat SDK presence";

        public enum VrcSdkKind
        {
            None = 0,
            SDK2 = 1,
            SDK3Avatars = 2,
            SDK3Worlds = 3,
            Unknown = 4,
        }

        private static bool s_cached;
        private static bool s_cachedInit;
        private static string s_cachedReason;
        private static VrcSdkKind s_cachedKind;
        private static bool s_cachedHasEnvConfig;

        private static bool? s_isVrcOverride;

        private static bool? s_debugForceIsVrc;

        public static bool IsVRC
        {
            get
            {
                EnsureCached();
                if (s_debugForceIsVrc.HasValue)
                    return s_debugForceIsVrc.Value;
                // IsVRC cannot be true when the SDK is not installed.
                return s_cached && (s_isVrcOverride ?? true);
            }
            set
            {
                // Setting true clears the override and returns to auto-detection.
                s_isVrcOverride = value ? (bool?)null : false;
            }
        }

        public static void ClearIsVRCOverride() => s_isVrcOverride = null;

        public static void SetDebugForceIsVRC(bool? force)
        {
            s_debugForceIsVrc = force;
        }

        public static bool IsVRCSDKInstalled
        {
            get
            {
                EnsureCached();
                return s_cached;
            }
        }

        public static VrcSdkKind InstalledKind
        {
            get
            {
                EnsureCached();
                return s_cachedKind;
            }
        }

        public static bool IsAvatarsSdk => InstalledKind == VrcSdkKind.SDK3Avatars;
        public static bool IsWorldsSdk => InstalledKind == VrcSdkKind.SDK3Worlds;
        public static bool IsSdk2 => InstalledKind == VrcSdkKind.SDK2;

        public static bool HasEnvConfig
        {
            get
            {
                EnsureCached();
                return s_cachedHasEnvConfig;
            }
        }

        public static string FriendlySdkName
        {
            get
            {
                switch (InstalledKind)
                {
                    case VrcSdkKind.SDK3Avatars: return "VRChat SDK3 (Avatars)";
                    case VrcSdkKind.SDK3Worlds: return "VRChat SDK3 (Worlds)";
                    case VrcSdkKind.SDK2: return "VRChat SDK2";
                    case VrcSdkKind.Unknown: return "VRChat SDK (Unknown)";
                    default: return "VRChat SDK";
                }
            }
        }

        public static string FriendlySdkNameOrNotInstalled => IsVRCSDKInstalled ? FriendlySdkName : "VRChat SDK (Not Installed)";

        public static string DetectionReason
        {
            get
            {
                EnsureCached();
                return s_cachedReason ?? string.Empty;
            }
        }

        private static void EnsureCached()
        {
            if (s_cachedInit) return;
            s_cachedInit = true;

            try
            {
                s_cached = VrcSdkDetector.Detect(out s_cachedKind, out s_cachedHasEnvConfig, out s_cachedReason);
            }
            catch (Exception)
            {
                s_cached = false;
                s_cachedKind = VrcSdkKind.None;
                s_cachedHasEnvConfig = false;
                s_cachedReason = "Detection failed (exception). Treating as not installed.";
            }
        }


        public PrerequisiteResult Evaluate()
        {
            EnsureCached();

            var isOverriddenOff = s_isVrcOverride.HasValue && s_isVrcOverride.Value == false;
            var debugForced = s_debugForceIsVrc.HasValue;
            var isVrcModeSuffix = debugForced
                ? $"IsVRC: {IsVRC.ToString().ToLowerInvariant()} (debug-forced)"
                : (isOverriddenOff
                    ? "IsVRC: false (user-disabled)"
                    : $"IsVRC: {IsVRC.ToString().ToLowerInvariant()}");

            if (s_cached)
            {
                var msg = string.IsNullOrEmpty(s_cachedReason)
                    ? $"{FriendlySdkName} detected"
                    : $"{FriendlySdkName} detected: {s_cachedReason}";
                var fullMsg = $"{msg}. {isVrcModeSuffix}.";
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, fullMsg);
                return PrerequisiteResult.Ok(fullMsg);
            }

            var notInstalledMsg = $"VRChat SDK not detected. VRChat-specific features will be disabled. IsVRC: false.";
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, notInstalledMsg);
            return PrerequisiteResult.Ok(notInstalledMsg);
        }

        public bool TryFix()
        {
            return false;
        }
    }
}
#endif
