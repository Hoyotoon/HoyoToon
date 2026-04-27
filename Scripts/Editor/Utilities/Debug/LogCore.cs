#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading;

namespace HoyoToon.Editor.Utilities.Debugging
{
    internal enum HoyoToonLogLevel
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Verbose = 3,
    }

    public enum HoyoToonLogCategory
    {
        General,
        Setup,
        Api,
        Detection,
        Prerequisites,
        Resources,
        Materials,
        Converter,
        HoyoToonFBX,
        Models,
        Textures,
    }

    internal static class LogCore
    {
        internal const string DebugEnabledEditorPrefsKey = "HoyoToon.Editor.DebugEnabled";
        internal const string LegacyDebugModeEditorPrefsKey = "HoyoToon.Editor.DebugMode";

        private const string HoyoToonColorHex = "#C000FF";
        private const string SetupColorHex = "#9AA8FF";
        private const string ApiColorHex = "#1EA7FF";
        private const string DetectionColorHex = "#FFB000";
        private const string PrerequisitesColorHex = "#00CFA0";
        private const string ResourcesColorHex = "#6D9EFF";
        private const string MaterialsColorHex = "#38D96B";
        private const string ConverterColorHex = "#00B86B";
        private const string HoyoToonFbxColorHex = "#00C7FF";
        private const string ModelsColorHex = "#2CC8B8";
        private const string TexturesColorHex = "#FF6B6B";
        private const string AutoColorHex = "#B7C4FF";
        private static readonly AsyncLocal<int> setupScopeDepth = new AsyncLocal<int>();

        internal static bool ShouldLog(bool enabled, HoyoToonLogLevel level)
        {
            return enabled || level == HoyoToonLogLevel.Warning || level == HoyoToonLogLevel.Error;
        }

        internal static IDisposable PushSetupScope()
        {
            setupScopeDepth.Value++;
            return new SetupScope();
        }

        internal static string FormatMessage(HoyoToonLogCategory category, string message, bool isBackgroundOperation)
        {
            var prefixBuilder = new StringBuilder();
            prefixBuilder.Append(FormatBracket("HoyoToon", HoyoToonColorHex));

            string categoryLabel = GetCategoryLabel(category);
            if (categoryLabel != null)
            {
                prefixBuilder.Append(FormatBracket(categoryLabel, GetCategoryColorHex(category)));
            }

            if (category != HoyoToonLogCategory.Setup && setupScopeDepth.Value > 0)
            {
                prefixBuilder.Append(FormatBracket(GetCategoryLabel(HoyoToonLogCategory.Setup), GetCategoryColorHex(HoyoToonLogCategory.Setup)));
            }

            if (isBackgroundOperation)
            {
                prefixBuilder.Append(FormatBracket("Auto", AutoColorHex));
            }

            string prefix = prefixBuilder.ToString();
            return string.IsNullOrWhiteSpace(message)
                ? prefix
                : $"{prefix} {message}";
        }

        private static string FormatBracket(string label, string colorHex)
        {
            return $"<color={colorHex}>[{label}]</color>";
        }

        private static string GetCategoryLabel(HoyoToonLogCategory category)
        {
            switch (category)
            {
                case HoyoToonLogCategory.Setup:
                    return "Setup";
                case HoyoToonLogCategory.Api:
                    return "API";
                case HoyoToonLogCategory.Detection:
                    return "Detection";
                case HoyoToonLogCategory.Prerequisites:
                    return "Prerequisites";
                case HoyoToonLogCategory.Resources:
                    return "Resources";
                case HoyoToonLogCategory.Materials:
                    return "Materials";
                case HoyoToonLogCategory.Converter:
                    return "Converter";
                case HoyoToonLogCategory.HoyoToonFBX:
                    return "HoyoToonFBX";
                case HoyoToonLogCategory.Models:
                    return "Models";
                case HoyoToonLogCategory.Textures:
                    return "Textures";
                default:
                    return null;
            }
        }

        private static string GetCategoryColorHex(HoyoToonLogCategory category)
        {
            switch (category)
            {
                case HoyoToonLogCategory.Setup:
                    return SetupColorHex;
                case HoyoToonLogCategory.Api:
                    return ApiColorHex;
                case HoyoToonLogCategory.Detection:
                    return DetectionColorHex;
                case HoyoToonLogCategory.Prerequisites:
                    return PrerequisitesColorHex;
                case HoyoToonLogCategory.Resources:
                    return ResourcesColorHex;
                case HoyoToonLogCategory.Materials:
                    return MaterialsColorHex;
                case HoyoToonLogCategory.Converter:
                    return ConverterColorHex;
                case HoyoToonLogCategory.HoyoToonFBX:
                    return HoyoToonFbxColorHex;
                case HoyoToonLogCategory.Models:
                    return ModelsColorHex;
                case HoyoToonLogCategory.Textures:
                    return TexturesColorHex;
                default:
                    return HoyoToonColorHex;
            }
        }

        private sealed class SetupScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                if (setupScopeDepth.Value > 0)
                {
                    setupScopeDepth.Value--;
                }
            }
        }
    }
}
#endif
