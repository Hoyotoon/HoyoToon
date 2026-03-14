#if UNITY_EDITOR
using System;

namespace HoyoToon.Editor.API
{
    [Serializable]
    public class BoneConstraintRule
    {
        public string TargetBone { get; set; }
        public string SourceBone { get; set; }

        // Parent, Rotation, Position
        public string ConstraintType { get; set; }
        public float? Weight { get; set; }
        public float? SourceWeight { get; set; }
        public ConstraintAxisConfig PositionAxes { get; set; }
        public ConstraintAxisConfig RotationAxes { get; set; }
        public bool? MaintainOffset { get; set; }
        public bool? Active { get; set; }
        public bool? Locked { get; set; }
    }

    [Serializable]
    public class ConstraintAxisConfig
    {
        public bool? X { get; set; }
        public bool? Y { get; set; }
        public bool? Z { get; set; }
    }
}
#endif
