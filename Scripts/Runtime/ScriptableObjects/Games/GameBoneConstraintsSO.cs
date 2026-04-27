using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameBoneConstraints", menuName = "HoyoToon/GameBoneConstraints")]
    public class GameBoneConstraintsSO : ScriptableObject
    {
        [Serializable]
        public class Entry : GameScopedEntry
        {
            [SerializeField] private string targetBone;
            [SerializeField] private string sourceBone;
            [SerializeField] private string constraintType;
            [SerializeField] private float weight = 1f;
            [SerializeField] private float sourceWeight = 1f;
            [SerializeField] private GameAxesData positionAxes;
            [SerializeField] private GameAxesData rotationAxes;
            [SerializeField] private bool maintainOffset;
            [SerializeField] private bool active = true;
            [SerializeField] private bool locked;

            public string TargetBone => targetBone;

            public string SourceBone => sourceBone;

            public string ConstraintType => constraintType;

            public float Weight => weight;

            public float SourceWeight => sourceWeight;

            public GameAxesData PositionAxes => positionAxes;

            public GameAxesData RotationAxes => rotationAxes;

            public bool MaintainOffset => maintainOffset;

            public bool Active => active;

            public bool Locked => locked;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }
}