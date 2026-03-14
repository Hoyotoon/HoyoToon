#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace HoyoToon.Editor.Prerequisites
{
    internal static class VrcSdkDetector
    {
        public static bool Detect(out VRCSDKInstalledCheck.VrcSdkKind kind, out bool hasEnvConfig, out string reason)
        {
            kind = VRCSDKInstalledCheck.VrcSdkKind.None;
            hasEnvConfig = false;
            reason = null;

            // Prefer package detection and avoid stale define symbols.
            if (TryDetectViaPackages(out kind, out reason))
            {
                hasEnvConfig = FindLoadedType("VRC.Editor.EnvConfig");
                return true;
            }

            hasEnvConfig = FindLoadedType("VRC.Editor.EnvConfig");

            if (FindLoadedType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor"))
            {
                kind = VRCSDKInstalledCheck.VrcSdkKind.SDK3Avatars;
                reason = "SDK3 Avatars types present";
                return true;
            }

            if (FindLoadedType("VRC.Udon.UdonBehaviour") || FindLoadedType("VRC.Udon.UdonProgramSource"))
            {
                kind = VRCSDKInstalledCheck.VrcSdkKind.SDK3Worlds;
                reason = "SDK3 Worlds/Udon types present";
                return true;
            }

            if (FindLoadedType("VRC.SDKBase.VRC_SdkBuilder"))
            {
                kind = VRCSDKInstalledCheck.VrcSdkKind.Unknown;
                reason = "found type VRC.SDKBase.VRC_SdkBuilder";
                return true;
            }

            if (FindLoadedType("VRCSDK2.VRC_SdkBuilder"))
            {
                kind = VRCSDKInstalledCheck.VrcSdkKind.SDK2;
                reason = "SDK2 types present";
                return true;
            }

            if (LooksLikeLegacyInstall(out kind, out reason))
            {
                return true;
            }

            kind = VRCSDKInstalledCheck.VrcSdkKind.None;
            hasEnvConfig = false;
            reason = "no VRChat SDK packages, types, or legacy footprint found";
            return false;
        }

        private static bool TryDetectViaPackages(out VRCSDKInstalledCheck.VrcSdkKind kind, out string reason)
        {
            kind = VRCSDKInstalledCheck.VrcSdkKind.None;
            reason = null;

            try
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

                var manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
                var lockPath = Path.Combine(projectRoot, "Packages", "packages-lock.json");

                var manifest = SafeReadAllText(manifestPath);
                var lockFile = SafeReadAllText(lockPath);

                bool HasPkg(string id)
                {
                    var token = "\"" + id + "\"";
                    return (!string.IsNullOrEmpty(manifest) && manifest.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                           || (!string.IsNullOrEmpty(lockFile) && lockFile.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                           || Directory.Exists(Path.Combine(projectRoot, "Packages", id));
                }

                var hasBase = HasPkg("com.vrchat.base");
                var hasAvatars = HasPkg("com.vrchat.avatars");
                var hasWorlds = HasPkg("com.vrchat.worlds");
                var hasVpmResolver = HasPkg("com.vrchat.core.vpm-resolver");

                if (!hasBase && !hasAvatars && !hasWorlds)
                {
                    return false;
                }

                if (hasWorlds)
                {
                    kind = VRCSDKInstalledCheck.VrcSdkKind.SDK3Worlds;
                }
                else if (hasAvatars)
                {
                    kind = VRCSDKInstalledCheck.VrcSdkKind.SDK3Avatars;
                }
                else
                {
                    kind = VRCSDKInstalledCheck.VrcSdkKind.Unknown;
                }

                if (hasWorlds)
                {
                    reason = "UPM/VPM packages indicate com.vrchat.worlds";
                }
                else if (hasAvatars)
                {
                    reason = "UPM/VPM packages indicate com.vrchat.avatars";
                }
                else
                {
                    reason = "UPM/VPM packages indicate com.vrchat.base";
                }

                if (hasVpmResolver)
                {
                    reason += " (+ com.vrchat.core.vpm-resolver)";
                }

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
                {
                    return null;
                }

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
                if (Type.GetType(fullName) != null)
                {
                    return true;
                }

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.GetType(fullName, throwOnError: false) != null)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool LooksLikeLegacyInstall(out VRCSDKInstalledCheck.VrcSdkKind kind, out string reason)
        {
            kind = VRCSDKInstalledCheck.VrcSdkKind.Unknown;
            reason = null;

            try
            {
                var vrcsdkRoot = Path.Combine(Application.dataPath, "VRCSDK");
                if (!Directory.Exists(vrcsdkRoot))
                {
                    return false;
                }

                var udonSharpPath = Path.Combine(Application.dataPath, "UdonSharp");
                var hasUdonSharp = Directory.Exists(udonSharpPath);
                kind = hasUdonSharp ? VRCSDKInstalledCheck.VrcSdkKind.SDK3Worlds : VRCSDKInstalledCheck.VrcSdkKind.SDK3Avatars;

                var versionFile = Path.Combine(vrcsdkRoot, "version");
                if (File.Exists(versionFile))
                {
                    var ver = SafeReadFirstLine(versionFile);
                    reason = string.IsNullOrEmpty(ver)
                        ? "legacy footprint Assets/VRCSDK + version file"
                        : $"legacy footprint Assets/VRCSDK (version {ver})";
                    return true;
                }

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
                {
                    return sr.ReadLine()?.Trim();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
