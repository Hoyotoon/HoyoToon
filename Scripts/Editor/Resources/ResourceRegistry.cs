#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Resources;

namespace HoyoToon.Editor.Resources
{
    internal static class ResourceRegistry
    {
        private const string ResourceAssetSuffix = "/Resources/HoyoToonResources.asset";

        private static List<ResourcesSO> resources = new List<ResourcesSO>();
        private static bool isInitialized;

        internal static IReadOnlyList<ResourcesSO> Resources
        {
            get
            {
                EnsureInitialized();
                return resources;
            }
        }

        internal static void Initialize()
        {
            resources = GeneratedAssetQueryUtility
                .LoadGeneratedAssets<ResourcesSO>("t:ResourcesSO", ResourceAssetSuffix)
                .Where(resource => resource != null && !string.IsNullOrWhiteSpace(resource.Key))
                .GroupBy(resource => resource.Key, StringComparer.Ordinal)
                .Select(group => SelectResourceAsset(group))
                .Where(resource => resource != null)
                .OrderBy(resource => resource.Key, StringComparer.Ordinal)
                .ToList();

            isInitialized = true;
            HoyoToonLogger.Verbose(HoyoToonLogCategory.Resources, $"Loaded {resources.Count} resource definition(s) into the resource registry.");
        }

        internal static bool TryGetResources(string key, out ResourcesSO resourceAsset)
        {
            EnsureInitialized();
            resourceAsset = resources.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
            return resourceAsset != null;
        }

        private static ResourcesSO SelectResourceAsset(IGrouping<string, ResourcesSO> group)
        {
            ResourcesSO selected = group.FirstOrDefault();
            int count = group.Count();
            if (count > 1 && selected != null)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Resources,
                    $"Found {count} generated resource definitions for '{group.Key}'. Using '{UnityEditor.AssetDatabase.GetAssetPath(selected)}'.",
                    context: selected);
            }

            return selected;
        }

        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }
    }
}
#endif
