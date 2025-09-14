// Copyright (c) Jason Ma

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LWGUI.Runtime.LwguiGradient
{
    [Serializable]
    public class LwguiGradient : IDisposable
    {
        #region Channel Enum
        
        public enum Channel
        {
            Red     = 0,
            Green   = 1,
            Blue    = 2,
            Alpha   = 3,
            Num     = 4
        }

        [Flags] // Flags Attribute must be used to support bit operations
        public enum ChannelMask
        {
            None    = 0,
            Red     = 1 << 0,
            Green   = 1 << 1,
            Blue    = 1 << 2,
            Alpha   = 1 << 3,
            RGB     = Red | Green | Blue,
            All     = ~0
        }
        
        public enum GradientTimeRange
        {
            One                 = 1,
            TwentyFour          = 24,
            TwentyFourHundred   = 2400
        }

        public static bool HasChannelMask(ChannelMask channelMaskA, ChannelMask channelMaskB) => ((uint)channelMaskA & (uint)channelMaskB) > 0;
        
        public static bool IsChannelIndexInMask(int channelIndex, ChannelMask channelMask) => ((uint)channelMask & (uint)(1 << channelIndex)) > 0;
        
        public static ChannelMask ChannelIndexToMask(int channelIndex) => (ChannelMask)(1 << channelIndex);
        
        #endregion

        #region Const

        public static readonly Color[] channelColors = new[] { Color.red, Color.green, Color.blue, Color.white };
        public static readonly char[] channelNames = new[] { 'r', 'g', 'b', 'a' };

        public static AnimationCurve defaultCurve => new (new Keyframe(0, 1).SetLinearTangentMode(), new Keyframe(1, 1).SetLinearTangentMode());

        #endregion

        #region Data

        // The complete data is stored by RGBA Curves and can be converted into Texture
        [SerializeField] private List<AnimationCurve> _curves;
        
        public List<AnimationCurve> rawCurves
        {
            get
            {
                _curves ??= new List<AnimationCurve>();
                if (_curves.Count < (int)Channel.Num)
                {
                    for (int c = 0; c < (int)Channel.Num; c++)
                    {
                        if (c == _curves.Count)
                            _curves.Add(defaultCurve);
                    }
                }

                return _curves;
            }
            set => SetRgbaCurves(value);
        }

        public AnimationCurve redCurve
        {
            get => rawCurves[(int)Channel.Red] ?? defaultCurve;
            set => SetCurve(value, ChannelMask.Red);
        }

        public AnimationCurve greenCurve
        {
            get => rawCurves[(int)Channel.Green] ?? defaultCurve;
            set => SetCurve(value, ChannelMask.Green);
        }

        public AnimationCurve blueCurve
        {
            get => rawCurves[(int)Channel.Blue] ?? defaultCurve;
            set => SetCurve(value, ChannelMask.Blue);
        }

        public AnimationCurve alphaCurve
        {
            get => rawCurves[(int)Channel.Alpha] ?? defaultCurve;
            set => SetCurve(value, ChannelMask.Alpha);
        }

        #endregion

        #region Constructor

        public LwguiGradient()
        {
            _curves = new List<AnimationCurve>();
            for (int c = 0; c < (int)Channel.Num; c++)
                _curves.Add(defaultCurve);
        }

        public LwguiGradient(LwguiGradient src)
        {
            DeepCopyFrom(src);
        }

        public LwguiGradient(params Keyframe[] keys)
        {
            _curves = new List<AnimationCurve>();
            for (int c = 0; c < (int)Channel.Num; c++)
                _curves.Add(new AnimationCurve());
            
            if (keys?.Length > 0)
            {
                AddKeys(keys, ChannelMask.All);
            }
        }

        public LwguiGradient(Color[] colors, float[] times)
        {
            _curves = new List<AnimationCurve>();
            for (int c = 0; c < (int)Channel.Num; c++)
                _curves.Add(new AnimationCurve());
            
            if (colors == null || times == null)
                return;

            for (int i = 0; i < Mathf.Min(colors.Length, times.Length); i++)
            {
                for (int c = 0; c < (int)Channel.Num; c++)
                {
                    _curves[c].AddKey(new Keyframe(times[i], colors[i][c]).SetLinearTangentMode());
                }
            }
            SetLinearTangentMode();
        }

        public LwguiGradient(List<AnimationCurve> inRgbaCurves) => SetRgbaCurves(inRgbaCurves);

        public static LwguiGradient white
        {
	        get => new ();
        }

        public static LwguiGradient gray
        {
	        get => new (new []{Color.gray, Color.gray}, new []{0.0f, 1.0f});
        }

        public static LwguiGradient black
        {
	        get => new (new []{Color.black, Color.black}, new []{0.0f, 1.0f});
        }

        public static LwguiGradient red
        {
	        get => new (new []{Color.red, Color.red}, new []{0.0f, 1.0f});
        }

        public static LwguiGradient green
        {
	        get => new (new []{Color.green, Color.green}, new []{0.0f, 1.0f});
        }

        public static LwguiGradient blue
        {
	        get => new (new []{Color.blue, Color.blue}, new []{0.0f, 1.0f});
        }

        public static LwguiGradient cyan
        {
	        get => new (new []{Color.cyan, Color.cyan}, new []{0.0f, 1.0f});
        }

        public static LwguiGradient magenta
        {
	        get => new (new []{Color.magenta, Color.magenta}, new []{0.0f, 1.0f});
        }

        public static LwguiGradient yellow
        {
	        get => new (new []{Color.yellow, Color.yellow}, new []{0.0f, 1.0f});
        }

        #endregion

        public int GetValueBasedHashCode()
        {
            var hash = 17;

            if (_curves != null)
            {
                foreach (var curve in _curves)
                {
                    if (curve != null)
                    {
                        hash = hash * 23 + curve.GetHashCode();
                    }
                }
            }

            return hash;
        }
        
        public void Dispose()
        {
            _curves?.Clear();
        }
        
        public void Clear(ChannelMask channelMask = ChannelMask.All)
        {
            _curves ??= new List<AnimationCurve>();
            for (int c = 0; c < (int)Channel.Num; c++)
            {
                if (!IsChannelIndexInMask(c, channelMask)) continue;
                
                if (_curves.Count > c) _curves[c].keys = Array.Empty<Keyframe>();
                else _curves.Add(new AnimationCurve());
            }
        }
        
        public void DeepCopyFrom(LwguiGradient src)
        {
            _curves ??= new List<AnimationCurve>();
            for (int c = 0; c < (int)Channel.Num; c++)
            {
                if (_curves.Count == c)
                    _curves.Add(new AnimationCurve());

                _curves[c].keys = Array.Empty<Keyframe>();
            }

            for (int c = 0; c < src._curves.Count; c++)
            {
                foreach (var key in src._curves[c].keys)
                {
                    _curves[c].AddKey(key);
                }
            }
        }

        public void SetCurve(AnimationCurve curve, ChannelMask channelMask)
        {
            curve ??= defaultCurve;
            for (int c = 0; c < (int)Channel.Num; c++)
            {
                if (!IsChannelIndexInMask(c, channelMask)) continue;

                _curves[c] = curve;
            }
        }
        
        public void SetRgbaCurves(List<AnimationCurve> inRgbaCurves)
        {
            _curves = new List<AnimationCurve>();
            
            for (int c = 0; c < (int)Channel.Num; c++)
            {
                if (inRgbaCurves?.Count > c && inRgbaCurves[c]?.keys.Length > 0)
                {
                    _curves.Add(inRgbaCurves[c]);
                }
                else
                {
                    _curves.Add(defaultCurve);
                }
            }
        }

        public void AddKey(Keyframe key, ChannelMask channelMask)
        {
            for (int c = 0; c < (int)Channel.Num; c++)
            {
                if (!IsChannelIndexInMask(c, channelMask))
                    continue;
                
                rawCurves[c].AddKey(key);
            }

        }

        public void AddKeys(Keyframe[] keys, ChannelMask channelMask)
        {
            for (int i = 0; i < keys?.Length; i++)
            {
                AddKey(keys[i], channelMask);
            }
        }

        public Color Evaluate(float time, ChannelMask channelMask = ChannelMask.All, GradientTimeRange timeRange = GradientTimeRange.One)
        {
            time /= (int)timeRange;
            
            if (channelMask == ChannelMask.Alpha)
            {
                var alpha = rawCurves[(int)Channel.Alpha].Evaluate(time);
                return new Color(alpha, alpha, alpha, 1);
            }

            return new Color(
                 IsChannelIndexInMask((int)Channel.Red, channelMask)     ? rawCurves[(int)Channel.Red].Evaluate(time)     : 0,
                 IsChannelIndexInMask((int)Channel.Green, channelMask)   ? rawCurves[(int)Channel.Green].Evaluate(time)   : 0,
                 IsChannelIndexInMask((int)Channel.Blue, channelMask)    ? rawCurves[(int)Channel.Blue].Evaluate(time)    : 0,
                 IsChannelIndexInMask((int)Channel.Alpha, channelMask)   ? rawCurves[(int)Channel.Alpha].Evaluate(time)   : 1);
        }

        public void SetLinearTangentMode()
        {
            for (int c = 0; c < (int)Channel.Num; c++)
            {
                rawCurves[c].SetLinearTangents();
            }
        }

        #region LwguiGradient <=> Ramp Texture

        public Color[] GetPixels(int width, int height, ChannelMask channelMask = ChannelMask.All)
        {
            var pixels = new Color[width * height];
            for (var x = 0; x < width; x++)
            {
                var u   = x / (float)width;
                var col = Evaluate(u, channelMask);
                for (int i = 0; i < height; i++)
                {
                    pixels[x + i * width] = col;
                }
            }

            return pixels;
        }

        public void GetPixels(ref Color[] outputPixels, ref int currentIndex, int width, int height, ChannelMask channelMask = ChannelMask.All)
        {
            if (outputPixels == null || currentIndex >= outputPixels.Length)
                return;
            
            for (var x = 0; x < width; x++)
            {
                var u   = x / (float)width;
                var col = Evaluate(u, channelMask);
                for (int i = 0; i < height; i++)
                {
                    if (currentIndex < outputPixels.Length)
                        outputPixels[currentIndex ++] = col;
                }
            }
        }

        public Texture2D GetPreviewRampTexture(int width = 256, int height = 1, ColorSpace colorSpace = ColorSpace.Gamma, ChannelMask channelMask = ChannelMask.All)
        {
            if (LwguiGradientHelper.TryGetRampPreview(this, width, height, colorSpace, channelMask, out var cachedPreview)) 
                return cachedPreview;
            
            var rampPreview   = new Texture2D(width, height, TextureFormat.RGBA32, false, colorSpace == ColorSpace.Linear);
            var pixels = GetPixels(width, height, channelMask);
            rampPreview.SetPixels(pixels);
            rampPreview.wrapMode = TextureWrapMode.Clamp;
            rampPreview.name = "LWGUI Gradient Preview";
            rampPreview.Apply();
            
            LwguiGradientHelper.SetRampPreview(this, width, height, colorSpace, channelMask, rampPreview);
            return rampPreview;
        }

        #endregion

        #region LwguiGradient <=> Gradient
        
        public struct LwguiKeyframe
        {
            public float time;
            public float value;
            public int index;

            public LwguiKeyframe(float time, float value, int index)
            {
                this.time  = time;
                this.value = value;
                this.index = index;
            }
        }

        public class LwguiMergedColorCurves : IDisposable
        {
            public List<List<LwguiKeyframe>> curves = new ();

            public LwguiMergedColorCurves()
            {
                for (int c = 0; c < (int)Channel.Num; c++)
                    curves.Add(new List<LwguiKeyframe>());
            }

            public LwguiMergedColorCurves(List<AnimationCurve> rgbaCurves)
            {
                for (int c = 0; c < (int)Channel.Num; c++)
                    curves.Add(new List<LwguiKeyframe>());
                
                // Get color keys
                {
                    var timeColorDic = new Dictionary<float, List<(float value, int index)>>();
                    for (int c = 0; c < (int)Channel.Num - 1; c++)
                    {
                        var keys = rgbaCurves[c].keys;
                        for (int j = 0; j < keys.Length; j++)
                        {
                            var keyframe = keys[j];
                            if (timeColorDic.ContainsKey(keyframe.time))
                            {
                                timeColorDic[keyframe.time].Add((keyframe.value, j));
                            }
                            else
                            {
                                timeColorDic.Add(keyframe.time, new List<(float value, int index)> { (keyframe.value, j) });
                            }
                        }
                    }

                    foreach (var kwPair in timeColorDic)
                    {
                        if (kwPair.Value.Count == (int)Channel.Num - 1)
                        {
                            for (int c = 0; c < (int)Channel.Num - 1; c++)
                            {
                                curves[c].Add(new LwguiKeyframe(kwPair.Key, kwPair.Value[c].value, kwPair.Value[c].index));
                            }
                        }
                    }
                }
            
                // Get alpha keys
                for (int i = 0; i < rgbaCurves[(int)Channel.Alpha].keys.Length; i++)
                {
                    var alphaKey = rgbaCurves[(int)Channel.Alpha].keys[i];
                    curves[(int)Channel.Alpha].Add(new LwguiKeyframe(alphaKey.time, alphaKey.value, i));
                }
            }

            public LwguiMergedColorCurves(Gradient gradient)
            {
                for (int c = 0; c < (int)Channel.Num; c++)
                    curves.Add(new List<LwguiKeyframe>());

                foreach (var colorKey in gradient.colorKeys)
                {
                    for (int c = 0; c < (int)Channel.Num - 1; c++)
                    {
                        curves[c].Add(new LwguiKeyframe(colorKey.time, colorKey.color[c], 0));
                    }
                }
                foreach (var alphaKey in gradient.alphaKeys)
                {
                    curves[(int)Channel.Alpha].Add(new LwguiKeyframe(alphaKey.time, alphaKey.alpha, 0));
                }
            }

            public Gradient ToGradient(int maxGradientKeyCount = 8) => new Gradient
            {
                colorKeys = curves[(int)Channel.Red].Select((keyframe, i) => new GradientColorKey(
                    new Color(
                        curves[(int)Channel.Red][i].value,
                        curves[(int)Channel.Green][i].value,
                        curves[(int)Channel.Blue][i].value), 
                    curves[(int)Channel.Red][i].time))
                    .Where((key, i) => i < maxGradientKeyCount).ToArray(),
                    
                alphaKeys = curves[(int)Channel.Alpha].Select(alphaKey => new GradientAlphaKey(alphaKey.value, alphaKey.time))
                    .Where((key, i) => i < maxGradientKeyCount).ToArray()
            };

            public List<AnimationCurve> ToAnimationCurves()
            {
                var outCurves = new List<AnimationCurve>();
                for (int c = 0; c < (int)Channel.Num; c++)
                {
                    var curve = new AnimationCurve();
                    foreach (var key in curves[c])
                    {
                        curve.AddKey(new Keyframe(key.time, key.value).SetLinearTangentMode());
                    }
                    curve.SetLinearTangents();
                    outCurves.Add(curve);
                }

                return outCurves;
            }

            public LwguiGradient ToLwguiGradient()
            {
                return new LwguiGradient(ToAnimationCurves());
            }

            public void Dispose()
            {
                curves?.Clear();
            }
        }

        public static LwguiGradient FromGradient(Gradient gradient)
        {
            return new LwguiMergedColorCurves(gradient).ToLwguiGradient();
        }

        public Gradient ToGradient(int maxGradientKeyCount = 8)
        {
            return new LwguiMergedColorCurves(rawCurves).ToGradient(maxGradientKeyCount);
        }

        #endregion

        #region Gamma <=> Linear

        public LwguiGradient ConvertColorSpaceWithoutCopy(ColorSpace targetColorSpace)
        {
            for (int c = 0; c < (int)Channel.Num; c++)
            {
                if (c != (int)Channel.Alpha)
                {
                    var keys = rawCurves[c].keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (targetColorSpace == ColorSpace.Gamma)
                            keys[i].value = Mathf.LinearToGammaSpace(keys[i].value);
                        else
                            keys[i].value = Mathf.GammaToLinearSpace(keys[i].value);
                    }

                    rawCurves[c].keys = keys;
                }
            }

            return this;
        }

        public LwguiGradient gamma => new LwguiGradient(this).ConvertColorSpaceWithoutCopy(ColorSpace.Gamma);
        
        public LwguiGradient linear => new LwguiGradient(this).ConvertColorSpaceWithoutCopy(ColorSpace.Linear);

        #endregion
    }
}