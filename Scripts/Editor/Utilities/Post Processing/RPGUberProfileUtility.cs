#if UNITY_EDITOR
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.Bloom;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.ChromaticAberration;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.RadialBlur;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.Uber;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoyoToon.Editor.Utilities.PostProcessing
{
    internal static class RPGUberProfileUtility
    {
        internal static bool TryAddMissingProfileComponents(
            RPGUber component,
            out VolumeProfile owningProfile,
            out bool changed,
            out string message)
        {
            changed = false;
            if (!TryFindOwningProfile(component, out owningProfile))
            {
                message = $"{nameof(RPGUber)}: could not resolve the owning VolumeProfile for this component.";
                return false;
            }

            changed |= AddIfMissing<RPGBloom>(owningProfile);
            changed |= AddIfMissing<RPGChromaticAberration>(owningProfile);
            changed |= AddIfMissing<RPGRadialBlur>(owningProfile);

            if (!changed)
            {
                message = $"{nameof(RPGUber)}: the profile already contains all RPG components.";
                return true;
            }

            EditorUtility.SetDirty(owningProfile);
            AssetDatabase.SaveAssetIfDirty(owningProfile);
            message = $"{nameof(RPGUber)}: added missing RPG components to '{owningProfile.name}'.";
            return true;
        }

        private static bool TryFindOwningProfile(RPGUber component, out VolumeProfile owningProfile)
        {
            owningProfile = null;
            if (component == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(component);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; ++i)
            {
                VolumeProfile profile = assets[i] as VolumeProfile;
                if (profile == null)
                {
                    continue;
                }

                if (!profile.components.Contains(component))
                {
                    continue;
                }

                owningProfile = profile;
                return true;
            }

            return false;
        }

        private static bool AddIfMissing<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile == null || profile.TryGet(out T _))
            {
                return false;
            }

            profile.Add<T>(overrides: false);
            return true;
        }
    }
}
#endif
