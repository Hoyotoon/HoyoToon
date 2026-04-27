using HoyoToon.Runtime.Scene;
using UnityEngine;
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

        static readonly Matrix4x4[] k_MainLightWorldToShadowScratch = new Matrix4x4[5];
        static readonly Vector4[] k_EsGlobalRotMatrixScratch = new Vector4[4];
        static int s_LastAppliedFrame = -1;
        static int s_LastAppliedEnvironmentId;
        static bool s_LastAppliedClearEnvironmentWhenMissing;

        struct ShadowState
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

        struct EnvironmentState
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

        internal static void Apply(HSRSceneController env, bool clearEnvironmentWhenMissing)
        {
            int frame = Time.frameCount;
            int environmentId = env != null ? env.GetInstanceID() : 0;
            bool clearEnvironmentKey = env == null && clearEnvironmentWhenMissing;
            if (s_LastAppliedFrame == frame
                && s_LastAppliedEnvironmentId == environmentId
                && s_LastAppliedClearEnvironmentWhenMissing == clearEnvironmentKey)
            {
                return;
            }

            s_LastAppliedFrame = frame;
            s_LastAppliedEnvironmentId = environmentId;
            s_LastAppliedClearEnvironmentWhenMissing = clearEnvironmentKey;

            ApplyShadowState(CaptureShadowState());

            if (env == null)
            {
                if (clearEnvironmentWhenMissing)
                    ApplyEnvironmentState(default);

                return;
            }

            ApplyEnvironmentState(CaptureEnvironmentState(env));
        }

        static ShadowState CaptureShadowState()
        {
            float cascadeCount = Shader.GetGlobalFloat(k_MainLightShadowCascadeCountId);
            Matrix4x4[] sourceShadowMatrices = Shader.GetGlobalMatrixArray(k_MainLightWorldToShadowId);
            Matrix4x4 shadow0 = Matrix4x4.identity;
            Matrix4x4 shadow1 = Matrix4x4.identity;
            Matrix4x4 shadow2 = Matrix4x4.identity;
            Matrix4x4 shadow3 = Matrix4x4.identity;
            Matrix4x4 shadow4 = Matrix4x4.identity;

            if (sourceShadowMatrices != null && sourceShadowMatrices.Length > 0)
            {
                if (sourceShadowMatrices.Length > 0) shadow0 = sourceShadowMatrices[0];
                if (sourceShadowMatrices.Length > 1) shadow1 = sourceShadowMatrices[1];
                if (sourceShadowMatrices.Length > 2) shadow2 = sourceShadowMatrices[2];
                if (sourceShadowMatrices.Length > 3) shadow3 = sourceShadowMatrices[3];
                if (sourceShadowMatrices.Length > 4) shadow4 = sourceShadowMatrices[4];

                if (cascadeCount <= 0f)
                    cascadeCount = Mathf.Min(4, sourceShadowMatrices.Length);
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

        static void ApplyShadowState(in ShadowState shadowState)
        {
            k_MainLightWorldToShadowScratch[0] = shadowState.MainLightWorldToShadow0;
            k_MainLightWorldToShadowScratch[1] = shadowState.MainLightWorldToShadow1;
            k_MainLightWorldToShadowScratch[2] = shadowState.MainLightWorldToShadow2;
            k_MainLightWorldToShadowScratch[3] = shadowState.MainLightWorldToShadow3;
            k_MainLightWorldToShadowScratch[4] = shadowState.MainLightWorldToShadow4;

            Shader.SetGlobalVector(k_CascadeShadowSplitSpheres0Id, shadowState.CascadeShadowSplitSpheres0);
            Shader.SetGlobalVector(k_CascadeShadowSplitSpheres1Id, shadowState.CascadeShadowSplitSpheres1);
            Shader.SetGlobalVector(k_CascadeShadowSplitSpheres2Id, shadowState.CascadeShadowSplitSpheres2);
            Shader.SetGlobalVector(k_CascadeShadowSplitSpheres3Id, shadowState.CascadeShadowSplitSpheres3);
            Shader.SetGlobalVector(k_CascadeShadowSplitSphereRadiiId, shadowState.CascadeShadowSplitSphereRadii);
            Shader.SetGlobalVector(k_MainLightShadowParamsId, shadowState.MainLightShadowParams);
            Shader.SetGlobalVector(k_MainLightShadowmapSizeId, shadowState.MainLightShadowmapSize);
            Shader.SetGlobalFloat(k_MainLightShadowCascadeCountId, shadowState.MainLightShadowCascadeCount);
            Shader.SetGlobalMatrixArray(k_MainLightWorldToShadowArrId, k_MainLightWorldToShadowScratch);
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

        static void ApplyEnvironmentState(in EnvironmentState environmentState)
        {
            Matrix4x4 globalRotMatrix = environmentState.GlobalRotMatrix;
            k_EsGlobalRotMatrixScratch[0] = globalRotMatrix.GetRow(0);
            k_EsGlobalRotMatrixScratch[1] = globalRotMatrix.GetRow(1);
            k_EsGlobalRotMatrixScratch[2] = globalRotMatrix.GetRow(2);
            k_EsGlobalRotMatrixScratch[3] = globalRotMatrix.GetRow(3);

            Shader.SetGlobalFloat("_GlobalOneMinusAvatarIntensity", environmentState.GlobalOneMinusAvatarIntensity);
            Shader.SetGlobalVector("_XPad0", Vector3.zero);
            Shader.SetGlobalVector("_ES_MonsterLightDir", environmentState.MonsterLightDir);
            Shader.SetGlobalFloat("_ES_Indoor", environmentState.Indoor);
            Shader.SetGlobalFloat("_ES_TransitionRate", environmentState.TransitionRate);
            Shader.SetGlobalFloat("_ES_SelfShadowLerpHair", environmentState.SelfShadowLerpHair);
            Shader.SetGlobalFloat("_ES_LEVEL_ADJUST_ON", environmentState.LevelAdjustOn);
            Shader.SetGlobalFloat("_XPad1", 0f);
            Shader.SetGlobalVectorArray(k_EsGlobalRotMatrixId, k_EsGlobalRotMatrixScratch);
            Shader.SetGlobalFloat("_ES_CharacterToonRampMode", environmentState.CharacterToonRampMode);
            Shader.SetGlobalFloat("_ES_CharacterDisableLocalMainLight", environmentState.CharacterDisableLocalMainLight);
            Shader.SetGlobalVector("_XPad2", Vector2.zero);
            Shader.SetGlobalVector("_ES_AddColor", environmentState.AddColor);
            Shader.SetGlobalVector("_ES_SPColor", environmentState.SpColor);
            Shader.SetGlobalFloat("_ES_SPIntensity", environmentState.SpIntensity);
            Shader.SetGlobalVector("_XPad3", Vector3.zero);
            Shader.SetGlobalVector("_ES_RimShadowColor", environmentState.RimShadowColor);
            Shader.SetGlobalFloat("_ES_RimShadowIntensity", environmentState.RimShadowIntensity);
            Shader.SetGlobalFloat("_ES_CharacterShadowFactor", environmentState.CharacterShadowFactor);
            Shader.SetGlobalFloat("_ES_OutLineDarkenVal", environmentState.OutlineDarkenVal);
            Shader.SetGlobalFloat("_ES_OutLineLightedVal", environmentState.OutlineLightedVal);
            Shader.SetGlobalFloat("_ES_OutlineDisableDistanceScale", environmentState.OutlineDisableDistanceScale);
            Shader.SetGlobalFloat("_ES_OutlineFallbackScale", environmentState.OutlineFallbackScale);
            Shader.SetGlobalFloat("_ES_HeightLerpTop", environmentState.HeightLerpTop);
            Shader.SetGlobalFloat("_ES_HeightLerpBottom", environmentState.HeightLerpBottom);
            Shader.SetGlobalVector("_ES_HeightLerpTopColor", environmentState.HeightLerpTopColor);
            Shader.SetGlobalVector("_ES_HeightLerpMiddleColor", environmentState.HeightLerpMiddleColor);
            Shader.SetGlobalVector("_ES_HeightLerpBottomColor", environmentState.HeightLerpBottomColor);
            Shader.SetGlobalVector("_ES_RimLightOffset", environmentState.RimLightOffset);
            Shader.SetGlobalFloat("_ES_RimLightWidth", environmentState.RimLightWidth);
            Shader.SetGlobalFloat("_ES_RimLightIntensity", environmentState.RimLightIntensity);
            Shader.SetGlobalFloat("_ES_RimLightAddMode", environmentState.RimLightAddMode);
            Shader.SetGlobalFloat("_ES_RimLightMode", environmentState.RimLightMode);
            Shader.SetGlobalVector("_XPad4", Vector2.zero);
            Shader.SetGlobalVector("_ES_RimLightColor", environmentState.RimLightColor);
            Shader.SetGlobalVector("_ES_LevelSkinLightColor", environmentState.LevelSkinLightColor);
            Shader.SetGlobalVector("_ES_LevelSkinShadowColor", environmentState.LevelSkinShadowColor);
            Shader.SetGlobalVector("_ES_LevelHighLightColor", environmentState.LevelHighLightColor);
            Shader.SetGlobalVector("_ES_LevelShadowColor", environmentState.LevelShadowColor);
            Shader.SetGlobalFloat("_ES_LevelShadow", environmentState.LevelShadow);
            Shader.SetGlobalFloat("_ES_LevelMid", environmentState.LevelMid);
            Shader.SetGlobalFloat("_ES_LevelHighLight", environmentState.LevelHighLight);
            Shader.SetGlobalFloat("_ES_LevelEyeShadowIntensity", environmentState.LevelEyeShadowIntensity);
            Shader.SetGlobalFloat("_ES_IndoorCharShadowAsCookie", environmentState.IndoorCharShadowAsCookie);
            Shader.SetGlobalFloat("_ES_FogColor", environmentState.FogColor);
            Shader.SetGlobalFloat("_ES_FogDensity", environmentState.FogDensity);
            Shader.SetGlobalFloat("_ES_FogNear", environmentState.FogNear);
            Shader.SetGlobalFloat("_ES_FogFar", environmentState.FogFar);
            Shader.SetGlobalFloat("_ES_HeightFogColor", environmentState.HeightFogColor);
            Shader.SetGlobalFloat("_ES_HeightFogBaseHeight", environmentState.HeightFogBaseHeight);
            Shader.SetGlobalFloat("_ES_HeightFogRange", environmentState.HeightFogRange);
            Shader.SetGlobalFloat("_ES_HeightFogDensity", environmentState.HeightFogDensity);
            Shader.SetGlobalFloat("_ES_HeightFogFogNear", environmentState.HeightFogFogNear);
            Shader.SetGlobalFloat("_ES_HeightFogFogFar", environmentState.HeightFogFogFar);
            Shader.SetGlobalFloat("_ES_FogCharacterNearFactor", environmentState.FogCharacterNearFactor);
            Shader.SetGlobalFloat("_ES_HeightFogAddAjust", environmentState.HeightFogAddAjust);
            Shader.SetGlobalFloat("_ES_DisableFogTransition", environmentState.DisableFogTransition);
            Shader.SetGlobalVector("_XPad5", Vector2.zero);
            Shader.SetGlobalVector("_ES_EffCustomLightPosition", environmentState.EffCustomLightPosition);
            Shader.SetGlobalFloat("_OutlineScale", environmentState.OutlineScale);
        }

        static float ToShaderBool(bool value)
        {
            return value ? 1f : 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFrameCache()
        {
            s_LastAppliedFrame = -1;
            s_LastAppliedEnvironmentId = 0;
            s_LastAppliedClearEnvironmentWhenMissing = false;
        }
    }
}
