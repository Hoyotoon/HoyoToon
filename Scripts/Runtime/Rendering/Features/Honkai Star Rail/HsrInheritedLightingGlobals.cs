using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Scene;
using UnityEngine;
using UnityEngine.Rendering;
using HoyoToon.Runtime.Scene.HSR;

namespace HoyoToon.Runtime.Rendering.HSR
{
    internal static class HsrInheritedLightingGlobals
    {
        static readonly int k_CascadeShadowSplitSpheres0Id = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
        static readonly int k_CascadeShadowSplitSpheres1Id = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
        static readonly int k_CascadeShadowSplitSpheres2Id = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
        static readonly int k_CascadeShadowSplitSpheres3Id = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
        static readonly int k_CascadeShadowSplitSphereRadiiId = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
        static readonly int k_MainLightShadowParamsId = Shader.PropertyToID("_MainLightShadowParams");
        static readonly int k_MainLightShadowmapSizeId = Shader.PropertyToID("_MainLightShadowmapSize");
        static readonly int k_MainLightShadowCascadeCountId = Shader.PropertyToID("_MainLightShadowCascadeCount");
        static readonly int k_MainLightWorldToShadowId = Shader.PropertyToID("_MainLightWorldToShadow");
        static readonly int k_MainLightWorldToShadowArrId = Shader.PropertyToID("_MainLightWorldToShadowArr");
        static readonly int k_EsGlobalRotMatrixId = Shader.PropertyToID("_ES_GlobalRotMatrix");
        static readonly int k_GlobalOneMinusAvatarIntensityId = Shader.PropertyToID("_GlobalOneMinusAvatarIntensity");
        static readonly int k_XPad0Id = Shader.PropertyToID("_XPad0");
        static readonly int k_EsMonsterLightDirId = Shader.PropertyToID("_ES_MonsterLightDir");
        static readonly int k_EsIndoorId = Shader.PropertyToID("_ES_Indoor");
        static readonly int k_EsTransitionRateId = Shader.PropertyToID("_ES_TransitionRate");
        static readonly int k_EsSelfShadowLerpHairId = Shader.PropertyToID("_ES_SelfShadowLerpHair");
        static readonly int k_EsLevelAdjustOnId = Shader.PropertyToID("_ES_LEVEL_ADJUST_ON");
        static readonly int k_XPad1Id = Shader.PropertyToID("_XPad1");
        static readonly int k_EsCharacterToonRampModeId = Shader.PropertyToID("_ES_CharacterToonRampMode");
        static readonly int k_EsCharacterDisableLocalMainLightId = Shader.PropertyToID("_ES_CharacterDisableLocalMainLight");
        static readonly int k_XPad2Id = Shader.PropertyToID("_XPad2");
        static readonly int k_EsAddColorId = Shader.PropertyToID("_ES_AddColor");
        static readonly int k_EsSpColorId = Shader.PropertyToID("_ES_SPColor");
        static readonly int k_EsSpIntensityId = Shader.PropertyToID("_ES_SPIntensity");
        static readonly int k_XPad3Id = Shader.PropertyToID("_XPad3");
        static readonly int k_EsRimShadowColorId = Shader.PropertyToID("_ES_RimShadowColor");
        static readonly int k_EsRimShadowIntensityId = Shader.PropertyToID("_ES_RimShadowIntensity");
        static readonly int k_EsCharacterShadowFactorId = Shader.PropertyToID("_ES_CharacterShadowFactor");
        static readonly int k_EsOutlineDarkenValId = Shader.PropertyToID("_ES_OutLineDarkenVal");
        static readonly int k_EsOutlineLightedValId = Shader.PropertyToID("_ES_OutLineLightedVal");
        static readonly int k_EsOutlineDisableDistanceScaleId = Shader.PropertyToID("_ES_OutlineDisableDistanceScale");
        static readonly int k_EsOutlineFallbackScaleId = Shader.PropertyToID("_ES_OutlineFallbackScale");
        static readonly int k_EsHeightLerpTopId = Shader.PropertyToID("_ES_HeightLerpTop");
        static readonly int k_EsHeightLerpBottomId = Shader.PropertyToID("_ES_HeightLerpBottom");
        static readonly int k_EsHeightLerpTopColorId = Shader.PropertyToID("_ES_HeightLerpTopColor");
        static readonly int k_EsHeightLerpMiddleColorId = Shader.PropertyToID("_ES_HeightLerpMiddleColor");
        static readonly int k_EsHeightLerpBottomColorId = Shader.PropertyToID("_ES_HeightLerpBottomColor");
        static readonly int k_EsRimLightOffsetId = Shader.PropertyToID("_ES_RimLightOffset");
        static readonly int k_EsRimLightWidthId = Shader.PropertyToID("_ES_RimLightWidth");
        static readonly int k_EsRimLightIntensityId = Shader.PropertyToID("_ES_RimLightIntensity");
        static readonly int k_EsRimLightAddModeId = Shader.PropertyToID("_ES_RimLightAddMode");
        static readonly int k_EsRimLightModeId = Shader.PropertyToID("_ES_RimLightMode");
        static readonly int k_XPad4Id = Shader.PropertyToID("_XPad4");
        static readonly int k_EsRimLightColorId = Shader.PropertyToID("_ES_RimLightColor");
        static readonly int k_EsLevelSkinLightColorId = Shader.PropertyToID("_ES_LevelSkinLightColor");
        static readonly int k_EsLevelSkinShadowColorId = Shader.PropertyToID("_ES_LevelSkinShadowColor");
        static readonly int k_EsLevelHighLightColorId = Shader.PropertyToID("_ES_LevelHighLightColor");
        static readonly int k_EsLevelShadowColorId = Shader.PropertyToID("_ES_LevelShadowColor");
        static readonly int k_EsLevelShadowId = Shader.PropertyToID("_ES_LevelShadow");
        static readonly int k_EsLevelMidId = Shader.PropertyToID("_ES_LevelMid");
        static readonly int k_EsLevelHighLightId = Shader.PropertyToID("_ES_LevelHighLight");
        static readonly int k_EsLevelEyeShadowIntensityId = Shader.PropertyToID("_ES_LevelEyeShadowIntensity");
        static readonly int k_EsIndoorCharShadowAsCookieId = Shader.PropertyToID("_ES_IndoorCharShadowAsCookie");
        static readonly int k_EsFogColorId = Shader.PropertyToID("_ES_FogColor");
        static readonly int k_EsFogDensityId = Shader.PropertyToID("_ES_FogDensity");
        static readonly int k_EsFogNearId = Shader.PropertyToID("_ES_FogNear");
        static readonly int k_EsFogFarId = Shader.PropertyToID("_ES_FogFar");
        static readonly int k_EsHeightFogColorId = Shader.PropertyToID("_ES_HeightFogColor");
        static readonly int k_EsHeightFogBaseHeightId = Shader.PropertyToID("_ES_HeightFogBaseHeight");
        static readonly int k_EsHeightFogRangeId = Shader.PropertyToID("_ES_HeightFogRange");
        static readonly int k_EsHeightFogDensityId = Shader.PropertyToID("_ES_HeightFogDensity");
        static readonly int k_EsHeightFogFogNearId = Shader.PropertyToID("_ES_HeightFogFogNear");
        static readonly int k_EsHeightFogFogFarId = Shader.PropertyToID("_ES_HeightFogFogFar");
        static readonly int k_EsFogCharacterNearFactorId = Shader.PropertyToID("_ES_FogCharacterNearFactor");
        static readonly int k_EsHeightFogAddAjustId = Shader.PropertyToID("_ES_HeightFogAddAjust");
        static readonly int k_EsDisableFogTransitionId = Shader.PropertyToID("_ES_DisableFogTransition");
        static readonly int k_XPad5Id = Shader.PropertyToID("_XPad5");
        static readonly int k_EsEffCustomLightPositionId = Shader.PropertyToID("_ES_EffCustomLightPosition");
        static readonly int k_OutlineScaleId = Shader.PropertyToID("_OutlineScale");
        const string k_HeightLerpKeyword = "_HEIGHTLERP";
        const string k_FogKeyword = "_ENABLE_FOG";

