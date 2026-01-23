#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Prerequisites;

namespace HoyoToon.EditorTools.ManagerUI
{
    internal static class HoyoToonManagerValidationUtility
    {
        internal readonly struct ValidationResult
        {
            public readonly bool IsReady;
            public readonly string Summary;
            public readonly MessageType MessageType;

            public ValidationResult(bool isReady, string summary, MessageType messageType)
            {
                IsReady = isReady;
                Summary = summary ?? string.Empty;
                MessageType = messageType;
            }
        }

        private const string SelectPrompt = "Select an FBX or prefab asset to begin.";
        private const string ConvertPrompt = "Convert this model with Hoyo2VRC before continuing when targeting VRChat.";
        private const string OptionalConvertPrompt = "Hoyo2VRC conversion is optional because the VRChat SDK is not detected.";

        private static ValidationResult _lastPendingModelValidation = new ValidationResult(false, SelectPrompt, MessageType.Info);

        public static ValidationResult GetPendingModelValidation()
        {
            return _lastPendingModelValidation;
        }

        public static bool CanProcessModel(HoyoToonManager manager, GameObject pendingAsset, out ValidationResult result)
        {
            _ = manager;
            if (pendingAsset == null)
            {
                result = new ValidationResult(false, SelectPrompt, MessageType.Info);
                _lastPendingModelValidation = result;
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(pendingAsset);
            var extension = string.IsNullOrEmpty(assetPath) ? string.Empty : Path.GetExtension(assetPath);

            bool isPrefab = extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase);
            bool isFbx = extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase);

            if (!isPrefab && !isFbx)
            {
                var summary = $"\"{pendingAsset.name}\" is not a supported asset type. Please select an FBX or prefab.";
                result = new ValidationResult(false, summary, MessageType.Error);
                _lastPendingModelValidation = result;
                return false;
            }

            bool requiresVrcConversion = VRCSDKInstalledCheck.IsVRC;
            if (requiresVrcConversion && !IsHoyo2VrcConverted(pendingAsset))
            {
                string summary = isPrefab
                    ? $"\"{pendingAsset.name}\" is a prefab, but it wasn't created from a validated Hoyo2VRC-converted FBX. Please regenerate the prefab after running the converter."
                    : $"\"{pendingAsset.name}\" {ConvertPrompt}";
                result = new ValidationResult(false, summary, MessageType.Error);
                _lastPendingModelValidation = result;
                return false;
            }

            if (isPrefab)
            {
                var summary = requiresVrcConversion
                    ? $"\"{pendingAsset.name}\" prefab passed validation and can be added directly to the scene."
                    : $"\"{pendingAsset.name}\" prefab is ready. {OptionalConvertPrompt}";
                result = new ValidationResult(true, summary, MessageType.Info);
            }
            else
            {
                var summary = requiresVrcConversion
                    ? $"\"{pendingAsset.name}\" looks ready. You can run Auto Setup."
                    : $"\"{pendingAsset.name}\" looks ready. {OptionalConvertPrompt}";
                result = new ValidationResult(true, summary, MessageType.Info);
            }
            _lastPendingModelValidation = result;
            return true;
        }

        private static bool IsHoyo2VrcConverted(GameObject model)
        {
            var assetPath = AssetDatabase.GetAssetPath(model);
            if (!string.IsNullOrEmpty(assetPath) && HoyoToonSetupModelsHelper.IsConverted(assetPath))
            {
                return true;
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                var dependencies = AssetDatabase.GetDependencies(assetPath, true);
                foreach (var dependency in dependencies)
                {
                    if (!dependency.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (HoyoToonSetupModelsHelper.IsConverted(dependency))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
#endif
