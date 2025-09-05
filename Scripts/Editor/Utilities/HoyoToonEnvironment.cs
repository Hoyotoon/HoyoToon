#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon
{
    /// <summary>
    /// Rich environment snapshot and helpers for PlayerSettings, Build settings, Quality, and Graphics.
    /// Central place for conditional config logic to query the current project state.
    /// </summary>
    public static class HoyoToonEnvironment
    {
        [Serializable]
        public struct Snapshot
        {
            // Build context
            public BuildTarget ActiveBuildTarget;
            public BuildTargetGroup ActiveBuildTargetGroup;
            public string[] ScriptingDefineSymbols;

            // Player settings
            public ColorSpace ColorSpace;
            public ScriptingImplementation ScriptingBackend;
            public ApiCompatibilityLevel ApiCompatibilityLevel;
            public bool RunInBackground;
            public bool AllowFullscreenSwitch;
            public bool ResizableWindow;
            public bool VisibleInBackground;
            public bool UseMacAppStoreValidation;
            public bool Use32BitDisplayBuffer;

            // Company/Product
            public string CompanyName;
            public string ProductName;
            public string ApplicationIdentifier;

            // Graphics
            public bool UseDefaultGraphicsAPIs;
            public string[] GraphicsAPIs; // friendly names (e.g., "Direct3D11")
            public int DefaultScreenWidth;
            public int DefaultScreenHeight;
            public int DefaultScreenRefreshRate;

            // Quality
            public string ActiveQualityName;
            public int VSyncCount;
            public int AntiAliasing;
            public ShadowProjection ShadowProjection;

            // Editor flags (for context only)
            public bool IsBatchMode;
        }

        public static Snapshot Capture()
        {
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            var activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);

            var snap = new Snapshot
            {
                ActiveBuildTarget = activeTarget,
                ActiveBuildTargetGroup = activeGroup,
                ScriptingDefineSymbols = (PlayerSettings.GetScriptingDefineSymbolsForGroup(activeGroup) ?? string.Empty)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToArray(),

                // Player
                ColorSpace = PlayerSettings.colorSpace,
                ScriptingBackend = PlayerSettings.GetScriptingBackend(activeGroup),
                ApiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(activeGroup),
                RunInBackground = PlayerSettings.runInBackground,
                AllowFullscreenSwitch = PlayerSettings.fullScreenMode == FullScreenMode.ExclusiveFullScreen || PlayerSettings.fullScreenMode == FullScreenMode.FullScreenWindow,
                ResizableWindow = PlayerSettings.resizableWindow,
                VisibleInBackground = PlayerSettings.visibleInBackground,
                UseMacAppStoreValidation = SafeGet(() => PlayerSettings.useMacAppStoreValidation, false),
                Use32BitDisplayBuffer = SafeGet(() => PlayerSettings.use32BitDisplayBuffer, false),

                CompanyName = Application.companyName,
                ProductName = Application.productName,
                ApplicationIdentifier = PlayerSettings.applicationIdentifier,

                // Graphics
                UseDefaultGraphicsAPIs = SafeGetUseDefaultGraphicsAPIs(activeTarget),
                GraphicsAPIs = SafeGetGraphicsAPIs(activeTarget),
                DefaultScreenWidth = PlayerSettings.defaultScreenWidth,
                DefaultScreenHeight = PlayerSettings.defaultScreenHeight,
                DefaultScreenRefreshRate = SafeGet(() => GetCurrentRefreshRate(), 0),

                // Quality
                ActiveQualityName = QualitySettings.names != null && QualitySettings.names.Length > QualitySettings.GetQualityLevel() ?
                    QualitySettings.names[QualitySettings.GetQualityLevel()] : "",
                VSyncCount = QualitySettings.vSyncCount,
                AntiAliasing = QualitySettings.antiAliasing,
                ShadowProjection = QualitySettings.shadowProjection,

                IsBatchMode = Application.isBatchMode
            };

            return snap;
        }

        public static void LogSummary(Snapshot s)
        {
            try
            {
                HoyoToonLogger.ManagerInfo(
                    $"Env: Target={s.ActiveBuildTarget} Group={s.ActiveBuildTargetGroup} | ColorSpace={s.ColorSpace} | " +
                    $"Scripting={s.ScriptingBackend} APICompat={s.ApiCompatibilityLevel} | Quality={s.ActiveQualityName} VSync={s.VSyncCount} AA={s.AntiAliasing} | " +
                    $"GraphicsAPIs={(s.UseDefaultGraphicsAPIs ? "(Default)" : string.Join(", ", s.GraphicsAPIs ?? Array.Empty<string>()))}");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Manager", $"HoyoToonEnvironment.LogSummary exception: {ex}", LogType.Exception);
            }
        }

        public static string ToPrettyString(Snapshot s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Environment Snapshot");
            sb.AppendLine();
            sb.AppendLine($"**Build Target:** {s.ActiveBuildTarget} ({s.ActiveBuildTargetGroup})  ");
            sb.AppendLine($"**Defines:** {string.Join(", ", s.ScriptingDefineSymbols ?? Array.Empty<string>())}  ");
            sb.AppendLine($"**Company/Product:** {s.CompanyName} / {s.ProductName}  ");
            sb.AppendLine($"**App Identifier:** {s.ApplicationIdentifier}  ");
            sb.AppendLine();
            sb.AppendLine("## Player Settings");
            sb.AppendLine($"- **Color Space:** {s.ColorSpace}");
            sb.AppendLine($"- **Scripting Backend:** {s.ScriptingBackend}");
            sb.AppendLine($"- **API Compatibility:** {s.ApiCompatibilityLevel}");
            sb.AppendLine($"- **Run In Background:** {s.RunInBackground}");
            sb.AppendLine($"- **Visible In Background:** {s.VisibleInBackground}");
            sb.AppendLine($"- **Resizable Window:** {s.ResizableWindow}");
            sb.AppendLine($"- **Allow Fullscreen Switch:** {s.AllowFullscreenSwitch}");
            sb.AppendLine($"- **Use 32-bit Display Buffer:** {s.Use32BitDisplayBuffer}");
            sb.AppendLine();
            sb.AppendLine("## Graphics");
            sb.AppendLine($"- **Use Default Graphics APIs:** {s.UseDefaultGraphicsAPIs}");
            sb.AppendLine($"- **Graphics APIs:** {(s.UseDefaultGraphicsAPIs ? "*Default*" : string.Join(", ", s.GraphicsAPIs ?? Array.Empty<string>()))}");
            sb.AppendLine($"- **Default Screen:** {s.DefaultScreenWidth}x{s.DefaultScreenHeight} @ {s.DefaultScreenRefreshRate}Hz");
            sb.AppendLine();
            sb.AppendLine("## Quality");
            sb.AppendLine($"- **Active Quality:** {s.ActiveQualityName}");
            sb.AppendLine($"- **VSync Count:** {s.VSyncCount}");
            sb.AppendLine($"- **Anti-Aliasing:** {s.AntiAliasing}");
            sb.AppendLine($"- **Shadow Projection:** {s.ShadowProjection}");
            sb.AppendLine();
            sb.AppendLine("## Editor");
            sb.AppendLine($"- **Batch Mode:** {s.IsBatchMode}");
            return sb.ToString();
        }

        // Optional: JSON for diagnostics/export (Editor-only simple)
        public static string ToJson(Snapshot s)
        {
            try { return JsonUtility.ToJson(s, true); }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Manager", $"HoyoToonEnvironment.ToJson exception: {ex}", LogType.Exception);
                return string.Empty;
            }
        }

        // ===================== Full Reflective Dump =====================
        [Serializable]
        public class KeyValue
        {
            public string Key;
            public string Value;
        }

        [Serializable]
        public class GroupDump
        {
            public string Group; // BuildTargetGroup name
            public KeyValue[] Items;
        }

        [Serializable]
        public class TargetDump
        {
            public string Target; // BuildTarget name
            public KeyValue[] Items;
        }

        [Serializable]
        public class FullDump
        {
            public KeyValue[] PlayerSettings;
            public KeyValue[] QualitySettings;
            public KeyValue[] Application;
            public KeyValue[] EditorUserBuildSettings;
            public KeyValue[] BuildPipeline;

            public GroupDump[] ByBuildTargetGroup;
            public TargetDump[] ByBuildTarget;
        }

        /// <summary>
        /// Reflectively dumps most public static properties/fields from common Unity settings classes,
        /// plus group/target-specific getters. Values are stringified for safe JSON serialization.
        /// </summary>
        public static FullDump DumpAll()
        {
            var dump = new FullDump
            {
                PlayerSettings = ReflectStatic(typeof(PlayerSettings)),
                QualitySettings = ReflectStatic(typeof(QualitySettings)),
                Application = ReflectStatic(typeof(Application)),
                EditorUserBuildSettings = ReflectStatic(typeof(EditorUserBuildSettings)),
                BuildPipeline = ReflectStatic(typeof(BuildPipeline)),
                ByBuildTargetGroup = DumpPerGroup(),
                ByBuildTarget = DumpPerTarget()
            };
            return dump;
        }

        /// <summary>
        /// JSON for FullDump using JsonUtility.
        /// </summary>
        public static string DumpAllAsJson()
        {
            try { return JsonUtility.ToJson(DumpAll(), true); }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Manager", $"HoyoToonEnvironment.DumpAllAsJson exception: {ex}", LogType.Exception);
                return string.Empty;
            }
        }

        // ---- Reflection helpers ----
        private static KeyValue[] ReflectStatic(Type t)
        {
            var list = new System.Collections.Generic.List<KeyValue>();
            try
            {
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

                foreach (var p in t.GetProperties(flags))
                {
                    try
                    {
                        if (!p.CanRead) continue;
                        if (p.GetIndexParameters().Length != 0) continue; // skip indexers
                        object val = p.GetValue(null, null);
                        list.Add(new KeyValue { Key = $"{t.Name}.{p.Name}", Value = SafeString(val) });
                    }
                    catch (Exception ex)
                    {
                        list.Add(new KeyValue { Key = $"{t.Name}.{p.Name}", Value = $"(error: {Short(ex)})" });
                    }
                }

                foreach (var f in t.GetFields(flags))
                {
                    try
                    {
                        object val = f.GetValue(null);
                        list.Add(new KeyValue { Key = $"{t.Name}.{f.Name}", Value = SafeString(val) });
                    }
                    catch (Exception ex)
                    {
                        list.Add(new KeyValue { Key = $"{t.Name}.{f.Name}", Value = $"(error: {Short(ex)})" });
                    }
                }
            }
            catch (Exception ex)
            {
                list.Add(new KeyValue { Key = $"{t.Name}.*", Value = $"(reflection error: {Short(ex)})" });
            }
            return list.ToArray();
        }

        private static GroupDump[] DumpPerGroup()
        {
            var groups = (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup));
            var result = new System.Collections.Generic.List<GroupDump>();

            foreach (var g in groups)
            {
                if (g == BuildTargetGroup.Unknown) continue;
                var items = new System.Collections.Generic.List<KeyValue>();
                try
                {
                    // Defines
                    string defs = SafeGet(() => PlayerSettings.GetScriptingDefineSymbolsForGroup(g), string.Empty);
                    items.Add(new KeyValue { Key = $"Defines[{g}]", Value = defs });
                    // Scripting backend
                    var backend = SafeGet(() => PlayerSettings.GetScriptingBackend(g), ScriptingImplementation.Mono2x);
                    items.Add(new KeyValue { Key = $"ScriptingBackend[{g}]", Value = backend.ToString() });
                    // API compat
                    var api = SafeGet(() => PlayerSettings.GetApiCompatibilityLevel(g), ApiCompatibilityLevel.NET_Unity_4_8);
                    items.Add(new KeyValue { Key = $"ApiCompatibility[{g}]", Value = api.ToString() });
                }
                catch (Exception ex)
                {
                    items.Add(new KeyValue { Key = $"Group[{g}]", Value = $"(error: {Short(ex)})" });
                }
                result.Add(new GroupDump { Group = g.ToString(), Items = items.ToArray() });
            }
            return result.ToArray();
        }

        private static TargetDump[] DumpPerTarget()
        {
            var targets = (BuildTarget[])Enum.GetValues(typeof(BuildTarget));
            var result = new System.Collections.Generic.List<TargetDump>();
            foreach (var t in targets)
            {
                var items = new System.Collections.Generic.List<KeyValue>();
                try
                {
                    bool def = SafeGetUseDefaultGraphicsAPIs(t);
                    items.Add(new KeyValue { Key = $"UseDefaultGraphicsAPIs[{t}]", Value = def.ToString() });
                    var apis = SafeGetGraphicsAPIs(t);
                    items.Add(new KeyValue { Key = $"GraphicsAPIs[{t}]", Value = (def ? "(Default)" : string.Join(", ", apis)) });
                }
                catch (Exception ex)
                {
                    items.Add(new KeyValue { Key = $"Target[{t}]", Value = $"(error: {Short(ex)})" });
                }
                result.Add(new TargetDump { Target = t.ToString(), Items = items.ToArray() });
            }
            return result.ToArray();
        }

        private static string SafeString(object val)
        {
            if (val == null) return "null";
            try
            {
                switch (val)
                {
                    case Array arr:
                        var parts = new System.Collections.Generic.List<string>();
                        foreach (var e in arr) parts.Add(e != null ? e.ToString() : "null");
                        return "[" + string.Join(", ", parts) + "]";
                    default:
                        return val.ToString();
                }
            }
            catch { return "(unprintable)"; }
        }

        private static string Short(Exception ex)
        {
            try { return ex.GetType().Name + ": " + ex.Message; }
            catch { return "Exception"; }
        }

        // --- Helpers -------------------------------------------------------
        private static T SafeGet<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch { return fallback; }
        }

        // Avoid direct use of obsolete Resolution.refreshRate by accessing refreshRateRatio via reflection.
        private static int GetCurrentRefreshRate()
        {
            try
            {
                var res = Screen.currentResolution;
                var ratioProp = typeof(Resolution).GetProperty("refreshRateRatio");
                if (ratioProp != null)
                {
                    var ratio = ratioProp.GetValue(res, null);
                    if (ratio != null)
                    {
                        var valueProp = ratio.GetType().GetProperty("value");
                        if (valueProp != null)
                        {
                            var v = valueProp.GetValue(ratio, null);
                            if (v is float f) return Mathf.RoundToInt(f);
                            if (v is double d) return Mathf.RoundToInt((float)d);
                        }
                        else
                        {
                            var numObj = ratio.GetType().GetProperty("numerator")?.GetValue(ratio, null);
                            var denObj = ratio.GetType().GetProperty("denominator")?.GetValue(ratio, null);
                            if (numObj != null && denObj != null)
                            {
                                float num = Convert.ToSingle(numObj);
                                float den = Mathf.Max(0.0001f, Convert.ToSingle(denObj));
                                return Mathf.RoundToInt(num / den);
                            }
                        }
                    }
                }

                // Fallback: try non-obsolete via reflection to avoid compiler warnings
                var rrProp = typeof(Resolution).GetProperty("refreshRate");
                if (rrProp != null)
                {
                    var v = rrProp.GetValue(res, null);
                    if (v is int i) return i;
                }
            }
            catch { }
            return 0;
        }

        private static bool SafeGetUseDefaultGraphicsAPIs(BuildTarget target)
        {
            try { return PlayerSettings.GetUseDefaultGraphicsAPIs(target); }
            catch { return true; }
        }

        private static string[] SafeGetGraphicsAPIs(BuildTarget target)
        {
            try
            {
                if (PlayerSettings.GetUseDefaultGraphicsAPIs(target))
                    return Array.Empty<string>();
                var apis = PlayerSettings.GetGraphicsAPIs(target);
                return apis?.Select(a => a.ToString()).ToArray() ?? Array.Empty<string>();
            }
            catch { return Array.Empty<string>(); }
        }
    }
}
#endif
