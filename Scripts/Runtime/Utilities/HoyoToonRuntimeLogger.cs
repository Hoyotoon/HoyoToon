using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace HoyoToon.Runtime.Utilities
{
    internal static class HoyoToonRuntimeLogger
    {
        private static readonly HashSet<LogKey> s_Warnings = new HashSet<LogKey>();
        private static readonly HashSet<LogKey> s_Errors = new HashSet<LogKey>();

        internal static void WarningOnce(object owner, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (s_Warnings.Add(LogKey.Create(owner, message)))
                Debug.LogWarning(message);
        }

        internal static void ErrorOnce(object owner, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (s_Errors.Add(LogKey.Create(owner, message)))
                Debug.LogError(message);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_Warnings.Clear();
            s_Errors.Clear();
        }

        private readonly struct LogKey
        {
            private LogKey(int ownerId, int messageHash)
            {
                OwnerId = ownerId;
                MessageHash = messageHash;
            }

            private readonly int OwnerId;
            private readonly int MessageHash;

            internal static LogKey Create(object owner, string message)
            {
                return new LogKey(GetOwnerId(owner), message.GetHashCode());
            }

            public override bool Equals(object obj)
            {
                return obj is LogKey other
                    && OwnerId == other.OwnerId
                    && MessageHash == other.MessageHash;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (OwnerId * 397) ^ MessageHash;
                }
            }

            private static int GetOwnerId(object owner)
            {
                if (owner == null)
                    return 0;

                if (owner is Object unityObject)
                    return unityObject != null ? unityObject.GetInstanceID() : 0;

                return RuntimeHelpers.GetHashCode(owner);
            }
        }
    }
}
