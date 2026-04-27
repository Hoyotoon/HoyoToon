using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.Utilities
{
    public static class UnityTagUtility
    {
        private static readonly HashSet<string> s_WarnedMissingTagOperations = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWarnings()
        {
            s_WarnedMissingTagOperations.Clear();
        }

        public static bool TryAssignTag(GameObject target, string tagName)
        {
            if (target == null || string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            try
            {
                if (string.Equals(target.tag, tagName, System.StringComparison.Ordinal))
                {
                    return true;
                }

                target.tag = tagName;
                return true;
            }
            catch (UnityException exception)
            {
                WarnMissingTagOnce(tagName, "assignment", exception);
                return false;
            }
        }

        public static bool TryCompareTag(GameObject target, string tagName)
        {
            if (target == null || string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            try
            {
                return target.CompareTag(tagName);
            }
            catch (UnityException exception)
            {
                WarnMissingTagOnce(tagName, "comparison", exception);
                return false;
            }
        }

        public static bool TryFindGameObjectsWithTag(string tagName, List<GameObject> results)
        {
            results?.Clear();
            if (results == null || string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            try
            {
                GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tagName);
                for (int i = 0; i < taggedObjects.Length; ++i)
                {
                    if (taggedObjects[i] != null)
                    {
                        results.Add(taggedObjects[i]);
                    }
                }

                return true;
            }
            catch (UnityException exception)
            {
                WarnMissingTagOnce(tagName, "lookup", exception);
                return false;
            }
        }

        private static void WarnMissingTagOnce(string tagName, string operation, UnityException exception)
        {
            string key = operation + ":" + tagName;
            if (!s_WarnedMissingTagOperations.Add(key))
            {
                return;
            }

            Debug.LogWarning($"HoyoToon skipped tag {operation} for '{tagName}'. Run the HoyoToon prerequisite checks to add the missing tag. {exception.Message}");
        }
    }
}
