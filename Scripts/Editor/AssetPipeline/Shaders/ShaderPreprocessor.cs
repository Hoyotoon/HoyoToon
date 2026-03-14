#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Prerequisites;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.AssetPipeline.Shaders
{
    [InitializeOnLoad]
    internal static class ShaderPreprocessor
    {
        private const string PackageId = "com.hoyotoon.hoyotoon";
        private const string ShadersFolderName = "Shaders";
        private const string LastProcessedKey = PrefsKeys.ShaderPreprocessorLastVRC;
        private static readonly Regex s_vrcEnableRegex = new Regex(@"/\*#VRC\s*(.*?)\s*#VRC_END\*/", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex s_vrcDisableRegex = new Regex(@"/\*#VRC\*/\s*(.*?)\s*/\*#VRC_END\*/", RegexOptions.Singleline | RegexOptions.Compiled);

        private static bool s_previousVrcState;
        private static bool s_initialized;

        static ShaderPreprocessor()
        {
            s_previousVrcState = VRCSDKInstalledCheck.IsVRC;
            EnsureInitialStateProcessed();
        }

        private static void EnsureInitialStateProcessed()
        {
            if (s_initialized)
                return;
            s_initialized = true;

            bool hasKey = EditorPrefs.HasKey(LastProcessedKey);
            if (!hasKey)
            {
                ProcessShaders(s_previousVrcState);
                return;
            }

            bool lastProcessed = EditorPrefs.GetBool(LastProcessedKey, s_previousVrcState);
            if (lastProcessed != s_previousVrcState)
                ProcessShaders(s_previousVrcState);
        }

        private static void ProcessShaders(bool isVrc)
        {
            string fullPath = GetShadersRootPath();
            if (string.IsNullOrEmpty(fullPath) || !Directory.Exists(fullPath))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Shader, LogLevel.Warning, $"Shader folder not found: {fullPath}");
                return;
            }

            int processedCount = 0;

            try
            {
                foreach (string file in Directory.GetFiles(fullPath, "*.shader", SearchOption.AllDirectories))
                {
                    if (ProcessFile(file, isVrc))
                        processedCount++;
                }

                foreach (string file in Directory.GetFiles(fullPath, "*.hlsl", SearchOption.AllDirectories))
                {
                    if (ProcessFile(file, isVrc))
                        processedCount++;
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Shader, LogLevel.Error, $"Shader preprocessing failed: {ex.Message}");
                return;
            }

            EditorPrefs.SetBool(LastProcessedKey, isVrc);

            if (processedCount > 0)
            {
                AssetDatabase.Refresh();
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Shader, LogLevel.Info, $"Processed {processedCount} shader file(s). VRChat mode: {(isVrc ? "ON" : "OFF")}");
            }
        }

        private static string GetShadersRootPath()
        {
            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ShaderPreprocessor).Assembly);
                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
                {
                    return Path.Combine(packageInfo.resolvedPath, ShadersFolderName);
                }

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.Combine(projectRoot, "Packages", PackageId, ShadersFolderName);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("ShaderPreprocessor.GetShadersRootPath", $"Failed to resolve shader root path: {ex.Message}");
                return null;
            }
        }

        private static bool ProcessFile(string filePath, bool isVrc)
        {
            string content;
            try
            {
                content = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Shader, LogLevel.Warning, $"Failed to read shader file '{filePath}': {ex.Message}");
                return false;
            }

            string originalContent = content;

            if (isVrc)
            {
                content = s_vrcEnableRegex.Replace(content, "/*#VRC*/$1/*#VRC_END*/");
            }
            else
            {
                content = s_vrcDisableRegex.Replace(content, "/*#VRC$1#VRC_END*/");
            }

            if (content == originalContent)
                return false;

            try
            {
                File.WriteAllText(filePath, content);
                return true;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Shader, LogLevel.Warning, $"Failed to write shader file '{filePath}': {ex.Message}");
                return false;
            }
        }
    }
}
#endif
