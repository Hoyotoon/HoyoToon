// Copyright (c) Jason Ma

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Disable Keyframe.tangentMode obsolete warning
#pragma warning disable 0618


namespace LWGUI.Runtime.LwguiGradient
{
    public static class LwguiGradientHelper
    {
        #region Extended Methods

        public static Keyframe SetLinearTangentMode(this Keyframe key)
        {
            key.tangentMode = 69;
            return key;
        }

        public static void SetLinearTangents(this AnimationCurve curve)
        {
            for (int i = 1; i < curve.keys.Length; i++)
            {
                var keyStart = curve.keys[i - 1];
                var keyEnd = curve.keys[i];
                float tangent = (keyEnd.value - keyStart.value) / (keyEnd.time - keyStart.time);
                keyStart.outTangent = tangent;
                keyEnd.inTangent = tangent;
                curve.MoveKey(i - 1, keyStart);
                curve.MoveKey(i, keyEnd);
            }
        }

        public static void DestroyInEditorOrRuntime(this UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(obj);
#else
            Object.Destroy(obj);
#endif
        }
        
        #endregion

        #region Ramp Preview Caches
        
        private static Dictionary<int/* LwguiGradient Value Based Hash */, 
            Dictionary<(int, int, ColorSpace, LwguiGradient.ChannelMask), Texture2D>> _gradientToPreviewDic;

        public static bool TryGetRampPreview(LwguiGradient gradient, int width, int height, ColorSpace colorSpace, LwguiGradient.ChannelMask channelMask, 
            out Texture2D cachedPreview)
        {
            cachedPreview = _gradientToPreviewDic?.GetValueOrDefault(gradient.GetValueBasedHashCode())?.GetValueOrDefault((width, height, colorSpace, channelMask));
            return cachedPreview;
        }
        
        public static void SetRampPreview(LwguiGradient gradient, int width, int height, ColorSpace colorSpace, LwguiGradient.ChannelMask channelMask, Texture2D newPreviewTex)
        {
            _gradientToPreviewDic ??= new Dictionary<int, Dictionary<(int, int, ColorSpace, LwguiGradient.ChannelMask), Texture2D>>();
            var hash = gradient.GetValueBasedHashCode();
            
            if (!_gradientToPreviewDic.ContainsKey(hash))
            {
                _gradientToPreviewDic[hash] = new Dictionary<(int, int, ColorSpace, LwguiGradient.ChannelMask), Texture2D>();
            }

            if (_gradientToPreviewDic[hash].ContainsKey((width, height, colorSpace, channelMask)))
            {
                _gradientToPreviewDic[hash][(width, height, colorSpace, channelMask)].DestroyInEditorOrRuntime();
            }
            
            _gradientToPreviewDic[hash][(width, height, colorSpace, channelMask)] = newPreviewTex;
        }

        public static void ClearRampPreviewCaches()
        {
            if (_gradientToPreviewDic == null) return;
            
            foreach (var paramsToPreviewTexDic in _gradientToPreviewDic.Values)
            {
                paramsToPreviewTexDic.Values.ToList().ForEach(previewTex => previewTex.DestroyInEditorOrRuntime());
            }
            _gradientToPreviewDic.Clear();
        }
        
        #endregion
    }
}