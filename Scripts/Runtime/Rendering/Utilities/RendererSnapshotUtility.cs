using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.Rendering.Utilities
{
    public static class RendererSnapshotUtility
    {
        public static int CopyToSnapshot<T>(IReadOnlyList<T> source, ref T[] snapshot, bool clearRemainder = true)
        {
            int count = source != null ? source.Count : 0;
            EnsureCapacity(ref snapshot, count);

            for (int i = 0; i < count; ++i)
            {
                snapshot[i] = source[i];
            }

            if (clearRemainder && snapshot != null)
            {
                Array.Clear(snapshot, count, snapshot.Length - count);
            }

            return count;
        }

        public static int CopyToSnapshot<T>(List<T> source, ref T[] snapshot, bool clearRemainder = true)
        {
            return CopyToSnapshot((IReadOnlyList<T>)source, ref snapshot, clearRemainder);
        }

        public static void EnsureCapacity<T>(ref T[] snapshot, int count)
        {
            if (snapshot != null && snapshot.Length >= count)
            {
                return;
            }

            int capacity = Mathf.Max(1, Mathf.NextPowerOfTwo(Mathf.Max(1, count)));
            snapshot = new T[capacity];
        }
    }
}
