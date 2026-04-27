#if UNITY_EDITOR
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoyoToon.Editor.Prerequisites.RenderPipeline
{
    internal static class RenderPipelineSwitcher
    {
        private const string SelectionEditorPrefsKey = "HoyoToon.Prerequisites.RenderPipeline.Selection";
        private const string RequiredAssetRelativePath = "/Scripts/Runtime/Rendering/HoyoToon.asset";

        public static string RequiredAssetPath => HoyoToonApi.PackageRootAssetPath + RequiredAssetRelativePath;

        public static RenderPipelineSelection GetSelection()
        {
            int value = EditorPrefs.GetInt(PrefsKey(SelectionEditorPrefsKey), (int)RenderPipelineSelection.Unspecified);
            return value switch
            {
                (int)RenderPipelineSelection.Universal => RenderPipelineSelection.Universal,
                (int)RenderPipelineSelection.BuiltIn => RenderPipelineSelection.BuiltIn,
                _ => RenderPipelineSelection.Unspecified,
            };
        }

        public static void SetSelection(RenderPipelineSelection selection)
        {
            EditorPrefs.SetInt(PrefsKey(SelectionEditorPrefsKey), (int)selection);
        }

        public static bool ApplySelection(RenderPipelineSelection selection, out string message)
        {
            switch (selection)
            {
                case RenderPipelineSelection.Universal:
                    if (!TryLoadRequiredPipelineAsset(out RenderPipelineAsset asset))
                    {
                        message = $"The packaged HoyoToon URP asset could not be loaded from '{RequiredAssetPath}'.";
                        return false;
                    }

                    ApplyPipelineAsset(asset);
                    SetSelection(RenderPipelineSelection.Universal);
                    message = $"Assigned the packaged HoyoToon URP asset from '{RequiredAssetPath}'.";
                    return true;

                case RenderPipelineSelection.BuiltIn:
                    ApplyPipelineAsset(null);
                    SetSelection(RenderPipelineSelection.BuiltIn);
                    message = "Cleared the project render pipeline assets and restored the Built-in Render Pipeline.";
                    return true;

                default:
                    message = "No render pipeline change was applied.";
                    return false;
            }
        }

        public static bool IsRequiredAsset(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && string.Equals(assetPath, RequiredAssetPath, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryLoadRequiredPipelineAsset(out RenderPipelineAsset asset)
        {
            asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(RequiredAssetPath);
            return asset != null;
        }

        private static void ApplyPipelineAsset(RenderPipelineAsset asset)
        {
            GraphicsSettings.defaultRenderPipeline = asset;

            int currentQualityLevel = QualitySettings.GetQualityLevel();
            int qualityLevelCount = QualitySettings.names.Length;
            for (int index = 0; index < qualityLevelCount; index++)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = asset;
            }

            if (qualityLevelCount > 0 && currentQualityLevel >= 0 && currentQualityLevel < qualityLevelCount)
            {
                QualitySettings.SetQualityLevel(currentQualityLevel, false);
            }

            EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
            AssetDatabase.SaveAssets();
        }

        private static string PrefsKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
        }
    }
}
#endif
