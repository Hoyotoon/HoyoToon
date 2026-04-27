#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HoyoToon.Editor.Utilities.Assets
{
    internal static class GeneratedAssetCacheRefreshUtility
    {
        private const BindingFlags InitializeMethodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly HashSet<Type> RegisteredCacheOwners = new HashSet<Type>();

        public static void RegisterCallingCacheOwner(Type cacheOwnerType)
        {
            if (cacheOwnerType == null)
            {
                return;
            }

            MethodInfo initializeMethod = cacheOwnerType.GetMethod("Initialize", InitializeMethodFlags, null, Type.EmptyTypes, null);
            if (initializeMethod == null)
            {
                return;
            }

            RegisteredCacheOwners.Add(cacheOwnerType);
        }

        public static void RefreshAll()
        {
            foreach (Type cacheOwnerType in RegisteredCacheOwners.OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                MethodInfo initializeMethod = cacheOwnerType.GetMethod("Initialize", InitializeMethodFlags, null, Type.EmptyTypes, null);
                initializeMethod?.Invoke(null, null);
            }
        }
    }
}
#endif