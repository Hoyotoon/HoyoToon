using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HoyoToon.Editor.API;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Assets
{
    public static class GeneratedAssetQueryUtility
    {
        private const BindingFlags InitializeMethodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static IEnumerable<TAsset> LoadGeneratedAssets<TAsset>(string searchFilter, string assetSuffix)
            where TAsset : UnityEngine.Object
        {
            GeneratedAssetCacheRefreshUtility.RegisterCallingCacheOwner(ResolveCallingCacheOwner());

            if (string.IsNullOrWhiteSpace(searchFilter) || string.IsNullOrWhiteSpace(assetSuffix))
            {
                return Enumerable.Empty<TAsset>();
            }

            return AssetDatabase.FindAssets(searchFilter, new[] { HoyoToonApi.ScriptablesAssetPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(assetPath => assetPath.EndsWith(assetSuffix, StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<TAsset>)
                .Where(asset => asset != null);
        }

        private static Type ResolveCallingCacheOwner()
        {
            MethodBase loadMethod = MethodBase.GetCurrentMethod();
            MethodBase[] stack = new System.Diagnostics.StackTrace().GetFrames()?
                .Select(frame => frame.GetMethod())
                .Where(method => method != null)
                .ToArray();

            if (stack == null)
            {
                return null;
            }

            foreach (MethodBase method in stack)
            {
                Type declaringType = method.DeclaringType;
                if (declaringType == null || declaringType == typeof(GeneratedAssetQueryUtility) || method == loadMethod)
                {
                    continue;
                }

                if (declaringType.GetMethod("Initialize", InitializeMethodFlags, null, Type.EmptyTypes, null) != null)
                {
                    return declaringType;
                }
            }

            return null;
        }
    }
}