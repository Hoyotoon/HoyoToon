#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using HoyoToon.Prerequisites;
using HoyoToon.Utilities;

namespace HoyoToon.Shaders
{
    [InitializeOnLoad]
    internal static class HoyoToonShaderPreprocessor
    {
        private const string PackageId = "com.meliverse.hoyotoon";
        private const string ShadersFolderName = "Shaders";
        private const string LastProcessedKey = "HoyoToon_ShaderPreprocessor_LastVRC";

        private static bool s_previousVrcState;
        private static bool s_initialized;

        static HoyoToonShaderPreprocessor()
        {
            s_previousVrcState = VRCSDKInstalledCheck.IsVRC;
            EditorApplication.update += CheckForToggle;
            EnsureInitialStateProcessed();
        }

        private static void CheckForToggle()
        {
            var current = VRCSDKInstalledCheck.IsVRC;
            if (current == s_previousVrcState)
                return;

            s_previousVrcState = current;
            ProcessShaders(current);
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
                HoyoToonLogger.ShaderWarning($"Shader folder not found: {fullPath}");
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
                HoyoToonLogger.ShaderError($"Shader preprocessing failed: {ex.Message}");
                return;
            }

            EditorPrefs.SetBool(LastProcessedKey, isVrc);

            if (processedCount > 0)
            {
                AssetDatabase.Refresh();
                HoyoToonLogger.ShaderInfo($"Processed {processedCount} shader file(s). VRChat mode: {(isVrc ? "ON" : "OFF")}");
            }
        }

        private static string GetShadersRootPath()
        {
            try
            {
                var packageInfo = PackageInfo.FindForAssembly(typeof(HoyoToonShaderPreprocessor).Assembly);
                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
                {
                    return Path.Combine(packageInfo.resolvedPath, ShadersFolderName);
                }

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.Combine(projectRoot, "Packages", PackageId, ShadersFolderName);
            }
            catch
            {
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
                HoyoToonLogger.ShaderWarning($"Failed to read shader file '{filePath}': {ex.Message}");
                return false;
            }

            string originalContent = content;

            if (isVrc)
            {
                content = Regex.Replace(content,
                    @"/\*#VRC\s*(.*?)\s*#VRC_END\*/",
                    "/*#VRC*/$1/*#VRC_END*/",
                    RegexOptions.Singleline);
            }
            else
            {
                content = Regex.Replace(content,
                    @"/\*#VRC\*/\s*(.*?)\s*/\*#VRC_END\*/",
                    "/*#VRC$1#VRC_END*/",
                    RegexOptions.Singleline);
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
                HoyoToonLogger.ShaderWarning($"Failed to write shader file '{filePath}': {ex.Message}");
                return false;
            }
        }
    }
}
#endif
