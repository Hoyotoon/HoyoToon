#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

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

            if (isPrefab)
            {
                var summary = $"\"{pendingAsset.name}\" prefab passed validation and can be added directly to the scene.";
                result = new ValidationResult(true, summary, MessageType.Info);
            }
            else
            {
                var summary = $"\"{pendingAsset.name}\" looks ready. You can run Auto Setup.";
                result = new ValidationResult(true, summary, MessageType.Info);
            }
            _lastPendingModelValidation = result;
            return true;
        }
    }
}
#endif
