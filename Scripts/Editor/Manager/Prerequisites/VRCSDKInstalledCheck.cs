#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Prerequisites
{
    /// <summary>
    /// Detects whether the VRChat SDK is present.
    ///
    /// This is informational and enables global feature gating; it is not a hard prerequisite.
    ///
    /// NOTE:
    /// - Does NOT require asmdef Version Defines.
    /// - Does NOT reference VRChat types directly.
    /// - Works with both VPM/UPM installs (assemblies present) and many legacy installs (Assets/VRCSDK).
    /// </summary>
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

        // -----------------------------------------------------------------
        // Global API: use this anywhere to gate VRChat-specific features.
        // -----------------------------------------------------------------
        private static bool s_cached;
        private static bool s_cachedInit;
        private static string s_cachedReason;
        private static VrcSdkKind s_cachedKind;
        private static bool s_cachedHasEnvConfig;

        // Optional runtime override.
        // - null  => follow auto-detection (SDK installed => true, else false)
        // - false => user-forced OFF (even if SDK is installed)
        // This is intentionally NOT persisted (no EditorPrefs) to keep the system global and dependency-free.
        private static bool? s_isVrcOverride;

        // Debug-only force override (ignores actual SDK presence). Intended for testing.
        private static bool? s_debugForceIsVrc;

        /// <summary>
        /// Global feature toggle for VRChat-specific behavior.
        /// Defaults to auto-detection, but can be overridden in-memory.
        ///
        /// Design note: this bool should remain accurate.
        /// It cannot be forced ON when the SDK is not installed.
        /// Setting it to true clears the override (returns to auto-detection).
        /// </summary>
        public static bool IsVRC
        {
            get
            {
                EnsureCached();
                if (s_debugForceIsVrc.HasValue)
                    return s_debugForceIsVrc.Value;
                // Accurate by construction: if the SDK isn't installed, IsVRC must be false.
                return s_cached && (s_isVrcOverride ?? true);
            }
            set
            {
                // Only meaningful override is OFF.
                // If user sets true, return to auto-detection.
                s_isVrcOverride = value ? (bool?)null : false;
            }
        }

        /// <summary>
        /// Clears the in-memory override so IsVRC returns to auto-detection.
        /// </summary>
        public static void ClearIsVRCOverride() => s_isVrcOverride = null;

        /// <summary>
        /// Debug-only: forces IsVRC on/off regardless of SDK presence. Pass null to clear.
        /// </summary>
        public static void SetDebugForceIsVRC(bool? force)
        {
            s_debugForceIsVrc = force;
        }

        /// <summary>
        /// True if a VRChat SDK appears to be installed.
        /// Cached after first evaluation.
        /// </summary>
        public static bool IsVRCSDKInstalled
        {
            get
            {
                EnsureCached();
                return s_cached;
            }
        }

        /// <summary>
        /// Best-effort classification of the installed VRChat SDK.
        /// </summary>
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

        /// <summary>
        /// True if VRChat's EnvConfig type is present (VRChat SDK may manage quality/project settings).
        /// </summary>
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

        /// <summary>
        /// Friendly SDK label, but never returns "Unknown" when the SDK is not installed.
        /// </summary>
        public static string FriendlySdkNameOrNotInstalled => IsVRCSDKInstalled ? FriendlySdkName : "VRChat SDK (Not Installed)";

        /// <summary>
        /// Human-readable explanation of what was detected (best-effort).
        /// </summary>
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
                s_cached = DetectVrcSdk(out s_cachedKind, out s_cachedHasEnvConfig, out s_cachedReason);
            }
            catch (Exception)
            {
                s_cached = false;
                s_cachedKind = VrcSdkKind.None;
                s_cachedHasEnvConfig = false;
                s_cachedReason = "Detection failed (exception). Treating as not installed.";
            }
        }

        // -----------------------------------------------------------------

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
                HoyoToonLogger.ManagerInfo(fullMsg);
                return PrerequisiteResult.Ok(fullMsg);
            }

            var notInstalledMsg = $"VRChat SDK not detected. VRChat-specific features will be disabled. IsVRC: false.";
            HoyoToonLogger.ManagerInfo(notInstalledMsg);
            return PrerequisiteResult.Ok(notInstalledMsg);
        }

        public bool TryFix()
        {
            // Nothing to auto-fix here.
            return false;
        }

        // -----------------------------------------------------------------
        // Detection logic
        // -----------------------------------------------------------------
        private static bool DetectVrcSdk(out VrcSdkKind kind, out bool hasEnvConfig, out string reason)
        {
            kind = VrcSdkKind.None;
            hasEnvConfig = false;
            reason = null;

            // 1) Prefer UPM/VPM package detection via Packages/manifest.json and Packages/packages-lock.json.
            //    We intentionally do NOT trust scripting define symbols here, because Unity may keep them around
            //    after packages are removed, causing false positives.
            if (TryDetectViaPackages(out kind, out reason))
            {
                hasEnvConfig = FindLoadedType("VRC.Editor.EnvConfig");
                return true;
            }

            // 2) Reflection fallback: types can be found via reflection when assemblies are still present.
            hasEnvConfig = FindLoadedType("VRC.Editor.EnvConfig");

            // Check specific SDK3 types first for better classification.
            if (FindLoadedType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor"))
            {
                kind = VrcSdkKind.SDK3Avatars;
                reason = "SDK3 Avatars types present";
                return true;
            }
            if (FindLoadedType("VRC.Udon.UdonBehaviour") || FindLoadedType("VRC.Udon.UdonProgramSource"))
            {
                kind = VrcSdkKind.SDK3Worlds;
                reason = "SDK3 Worlds/Udon types present";
                return true;
            }

            // Common base type present in multiple SDK installs.
            if (FindLoadedType("VRC.SDKBase.VRC_SdkBuilder"))
            {
                kind = VrcSdkKind.Unknown;
                reason = "found type VRC.SDKBase.VRC_SdkBuilder";
                return true;
            }

            // Try to detect SDK2 by common legacy namespace/type.
            if (FindLoadedType("VRCSDK2.VRC_SdkBuilder"))
            {
                kind = VrcSdkKind.SDK2;
                reason = "SDK2 types present";
                return true;
            }

            // 3) Legacy installs: look for Assets/VRCSDK footprint.
            if (LooksLikeLegacyInstall(out kind, out reason))
                return true;

            kind = VrcSdkKind.None;
            hasEnvConfig = false;
            reason = "no VRChat SDK packages, types, or legacy footprint found";
            return false;
        }

        private static bool TryDetectViaPackages(out VrcSdkKind kind, out string reason)
        {
            kind = VrcSdkKind.None;
            reason = null;

            try
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

                // UPM/VPM dependency files
                var manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
                var lockPath = Path.Combine(projectRoot, "Packages", "packages-lock.json");

                string manifest = SafeReadAllText(manifestPath);
                string lockFile = SafeReadAllText(lockPath);

                bool HasPkg(string id)
                {
                    // Cheap/robust-enough: just search for the package id token.
                    // manifest.json contains dependencies; packages-lock.json contains resolved entries.
                    var token = "\"" + id + "\"";
                    return (!string.IsNullOrEmpty(manifest) && manifest.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                           || (!string.IsNullOrEmpty(lockFile) && lockFile.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                           || Directory.Exists(Path.Combine(projectRoot, "Packages", id)); // embedded/local packages
                }

                bool hasBase = HasPkg("com.vrchat.base");
                bool hasAvatars = HasPkg("com.vrchat.avatars");
                bool hasWorlds = HasPkg("com.vrchat.worlds");
                bool hasVpmResolver = HasPkg("com.vrchat.core.vpm-resolver");

                // vpm-resolver alone isn't the SDK.
                if (!hasBase && !hasAvatars && !hasWorlds)
                    return false;

                if (hasWorlds)
                    kind = VrcSdkKind.SDK3Worlds;
                else if (hasAvatars)
                    kind = VrcSdkKind.SDK3Avatars;
                else
                    kind = VrcSdkKind.Unknown;

                if (hasWorlds)
                    reason = "UPM/VPM packages indicate com.vrchat.worlds";
                else if (hasAvatars)
                    reason = "UPM/VPM packages indicate com.vrchat.avatars";
                else
                    reason = "UPM/VPM packages indicate com.vrchat.base";

                if (hasVpmResolver)
                    reason += " (+ com.vrchat.core.vpm-resolver)";

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string SafeReadAllText(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return null;
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }

        private static bool FindLoadedType(string fullName)
        {
            try
            {
                // Quick path
                if (Type.GetType(fullName) != null) return true;

                // Search loaded assemblies (safe, no hard dependency)
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.GetType(fullName, throwOnError: false) != null)
                            return true;
                    }
                    catch
                    {
                        // ignore type-load errors
                    }
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }

        private static bool LooksLikeLegacyInstall(out VrcSdkKind kind, out string reason)
        {
            kind = VrcSdkKind.Unknown;
            reason = null;
            try
            {
                // Assets/VRCSDK is the classic legacy root.
                var vrcsdkRoot = Path.Combine(Application.dataPath, "VRCSDK");
                if (!Directory.Exists(vrcsdkRoot))
                    return false;

                // Best-effort classification for legacy projects:
                // - If UdonSharp is present, it's almost certainly a Worlds project.
                // - Otherwise assume Avatars if the SDK is present.
                var udonSharpPath = Path.Combine(Application.dataPath, "UdonSharp");
                bool hasUdonSharp = Directory.Exists(udonSharpPath);
                kind = hasUdonSharp ? VrcSdkKind.SDK3Worlds : VrcSdkKind.SDK3Avatars;

                // A lot of SDKs include Assets/VRCSDK/version
                var versionFile = Path.Combine(vrcsdkRoot, "version");
                if (File.Exists(versionFile))
                {
                    var ver = SafeReadFirstLine(versionFile);
                    reason = string.IsNullOrEmpty(ver)
                        ? "legacy footprint Assets/VRCSDK + version file"
                        : $"legacy footprint Assets/VRCSDK (version {ver})";
                    return true;
                }

                // Still count as installed if the folder exists (some forks / partial installs)
                reason = "legacy footprint Assets/VRCSDK";
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string SafeReadFirstLine(string path)
        {
            try
            {
                using (var sr = new StreamReader(path))
                    return sr.ReadLine()?.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
