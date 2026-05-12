using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.Scene.Placement
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Scene/Manager Layout References")]
    public sealed class ManagerLayoutReferences : MonoBehaviour
    {
        [Serializable]
        public struct TeamRootReference
        {
            public TeamGame Game;
            public Transform Root;
        }

        [SerializeField] private Transform singleRoot;
        [SerializeField] private Transform teamRoot;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private GameObject singleCamera;
        [SerializeField] private GameObject teamCamera;
        [SerializeField] private GameObject legacyGridCamera;
        [SerializeField] private TeamRootReference[] teamRoots = Array.Empty<TeamRootReference>();

        public Transform SingleRoot => singleRoot;
        public Transform TeamRoot => teamRoot;
        public Transform GridRoot => gridRoot;
        public GameObject SingleCamera => singleCamera;
        public GameObject TeamCamera => teamCamera;
        public GameObject LegacyGridCamera => legacyGridCamera;
        public IReadOnlyList<TeamRootReference> TeamRoots => teamRoots;

        public bool TryGetTeamRoot(TeamGame game, out Transform root)
        {
            if (teamRoots != null)
            {
                for (int i = 0; i < teamRoots.Length; ++i)
                {
                    if (teamRoots[i].Game == game)
                    {
                        root = teamRoots[i].Root;
                        return root != null;
                    }
                }
            }

            root = null;
            return false;
        }

        private void OnValidate()
        {
            if (singleRoot == null || teamRoot == null || gridRoot == null)
            {
                Debug.LogWarning($"{nameof(ManagerLayoutReferences)} on '{name}' is missing one or more layout roots.", this);
            }
        }
    }
}
