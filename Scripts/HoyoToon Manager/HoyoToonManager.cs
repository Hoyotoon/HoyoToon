using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon
{
    /// <summary>
    /// Runtime manager component responsible for coordinating HoyoToon systems in a scene.
    /// Serves as an anchor for editor tooling/custom inspectors so designers can configure
    /// shared resources from a single GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/HoyoToon Manager")]
    public class HoyoToonManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Automatically bootstrap HoyoToon services on Awake.")]
        private bool autoInitialize = true;

        [SerializeField]
        [Tooltip("Characters or models managed globally by HoyoToon features.")]
        private List<GameObject> managedModels = new List<GameObject>();

        [SerializeField]
        [Tooltip("Index of the currently active model inside the managed list. -1 means none.")]
        private int activeModelIndex = -1;

        public bool AutoInitialize => autoInitialize;
        public IReadOnlyList<GameObject> ManagedModels => managedModels;

        public int ActiveModelIndex
        {
            get
            {
                if (managedModels == null || managedModels.Count == 0)
                {
                    return -1;
                }

                activeModelIndex = Mathf.Clamp(activeModelIndex, -1, managedModels.Count - 1);
                return activeModelIndex;
            }
            set
            {
                if (managedModels == null || managedModels.Count == 0)
                {
                    activeModelIndex = -1;
                    return;
                }

                activeModelIndex = Mathf.Clamp(value, -1, managedModels.Count - 1);
            }
        }

        public GameObject ActiveModel
        {
            get
            {
                int index = ActiveModelIndex;
                if (index < 0 || index >= managedModels.Count)
                {
                    return null;
                }

                return managedModels[index];
            }
        }

        private void Awake()
        {
            if (!autoInitialize)
            {
                return;
            }

            Initialize();
        }

        /// <summary>
        /// Entry point for runtime setup. Currently a placeholder that can be expanded as
        /// subsystems come online.
        /// </summary>
        public void Initialize() { }

        public void RegisterModel(GameObject model)
        {
            if (model == null)
            {
                return;
            }

            if (managedModels == null)
            {
                managedModels = new List<GameObject>();
            }

            if (!managedModels.Contains(model))
            {
                managedModels.Add(model);
            }

            if (ActiveModelIndex == -1)
            {
                ActiveModelIndex = managedModels.Count - 1;
            }
        }

        public void RemoveModel(GameObject model)
        {
            if (managedModels == null || model == null)
            {
                return;
            }

            int index = managedModels.IndexOf(model);
            if (index < 0)
            {
                return;
            }

            managedModels.RemoveAt(index);

            if (managedModels.Count == 0)
            {
                activeModelIndex = -1;
            }
            else if (activeModelIndex >= managedModels.Count)
            {
                activeModelIndex = managedModels.Count - 1;
            }
        }
    }
}