        static readonly Matrix4x4[] k_MainLightWorldToShadowScratch = new Matrix4x4[5];
        static readonly List<Matrix4x4> k_MainLightWorldToShadowCaptureScratch = new List<Matrix4x4>(5);
        static readonly Vector4[] k_EsGlobalRotMatrixScratch = new Vector4[4];
        static readonly Dictionary<CameraFrameKey, ShadowState> s_ShadowStateByCameraFrame =
            new Dictionary<CameraFrameKey, ShadowState>();
        static readonly List<CameraFrameKey> s_StaleShadowStateKeys = new List<CameraFrameKey>(4);
        static int s_LastShadowStatePruneFrame = -1;

        internal readonly struct CameraFrameKey : IEquatable<CameraFrameKey>
        {
            public CameraFrameKey(int frame, int cameraId)
            {
                Frame = frame;
                CameraId = cameraId;
            }

            public int Frame { get; }
            public int CameraId { get; }

            public bool Equals(CameraFrameKey other)
            {
                return Frame == other.Frame && CameraId == other.CameraId;
            }

            public override bool Equals(object obj)
            {
                return obj is CameraFrameKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Frame * 397) ^ CameraId;
                }
            }
        }

        internal readonly struct SceneKeywordState
        {
            public SceneKeywordState(bool heightLerpEnabled, bool fogEnabled)
            {
                HeightLerpEnabled = heightLerpEnabled;
                FogEnabled = fogEnabled;
            }

            public bool HeightLerpEnabled { get; }
            public bool FogEnabled { get; }
        }

        internal readonly struct SceneGlobalState
        {
            readonly ShadowState m_ShadowState;
            readonly EnvironmentState m_EnvironmentState;

            internal SceneGlobalState(CameraFrameKey cameraFrameKey, in ShadowState shadowState, in EnvironmentState environmentState, bool applyEnvironmentState)
            {
                CameraFrameKey = cameraFrameKey;
                m_ShadowState = shadowState;
                m_EnvironmentState = environmentState;
                ApplyEnvironmentState = applyEnvironmentState;
            }

            public CameraFrameKey CameraFrameKey { get; }
            public int Frame => CameraFrameKey.Frame;
            public int CameraId => CameraFrameKey.CameraId;
            public bool ApplyEnvironmentState { get; }

            internal ShadowState ShadowState => m_ShadowState;
            internal EnvironmentState EnvironmentState => m_EnvironmentState;
        }

        internal struct ShadowState
        {
            public Vector4 CascadeShadowSplitSpheres0;
            public Vector4 CascadeShadowSplitSpheres1;
            public Vector4 CascadeShadowSplitSpheres2;
            public Vector4 CascadeShadowSplitSpheres3;
            public Vector4 CascadeShadowSplitSphereRadii;
            public Vector4 MainLightShadowParams;
            public Vector4 MainLightShadowmapSize;
            public float MainLightShadowCascadeCount;
            public Matrix4x4 MainLightWorldToShadow0;
            public Matrix4x4 MainLightWorldToShadow1;
            public Matrix4x4 MainLightWorldToShadow2;
            public Matrix4x4 MainLightWorldToShadow3;
            public Matrix4x4 MainLightWorldToShadow4;
        }

        internal struct EnvironmentState
        {
            public float GlobalOneMinusAvatarIntensity;
            public Vector3 MonsterLightDir;
            public float Indoor;
            public float TransitionRate;
            public float SelfShadowLerpHair;
            public float LevelAdjustOn;
            public Matrix4x4 GlobalRotMatrix;
            public float CharacterToonRampMode;
            public float CharacterDisableLocalMainLight;
            public Vector4 AddColor;
            public Vector4 SpColor;
            public float SpIntensity;
            public Vector4 RimShadowColor;
            public float RimShadowIntensity;
            public float CharacterShadowFactor;
            public float OutlineDarkenVal;
            public float OutlineLightedVal;
            public float OutlineDisableDistanceScale;
            public float OutlineFallbackScale;
            public float HeightLerpTop;
            public float HeightLerpBottom;
            public Vector4 HeightLerpTopColor;
            public Vector4 HeightLerpMiddleColor;
            public Vector4 HeightLerpBottomColor;
            public Vector2 RimLightOffset;
            public float RimLightWidth;
            public float RimLightIntensity;
            public float RimLightAddMode;
            public float RimLightMode;
            public Vector4 RimLightColor;
            public Vector4 LevelSkinLightColor;
            public Vector4 LevelSkinShadowColor;
            public Vector4 LevelHighLightColor;
            public Vector4 LevelShadowColor;
            public float LevelShadow;
            public float LevelMid;
            public float LevelHighLight;
            public float LevelEyeShadowIntensity;
            public float IndoorCharShadowAsCookie;
            public float FogColor;
            public float FogDensity;
            public float FogNear;
            public float FogFar;
            public float HeightFogColor;
            public float HeightFogBaseHeight;
            public float HeightFogRange;
            public float HeightFogDensity;
            public float HeightFogFogNear;
            public float HeightFogFogFar;
            public float FogCharacterNearFactor;
            public float HeightFogAddAjust;
            public float DisableFogTransition;
            public Vector4 EffCustomLightPosition;
            public float OutlineScale;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCachedState()
        {
            s_ShadowStateByCameraFrame.Clear();
            s_StaleShadowStateKeys.Clear();
            k_MainLightWorldToShadowCaptureScratch.Clear();
            s_LastShadowStatePruneFrame = -1;
        }

        internal static SceneGlobalState CaptureSceneGlobals(HSRSceneController env, bool clearEnvironmentWhenMissing, Camera camera)
        {
            bool applyEnvironmentState = env != null || clearEnvironmentWhenMissing;
            CameraFrameKey cameraFrameKey = CreateCameraFrameKey(camera);
            return new SceneGlobalState(
                cameraFrameKey,
                GetOrCaptureShadowState(cameraFrameKey),
                applyEnvironmentState ? CaptureEnvironmentState(env) : default,
                applyEnvironmentState);
        }

        static CameraFrameKey CreateCameraFrameKey(Camera camera)
        {
            return new CameraFrameKey(Time.frameCount, camera != null ? camera.GetInstanceID() : 0);
        }

        internal static void ApplySceneGlobals(RasterCommandBuffer cmd, in SceneGlobalState globals)
        {
            if (cmd == null)
                return;

            ApplyShadowState(cmd, globals.ShadowState);

            if (globals.ApplyEnvironmentState)
                ApplyEnvironmentState(cmd, globals.EnvironmentState);
        }

        internal static SceneKeywordState CaptureSceneKeywords(HSRSceneController env)
        {
            return env != null
                ? new SceneKeywordState(env.HeightLerpEnable, env.SceneFogEnabled)
                : default;
        }

        internal static void ApplySceneKeywords(RasterCommandBuffer cmd, in SceneKeywordState keywords)
        {
            if (cmd == null)
                return;

            CoreUtils.SetKeyword(cmd, k_HeightLerpKeyword, keywords.HeightLerpEnabled);
            CoreUtils.SetKeyword(cmd, k_FogKeyword, keywords.FogEnabled);
        }

        internal static void ClearSceneKeywords(RasterCommandBuffer cmd)
        {
            if (cmd == null)
                return;

            CoreUtils.SetKeyword(cmd, k_HeightLerpKeyword, false);
            CoreUtils.SetKeyword(cmd, k_FogKeyword, false);
        }

        static ShadowState GetOrCaptureShadowState(CameraFrameKey cameraFrameKey)
        {
            PruneShadowStateCache(cameraFrameKey.Frame);

            if (s_ShadowStateByCameraFrame.TryGetValue(cameraFrameKey, out ShadowState shadowState))
                return shadowState;

            shadowState = CaptureShadowState();
            s_ShadowStateByCameraFrame[cameraFrameKey] = shadowState;
            return shadowState;
        }

        static void PruneShadowStateCache(int frame)
        {
            if (s_LastShadowStatePruneFrame == frame)
                return;

            s_LastShadowStatePruneFrame = frame;
            s_StaleShadowStateKeys.Clear();

            foreach (CameraFrameKey cachedKey in s_ShadowStateByCameraFrame.Keys)
            {
                if (cachedKey.Frame != frame)
                    s_StaleShadowStateKeys.Add(cachedKey);
            }

            for (int i = 0; i < s_StaleShadowStateKeys.Count; ++i)
            {
                s_ShadowStateByCameraFrame.Remove(s_StaleShadowStateKeys[i]);
            }

            s_StaleShadowStateKeys.Clear();
        }

        static ShadowState CaptureShadowState()
        {
            float cascadeCount = Shader.GetGlobalFloat(k_MainLightShadowCascadeCountId);
            k_MainLightWorldToShadowCaptureScratch.Clear();
            Shader.GetGlobalMatrixArray(k_MainLightWorldToShadowId, k_MainLightWorldToShadowCaptureScratch);
            Matrix4x4 shadow0 = Matrix4x4.identity;
            Matrix4x4 shadow1 = Matrix4x4.identity;
            Matrix4x4 shadow2 = Matrix4x4.identity;
            Matrix4x4 shadow3 = Matrix4x4.identity;
            Matrix4x4 shadow4 = Matrix4x4.identity;

            int shadowMatrixCount = k_MainLightWorldToShadowCaptureScratch.Count;
            if (shadowMatrixCount > 0)
            {
                if (shadowMatrixCount > 0) shadow0 = k_MainLightWorldToShadowCaptureScratch[0];
                if (shadowMatrixCount > 1) shadow1 = k_MainLightWorldToShadowCaptureScratch[1];
                if (shadowMatrixCount > 2) shadow2 = k_MainLightWorldToShadowCaptureScratch[2];
                if (shadowMatrixCount > 3) shadow3 = k_MainLightWorldToShadowCaptureScratch[3];
                if (shadowMatrixCount > 4) shadow4 = k_MainLightWorldToShadowCaptureScratch[4];

                if (cascadeCount <= 0f)
                    cascadeCount = Mathf.Min(4, shadowMatrixCount);
            }
            else if (cascadeCount <= 0f)
            {
                cascadeCount = 1f;
            }

            return new ShadowState
            {
                CascadeShadowSplitSpheres0 = Shader.GetGlobalVector(k_CascadeShadowSplitSpheres0Id),
                CascadeShadowSplitSpheres1 = Shader.GetGlobalVector(k_CascadeShadowSplitSpheres1Id),
                CascadeShadowSplitSpheres2 = Shader.GetGlobalVector(k_CascadeShadowSplitSpheres2Id),
                CascadeShadowSplitSpheres3 = Shader.GetGlobalVector(k_CascadeShadowSplitSpheres3Id),
                CascadeShadowSplitSphereRadii = Shader.GetGlobalVector(k_CascadeShadowSplitSphereRadiiId),
                MainLightShadowParams = Shader.GetGlobalVector(k_MainLightShadowParamsId),
                MainLightShadowmapSize = Shader.GetGlobalVector(k_MainLightShadowmapSizeId),
                MainLightShadowCascadeCount = cascadeCount,
                MainLightWorldToShadow0 = shadow0,
                MainLightWorldToShadow1 = shadow1,
                MainLightWorldToShadow2 = shadow2,
                MainLightWorldToShadow3 = shadow3,
                MainLightWorldToShadow4 = shadow4
            };
        }

        static void ApplyShadowState(RasterCommandBuffer cmd, in ShadowState shadowState)
        {
            k_MainLightWorldToShadowScratch[0] = shadowState.MainLightWorldToShadow0;
            k_MainLightWorldToShadowScratch[1] = shadowState.MainLightWorldToShadow1;
            k_MainLightWorldToShadowScratch[2] = shadowState.MainLightWorldToShadow2;
            k_MainLightWorldToShadowScratch[3] = shadowState.MainLightWorldToShadow3;
            k_MainLightWorldToShadowScratch[4] = shadowState.MainLightWorldToShadow4;

            cmd.SetGlobalVector(k_CascadeShadowSplitSpheres0Id, shadowState.CascadeShadowSplitSpheres0);
            cmd.SetGlobalVector(k_CascadeShadowSplitSpheres1Id, shadowState.CascadeShadowSplitSpheres1);
            cmd.SetGlobalVector(k_CascadeShadowSplitSpheres2Id, shadowState.CascadeShadowSplitSpheres2);
            cmd.SetGlobalVector(k_CascadeShadowSplitSpheres3Id, shadowState.CascadeShadowSplitSpheres3);
            cmd.SetGlobalVector(k_CascadeShadowSplitSphereRadiiId, shadowState.CascadeShadowSplitSphereRadii);
            cmd.SetGlobalVector(k_MainLightShadowParamsId, shadowState.MainLightShadowParams);
            cmd.SetGlobalVector(k_MainLightShadowmapSizeId, shadowState.MainLightShadowmapSize);
            cmd.SetGlobalFloat(k_MainLightShadowCascadeCountId, shadowState.MainLightShadowCascadeCount);
            cmd.SetGlobalMatrixArray(k_MainLightWorldToShadowArrId, k_MainLightWorldToShadowScratch);
        }

        static EnvironmentState CaptureEnvironmentState(HSRSceneController env)
        {
            if (env == null)
                return default;

            return new EnvironmentState
            {
                GlobalOneMinusAvatarIntensity = env._GlobalOneMinusAvatarIntensity,
                MonsterLightDir = env._ES_MonsterLightDir,
                Indoor = ToShaderBool(env._ES_Indoor),
                TransitionRate = env._ES_TransitionRate,
                SelfShadowLerpHair = env._ES_SelfShadowLerpHair,
                LevelAdjustOn = ToShaderBool(env._ES_LEVEL_ADJUST_ON),
                GlobalRotMatrix = env.GetGlobalRotMatrix(),
                CharacterToonRampMode = env._ES_CharacterToonRampMode,
                CharacterDisableLocalMainLight = ToShaderBool(env._ES_CharacterDisableLocalMainLight),
                AddColor = env._ES_AddColor,
                SpColor = env._ES_SPColor,
                SpIntensity = env._ES_SPIntensity,
                RimShadowColor = env._ES_RimShadowColor,
                RimShadowIntensity = env._ES_RimShadowIntensity,
                CharacterShadowFactor = env._ES_CharacterShadowFactor,
                OutlineDarkenVal = env._ES_OutLineDarkenVal,
                OutlineLightedVal = env._ES_OutLineLightedVal,
                OutlineDisableDistanceScale = env._ES_OutlineDisableDistanceScale,
                OutlineFallbackScale = env._ES_OutlineFallbackScale,
                HeightLerpTop = env._ES_HeightLerpTop,
                HeightLerpBottom = env._ES_HeightLerpBottom,
                HeightLerpTopColor = env._ES_HeightLerpTopColor,
                HeightLerpMiddleColor = env._ES_HeightLerpMiddleColor,
                HeightLerpBottomColor = env._ES_HeightLerpBottomColor,
                RimLightOffset = env._ES_RimLightOffset,
                RimLightWidth = env._ES_RimLightWidth,
                RimLightIntensity = env._ES_RimLightIntensity,
                RimLightAddMode = env._ES_RimLightAddMode,
                RimLightMode = env._ES_RimLightMode,
                RimLightColor = env._ES_RimLightColor,
                LevelSkinLightColor = env._ES_LevelSkinLightColor,
                LevelSkinShadowColor = env._ES_LevelSkinShadowColor,
                LevelHighLightColor = env._ES_LevelHighLightColor,
                LevelShadowColor = env._ES_LevelShadowColor,
                LevelShadow = env._ES_LevelShadow,
                LevelMid = env._ES_LevelMid,
                LevelHighLight = env._ES_LevelHighLight,
                LevelEyeShadowIntensity = env._ES_LevelEyeShadowIntensity,
                IndoorCharShadowAsCookie = ToShaderBool(env._ES_IndoorCharShadowAsCookie),
                FogColor = env._ES_FogColor,
                FogDensity = env._ES_FogDensity,
                FogNear = env._ES_FogNear,
                FogFar = env._ES_FogFar,
                HeightFogColor = env._ES_HeightFogColor,
                HeightFogBaseHeight = env._ES_HeightFogBaseHeight,
                HeightFogRange = env._ES_HeightFogRange,
                HeightFogDensity = env._ES_HeightFogDensity,
                HeightFogFogNear = env._ES_HeightFogFogNear,
                HeightFogFogFar = env._ES_HeightFogFogFar,
                FogCharacterNearFactor = env._ES_FogCharacterNearFactor,
                HeightFogAddAjust = env._ES_HeightFogAddAjust,
                DisableFogTransition = ToShaderBool(env._ES_DisableFogTransition),
                EffCustomLightPosition = env._ES_EffCustomLightPosition,
                OutlineScale = env._OutlineScale
            };
        }

        static void ApplyEnvironmentState(RasterCommandBuffer cmd, in EnvironmentState environmentState)
        {
            Matrix4x4 globalRotMatrix = environmentState.GlobalRotMatrix;
            k_EsGlobalRotMatrixScratch[0] = globalRotMatrix.GetRow(0);
            k_EsGlobalRotMatrixScratch[1] = globalRotMatrix.GetRow(1);
            k_EsGlobalRotMatrixScratch[2] = globalRotMatrix.GetRow(2);
            k_EsGlobalRotMatrixScratch[3] = globalRotMatrix.GetRow(3);

            cmd.SetGlobalFloat(k_GlobalOneMinusAvatarIntensityId, environmentState.GlobalOneMinusAvatarIntensity);
            cmd.SetGlobalVector(k_XPad0Id, Vector4.zero);
            cmd.SetGlobalVector(
                k_EsMonsterLightDirId,
                new Vector4(
                    environmentState.MonsterLightDir.x,
                    environmentState.MonsterLightDir.y,
                    environmentState.MonsterLightDir.z,
                    0f));
            cmd.SetGlobalFloat(k_EsIndoorId, environmentState.Indoor);
            cmd.SetGlobalFloat(k_EsTransitionRateId, environmentState.TransitionRate);
            cmd.SetGlobalFloat(k_EsSelfShadowLerpHairId, environmentState.SelfShadowLerpHair);
            cmd.SetGlobalFloat(k_EsLevelAdjustOnId, environmentState.LevelAdjustOn);
            cmd.SetGlobalFloat(k_XPad1Id, 0f);
            cmd.SetGlobalVectorArray(k_EsGlobalRotMatrixId, k_EsGlobalRotMatrixScratch);
            cmd.SetGlobalFloat(k_EsCharacterToonRampModeId, environmentState.CharacterToonRampMode);
            cmd.SetGlobalFloat(k_EsCharacterDisableLocalMainLightId, environmentState.CharacterDisableLocalMainLight);
            cmd.SetGlobalVector(k_XPad2Id, Vector4.zero);
            cmd.SetGlobalVector(k_EsAddColorId, environmentState.AddColor);
            cmd.SetGlobalVector(k_EsSpColorId, environmentState.SpColor);
            cmd.SetGlobalFloat(k_EsSpIntensityId, environmentState.SpIntensity);
            cmd.SetGlobalVector(k_XPad3Id, Vector4.zero);
            cmd.SetGlobalVector(k_EsRimShadowColorId, environmentState.RimShadowColor);
            cmd.SetGlobalFloat(k_EsRimShadowIntensityId, environmentState.RimShadowIntensity);
            cmd.SetGlobalFloat(k_EsCharacterShadowFactorId, environmentState.CharacterShadowFactor);
            cmd.SetGlobalFloat(k_EsOutlineDarkenValId, environmentState.OutlineDarkenVal);
            cmd.SetGlobalFloat(k_EsOutlineLightedValId, environmentState.OutlineLightedVal);
            cmd.SetGlobalFloat(k_EsOutlineDisableDistanceScaleId, environmentState.OutlineDisableDistanceScale);
            cmd.SetGlobalFloat(k_EsOutlineFallbackScaleId, environmentState.OutlineFallbackScale);
            cmd.SetGlobalFloat(k_EsHeightLerpTopId, environmentState.HeightLerpTop);
            cmd.SetGlobalFloat(k_EsHeightLerpBottomId, environmentState.HeightLerpBottom);
            cmd.SetGlobalVector(k_EsHeightLerpTopColorId, environmentState.HeightLerpTopColor);
            cmd.SetGlobalVector(k_EsHeightLerpMiddleColorId, environmentState.HeightLerpMiddleColor);
            cmd.SetGlobalVector(k_EsHeightLerpBottomColorId, environmentState.HeightLerpBottomColor);
            cmd.SetGlobalVector(k_EsRimLightOffsetId, new Vector4(environmentState.RimLightOffset.x, environmentState.RimLightOffset.y, 0f, 0f));
            cmd.SetGlobalFloat(k_EsRimLightWidthId, environmentState.RimLightWidth);
            cmd.SetGlobalFloat(k_EsRimLightIntensityId, environmentState.RimLightIntensity);
            cmd.SetGlobalFloat(k_EsRimLightAddModeId, environmentState.RimLightAddMode);
            cmd.SetGlobalFloat(k_EsRimLightModeId, environmentState.RimLightMode);
            cmd.SetGlobalVector(k_XPad4Id, Vector4.zero);
            cmd.SetGlobalVector(k_EsRimLightColorId, environmentState.RimLightColor);
            cmd.SetGlobalVector(k_EsLevelSkinLightColorId, environmentState.LevelSkinLightColor);
            cmd.SetGlobalVector(k_EsLevelSkinShadowColorId, environmentState.LevelSkinShadowColor);
            cmd.SetGlobalVector(k_EsLevelHighLightColorId, environmentState.LevelHighLightColor);
            cmd.SetGlobalVector(k_EsLevelShadowColorId, environmentState.LevelShadowColor);
            cmd.SetGlobalFloat(k_EsLevelShadowId, environmentState.LevelShadow);
            cmd.SetGlobalFloat(k_EsLevelMidId, environmentState.LevelMid);
            cmd.SetGlobalFloat(k_EsLevelHighLightId, environmentState.LevelHighLight);
            cmd.SetGlobalFloat(k_EsLevelEyeShadowIntensityId, environmentState.LevelEyeShadowIntensity);
            cmd.SetGlobalFloat(k_EsIndoorCharShadowAsCookieId, environmentState.IndoorCharShadowAsCookie);
            cmd.SetGlobalFloat(k_EsFogColorId, environmentState.FogColor);
            cmd.SetGlobalFloat(k_EsFogDensityId, environmentState.FogDensity);
            cmd.SetGlobalFloat(k_EsFogNearId, environmentState.FogNear);
            cmd.SetGlobalFloat(k_EsFogFarId, environmentState.FogFar);
            cmd.SetGlobalFloat(k_EsHeightFogColorId, environmentState.HeightFogColor);
            cmd.SetGlobalFloat(k_EsHeightFogBaseHeightId, environmentState.HeightFogBaseHeight);
            cmd.SetGlobalFloat(k_EsHeightFogRangeId, environmentState.HeightFogRange);
            cmd.SetGlobalFloat(k_EsHeightFogDensityId, environmentState.HeightFogDensity);
            cmd.SetGlobalFloat(k_EsHeightFogFogNearId, environmentState.HeightFogFogNear);
            cmd.SetGlobalFloat(k_EsHeightFogFogFarId, environmentState.HeightFogFogFar);
            cmd.SetGlobalFloat(k_EsFogCharacterNearFactorId, environmentState.FogCharacterNearFactor);
            cmd.SetGlobalFloat(k_EsHeightFogAddAjustId, environmentState.HeightFogAddAjust);
            cmd.SetGlobalFloat(k_EsDisableFogTransitionId, environmentState.DisableFogTransition);
            cmd.SetGlobalVector(k_XPad5Id, Vector4.zero);
            cmd.SetGlobalVector(k_EsEffCustomLightPositionId, environmentState.EffCustomLightPosition);
            cmd.SetGlobalFloat(k_OutlineScaleId, environmentState.OutlineScale);
        }

        static float ToShaderBool(bool value)
        {
            return value ? 1f : 0f;
        }

    }
}
