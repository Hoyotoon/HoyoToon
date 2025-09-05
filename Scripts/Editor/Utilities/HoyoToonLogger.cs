#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    // Thin wrapper that exposes only category/uniquely named logs for modularity.
    public static class HoyoToonLogger
    {
        // Keep API minimal and descriptive. These methods call into LogCore.
        public static void Info(string message) => HoyoToonLogCore.Log(message);
        public static void Warning(string message) => HoyoToonLogCore.Warn(message);
        public static void Error(string message) => HoyoToonLogCore.Error(message);

        public static void Info(string message, UnityEngine.Object context) => HoyoToonLogCore.Log(message, context);
        public static void Warning(string message, UnityEngine.Object context) => HoyoToonLogCore.Warn(message, context);
        public static void Error(string message, UnityEngine.Object context) => HoyoToonLogCore.Error(message, context);

        // Example of uniquely named logs for common categories used by HoyoToon.
        public static void ShaderInfo(string message) => HoyoToonLogCore.LogCategory("Shader", message);
        public static void ShaderWarning(string message) => HoyoToonLogCore.WarnCategory("Shader", message);
        public static void ShaderError(string message) => HoyoToonLogCore.ErrorCategory("Shader", message);

        public static void UIInfo(string message) => HoyoToonLogCore.LogCategory("UI", message);
        public static void UIWarning(string message) => HoyoToonLogCore.WarnCategory("UI", message);
        public static void UIError(string message) => HoyoToonLogCore.ErrorCategory("UI", message);

        public static void ModelInfo(string message) => HoyoToonLogCore.LogCategory("Model", message);
        public static void ModelWarning(string message) => HoyoToonLogCore.WarnCategory("Model", message);
        public static void ModelError(string message) => HoyoToonLogCore.ErrorCategory("Model", message);

        public static void APIInfo(string message) => HoyoToonLogCore.LogCategory("API", message);
        public static void APIWarning(string message) => HoyoToonLogCore.WarnCategory("API", message);
        public static void APIError(string message) => HoyoToonLogCore.ErrorCategory("API", message);

        public static void TextureInfo(string message) => HoyoToonLogCore.LogCategory("Texture", message);
        public static void TextureWarning(string message) => HoyoToonLogCore.WarnCategory("Texture", message);
        public static void TextureError(string message) => HoyoToonLogCore.ErrorCategory("Texture", message);

        public static void MaterialInfo(string message) => HoyoToonLogCore.LogCategory("Material", message);
        public static void MaterialWarning(string message) => HoyoToonLogCore.WarnCategory("Material", message);
        public static void MaterialError(string message) => HoyoToonLogCore.ErrorCategory("Material", message);

        public static void ManagerInfo(string message) => HoyoToonLogCore.LogCategory("Manager", message);
        public static void ManagerWarning(string message) => HoyoToonLogCore.WarnCategory("Manager", message);
        public static void ManagerError(string message) => HoyoToonLogCore.ErrorCategory("Manager", message);

        public static void ResourcesInfo(string message) => HoyoToonLogCore.LogCategory("Resources", message);
        public static void ResourcesWarning(string message) => HoyoToonLogCore.WarnCategory("Resources", message);
        public static void ResourcesError(string message) => HoyoToonLogCore.ErrorCategory("Resources", message);

        // Unconditional pass-through if needed
        public static void Always(string message, LogType type = LogType.Log) => HoyoToonLogCore.LogAlways(message, type);
        public static void Always(string category, string message, LogType type) => HoyoToonLogCore.LogAlwaysCategory(category, message, type);

        // Event exposure passthrough for consumers preferring HoyoToonLogger surface
        public static event Action<string, LogType> OnLog
        {
            add { HoyoToonLogCore.OnLog += value; }
            remove { HoyoToonLogCore.OnLog -= value; }
        }
    }
}
#endif
