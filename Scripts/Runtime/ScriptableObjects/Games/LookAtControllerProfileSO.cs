using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "LookAtControllerProfile", menuName = "HoyoToon/Look At Controller Profile")]
    public class LookAtControllerProfileSO : GameScopedScriptableObject
    {
        private const float MinimumAxisMagnitude = 0.0001f;
        private const float BlendShapeFullWeight = 100f;

        public enum EyeBlendShapeAxis
        {
            Yaw,
            Pitch,
        }

        public enum EyeBlendShapeDirection
        {
            Negative,
            Positive,
        }

        [Serializable]
        public sealed class LookAtIKSettings
        {
            [SerializeField] [Min(0f)] private float fadeInTime = 0.6f;
            [SerializeField] [Min(0f)] private float fadeOutTime = 0.7f;
            [SerializeField] [Min(0f)] private float speed = 1.6f;
            [SerializeField] [Min(0f)] private float stopSpeed = 0.05f;
            [SerializeField] [Range(0f, 1f)] private float upDownFilterIntensity = 0.5f;
            [SerializeField] [Range(0f, 1f)] private float leftRightFilterIntensity = 0.06f;
            [SerializeField] private bool stopLookAtIkUpdate;
            [SerializeField] [Min(0f)] private float minSpringHairAngle = 0.07f;
            [SerializeField] [Min(0f)] private float overrideMinSpringHairAngle = 0.6f;

            public float FadeInTime => Mathf.Max(0f, fadeInTime);
            public float FadeOutTime => Mathf.Max(0f, fadeOutTime);
            public float Speed => Mathf.Max(0f, speed);
            public float StopSpeed => Mathf.Max(0f, stopSpeed);
            public float UpDownFilterIntensity => Mathf.Clamp01(upDownFilterIntensity);
            public float LeftRightFilterIntensity => Mathf.Clamp01(leftRightFilterIntensity);
            public bool StopLookAtIkUpdate => stopLookAtIkUpdate;
            public float MinSpringHairAngle => Mathf.Max(0f, minSpringHairAngle);
            public float OverrideMinSpringHairAngle => Mathf.Max(0f, overrideMinSpringHairAngle);
        }

        [Serializable]
        public sealed class LookAtSolverSettings
        {
            [SerializeField] private bool useNoAnimatorMode;
            [SerializeField] private bool normalizeTarget;
            [SerializeField] private bool useNewRotationEvaluateMode;
            [SerializeField] private List<string> headBoneNames = new List<string> { "Head_M", "Head" };
            [SerializeField] private List<string> rootBoneNames = new List<string> { "Root_M", "Root" };
            [SerializeField] [Min(0)] private int spineNum = 4;
            [SerializeField] [Range(0f, 1f)] private float bodyWeight = 0.4f;
            [SerializeField] [Range(0f, 1f)] private float headWeight = 1f;
            [SerializeField] [Range(0f, 180f)] private float bodyRotMax = 180f;
            [SerializeField] [Range(0f, 180f)] private float headRotMax = 180f;
            [SerializeField] [Min(0f)] private float bodyPitchUpFactor = 0.25f;
            [SerializeField] [Min(0f)] private float bodyPitchDownFactor = 1f;
            [SerializeField] [Min(0f)] private float headPitchFactor = 1f;
            [SerializeField] [Range(0f, 1f)] private float bodyDefaultFwdWeight = 0.5f;
            [SerializeField] [Range(0f, 1f)] private float headDefaultFwdWeight = 1f;
            [SerializeField] private bool autoDetectLocalForwardAxis = true;
            [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
            [SerializeField] private Vector3 localUpAxis = Vector3.up;
            [SerializeField] private AnimationCurve uprightConstraintCurve = CreateDefaultUprightConstraintCurve();

            public bool UseNoAnimatorMode => useNoAnimatorMode;
            public bool NormalizeTarget => normalizeTarget;
            public bool UseNewRotationEvaluateMode => useNewRotationEvaluateMode;
            public IReadOnlyList<string> HeadBoneNames => headBoneNames;
            public IReadOnlyList<string> RootBoneNames => rootBoneNames;
            public int SpineNum => Mathf.Max(0, spineNum);
            public float BodyWeight => Mathf.Clamp01(bodyWeight);
            public float HeadWeight => Mathf.Clamp01(headWeight);
            public float BodyRotMax => Mathf.Clamp(bodyRotMax, 0f, 180f);
            public float HeadRotMax => Mathf.Clamp(headRotMax, 0f, 180f);
            public float BodyPitchUpFactor => Mathf.Max(0f, bodyPitchUpFactor);
            public float BodyPitchDownFactor => Mathf.Max(0f, bodyPitchDownFactor);
            public float HeadPitchFactor => Mathf.Max(0f, headPitchFactor);
            public float BodyDefaultFwdWeight => Mathf.Clamp01(bodyDefaultFwdWeight);
            public float HeadDefaultFwdWeight => Mathf.Clamp01(headDefaultFwdWeight);
            public bool AutoDetectLocalForwardAxis => autoDetectLocalForwardAxis;
            public Vector3 LocalForwardAxis => NormalizeAxis(localForwardAxis, Vector3.forward);
            public Vector3 LocalUpAxis => NormalizeAxis(localUpAxis, Vector3.up);
            public AnimationCurve UprightConstraintCurve => uprightConstraintCurve;
        }

        [Serializable]
        public sealed class LookAtTargetConstraint
        {
            [SerializeField] private bool drawConstraint;
            [SerializeField] [Range(0f, 89.9f)] private float pitchUp = 30f;
            [SerializeField] [Range(0f, 89.9f)] private float pitchDown = 30f;
            [SerializeField] [Range(0f, 180f)] private float yawLeft = 50f;
            [SerializeField] [Range(0f, 180f)] private float yawRight = 50f;

            public bool DrawConstraint => drawConstraint;
            public float PitchUp => Mathf.Clamp(pitchUp, 0f, 89.9f);
            public float PitchDown => Mathf.Clamp(pitchDown, 0f, 89.9f);
            public float YawLeft => Mathf.Clamp(yawLeft, 0f, 180f);
            public float YawRight => Mathf.Clamp(yawRight, 0f, 180f);

            public bool Contains(float yawDegrees, float pitchDegrees)
            {
                return yawDegrees >= -YawLeft
                    && yawDegrees <= YawRight
                    && pitchDegrees >= -PitchDown
                    && pitchDegrees <= PitchUp;
            }

            public Vector2 Clamp(float yawDegrees, float pitchDegrees)
            {
                return new Vector2(
                    Mathf.Clamp(yawDegrees, -YawLeft, YawRight),
                    Mathf.Clamp(pitchDegrees, -PitchDown, PitchUp));
            }
        }

        [Serializable]
        public sealed class EyeBlendShapeConfig
        {
            [SerializeField] private string blendShapeName;
            [SerializeField] private EyeBlendShapeAxis axis;
            [SerializeField] private EyeBlendShapeDirection direction = EyeBlendShapeDirection.Positive;
            [SerializeField] [Range(0f, 100f)] private float maxValue = BlendShapeFullWeight;
            [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            public string BlendShapeName => blendShapeName;
            public EyeBlendShapeAxis Axis => axis;
            public EyeBlendShapeDirection Direction => direction;
            public float MaxValue => Mathf.Clamp(maxValue, 0f, BlendShapeFullWeight);
            public AnimationCurve Curve => curve;
            public bool IsValid => !string.IsNullOrWhiteSpace(blendShapeName);

            public float Evaluate(float normalizedAxisWeight)
            {
                float clampedWeight = Mathf.Clamp01(normalizedAxisWeight);
                float curveWeight = curve != null && curve.length > 0
                    ? curve.Evaluate(clampedWeight)
                    : clampedWeight;

                return Mathf.Clamp01(curveWeight) * MaxValue;
            }
        }

        [Serializable]
        public sealed class EyeFollowSettings
        {
            [SerializeField] private bool enabled = true;
            [SerializeField] private bool rotateEyeBones = true;
            [SerializeField] private bool driveBlendShapes;
            [SerializeField] [Range(0f, 1f)] private float weight = 1f;
            [SerializeField] [Min(0f)] private float speed = 8f;
            [SerializeField] [Range(0f, 89.9f)] private float yawLeft = 18f;
            [SerializeField] [Range(0f, 89.9f)] private float yawRight = 18f;
            [SerializeField] [Range(0f, 89.9f)] private float pitchUp = 12f;
            [SerializeField] [Range(0f, 89.9f)] private float pitchDown = 10f;
            [SerializeField] private List<string> leftEyeBoneNames = new List<string>
            {
                "Eye_L",
                "Eye_L_M",
                "L_Eye",
                "L_Eye_M",
                "LeftEye",
                "Left_Eye",
            };
            [SerializeField] private List<string> rightEyeBoneNames = new List<string>
            {
                "Eye_R",
                "Eye_R_M",
                "R_Eye",
                "R_Eye_M",
                "RightEye",
                "Right_Eye",
            };
            [SerializeField] private bool autoDetectLocalForwardAxis = true;
            [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
            [SerializeField] private Vector3 localUpAxis = Vector3.up;
            [SerializeField] private List<EyeBlendShapeConfig> blendShapes = new List<EyeBlendShapeConfig>();

            public bool Enabled => enabled;
            public bool RotateEyeBones => rotateEyeBones;
            public bool DriveBlendShapes => driveBlendShapes;
            public float Weight => Mathf.Clamp01(weight);
            public float Speed => Mathf.Max(0f, speed);
            public float YawLeft => Mathf.Clamp(yawLeft, 0f, 89.9f);
            public float YawRight => Mathf.Clamp(yawRight, 0f, 89.9f);
            public float PitchUp => Mathf.Clamp(pitchUp, 0f, 89.9f);
            public float PitchDown => Mathf.Clamp(pitchDown, 0f, 89.9f);
            public IReadOnlyList<string> LeftEyeBoneNames => leftEyeBoneNames;
            public IReadOnlyList<string> RightEyeBoneNames => rightEyeBoneNames;
            public bool AutoDetectLocalForwardAxis => autoDetectLocalForwardAxis;
            public Vector3 LocalForwardAxis => NormalizeAxis(localForwardAxis, Vector3.forward);
            public Vector3 LocalUpAxis => NormalizeAxis(localUpAxis, Vector3.up);
            public IReadOnlyList<EyeBlendShapeConfig> BlendShapes => blendShapes;
            public bool HasBlendShapes => blendShapes != null && blendShapes.Count > 0;

            public Vector2 Clamp(float yawDegrees, float pitchDegrees)
            {
                return new Vector2(
                    Mathf.Clamp(yawDegrees, -YawLeft, YawRight),
                    Mathf.Clamp(pitchDegrees, -PitchDown, PitchUp));
            }

            public bool CanDriveBlendShape(string blendShapeName)
            {
                if (string.IsNullOrWhiteSpace(blendShapeName) || blendShapes == null)
                    return false;

                for (int i = 0; i < blendShapes.Count; ++i)
                {
                    EyeBlendShapeConfig config = blendShapes[i];
                    if (config != null
                        && config.IsValid
                        && string.Equals(config.BlendShapeName, blendShapeName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [SerializeField] private string displayName;
        [SerializeField] private List<string> lookAtStates = new List<string>();
        [SerializeField] [Min(0f)] private float lookWaitTime;
        [SerializeField] [Min(0f)] private float idleShowWaitTime;
        [SerializeField] private float upWeight;
        [SerializeField] private float forwardWeight;
        [SerializeField] private float rightWeight;
        [SerializeField] [Min(0f)] private float outOfConstraintTimer = 0.2f;
        [SerializeField] private bool disableWhenOutOfConstraint;
        [SerializeField] [Min(0f)] private float constraintReentryMargin = 25f;
        [SerializeField] private LookAtTargetConstraint constraint = new LookAtTargetConstraint();
        [SerializeField] private LookAtTargetConstraint anotherConstraint = new LookAtTargetConstraint();
        [SerializeField] private bool useAnotherConstraintAtCloseRange;
        [SerializeField] [Min(0f)] private float closeConstraintDistance = 1.25f;
        [SerializeField] [Min(0f)] private float closeConstraintBlendRange = 0.75f;
        [SerializeField] [Min(0f)] private float constraintSpeed = 1f;
        [SerializeField] private AnimationCurve constraintCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] [Min(0f)] private float outConstraintSpeed = 1f;
        [SerializeField] private AnimationCurve outConstraintCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private LookAtIKSettings lookAt = new LookAtIKSettings();
        [SerializeField] private LookAtSolverSettings solver = new LookAtSolverSettings();
        [SerializeField] private EyeFollowSettings eyeFollow = new EyeFollowSettings();

        public string DisplayName => displayName;
        public IReadOnlyList<string> LookAtStates => lookAtStates;
        public float LookWaitTime => Mathf.Max(0f, lookWaitTime);
        public float IdleShowWaitTime => Mathf.Max(0f, idleShowWaitTime);
        public float UpWeight => upWeight;
        public float ForwardWeight => forwardWeight;
        public float RightWeight => rightWeight;
        public float OutOfConstraintTimer => Mathf.Max(0f, outOfConstraintTimer);
        public bool DisableWhenOutOfConstraint => disableWhenOutOfConstraint;
        public float ConstraintReentryMargin => Mathf.Max(0f, constraintReentryMargin);
        public LookAtTargetConstraint Constraint => constraint;
        public LookAtTargetConstraint AnotherConstraint => anotherConstraint;
        public bool UseAnotherConstraintAtCloseRange => useAnotherConstraintAtCloseRange;
        public float CloseConstraintDistance => Mathf.Max(0f, closeConstraintDistance);
        public float CloseConstraintBlendRange => Mathf.Max(0f, closeConstraintBlendRange);
        public float ConstraintSpeed => Mathf.Max(0f, constraintSpeed);
        public AnimationCurve ConstraintCurve => constraintCurve;
        public float OutConstraintSpeed => Mathf.Max(0f, outConstraintSpeed);
        public AnimationCurve OutConstraintCurve => outConstraintCurve;
        public LookAtIKSettings LookAt => lookAt;
        public LookAtSolverSettings Solver => solver;
        public EyeFollowSettings EyeFollow => eyeFollow;

        private static Vector3 NormalizeAxis(Vector3 axis, Vector3 fallback)
        {
            return axis.sqrMagnitude >= MinimumAxisMagnitude ? axis.normalized : fallback;
        }

        private static AnimationCurve CreateDefaultUprightConstraintCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(1f, 0.5f));
        }
    }
}
