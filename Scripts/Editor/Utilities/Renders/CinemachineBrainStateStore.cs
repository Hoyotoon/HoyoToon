#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Renders
{
    internal sealed class CinemachineBrainStateStore
    {
        private const string CinemachineBrainTypeName = "CinemachineBrain";

        private readonly List<Entry> entries = new List<Entry>();

        internal void DisableForSync(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            Behaviour[] behaviours = camera.GetComponents<Behaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                Behaviour behaviour = behaviours[index];
                if (behaviour == null
                    || !IsCinemachineBrain(behaviour)
                    || Contains(behaviour))
                {
                    continue;
                }

                entries.Add(new Entry(behaviour, behaviour.enabled));
                behaviour.enabled = false;
            }
        }

        internal void Restore()
        {
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                Entry entry = entries[index];
                if (entry.Behaviour != null)
                {
                    entry.Behaviour.enabled = entry.WasEnabled;
                }
            }

            entries.Clear();
        }

        private bool Contains(Behaviour behaviour)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].Behaviour == behaviour)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCinemachineBrain(Behaviour behaviour)
        {
            return string.Equals(behaviour.GetType().Name, CinemachineBrainTypeName, StringComparison.Ordinal);
        }

        private readonly struct Entry
        {
            internal readonly Behaviour Behaviour;
            internal readonly bool WasEnabled;

            internal Entry(Behaviour behaviour, bool wasEnabled)
            {
                Behaviour = behaviour;
                WasEnabled = wasEnabled;
            }
        }
    }
}
#endif
