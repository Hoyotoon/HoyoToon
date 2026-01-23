#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.EditorTools.ManagerUI.Modules
{
    internal abstract class HoyoToonManagerModule : IDisposable
    {
        public abstract string DisplayName { get; }

        /// <summary>
        /// Called to draw the module's UI. Modules can assume GUI layout context.
        /// </summary>
        public abstract void OnGUI(HoyoToonManager targetManager);

        public virtual void Dispose()
        {
        }
    }
}
#endif
