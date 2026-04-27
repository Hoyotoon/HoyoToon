using System;
using HoyoToon.Runtime.Scene.Placement;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HoyoToon.Editor.UI.Manager
{
    public sealed class BatchModelDetectionInfo
    {
        public GameObject ModelAsset { get; set; }
        public string AssetPath { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string GameKey { get; set; } = string.Empty;
        public string MatchedJsonAssetPath { get; set; } = string.Empty;
        public bool IsConverted { get; set; }
        public int CompatibleJsonCount { get; set; }
        public int CompatibleMaterialCount { get; set; }
        public int JsonCount { get; set; }
        public int MaterialCount { get; set; }
    }

    public sealed class ModuleContext
    {
        public EditorWindow Window { get; set; }
        public Object ActiveObject { get; set; }
        public Object[] SelectedObjects { get; set; } = Array.Empty<Object>();
        public GameObject SelectedGameObject { get; set; }
        public GameObject SelectedModelAsset { get; set; }
        public string SelectedModelAssetPath { get; set; } = string.Empty;
        public DefaultAsset SelectedModelFolder { get; set; }
        public string SelectedModelFolderPath { get; set; } = string.Empty;
        public BatchModelDetectionInfo[] BatchModelDetections { get; set; } = Array.Empty<BatchModelDetectionInfo>();
        public GameObject TargetRoot { get; set; }
        public Component TargetComponent { get; set; }
        public SerializedObject SerializedObject { get; set; }
        public string DetectedModelName { get; set; } = string.Empty;
        public string DetectedCharacterName { get; set; } = string.Empty;
        public string DetectedGameKey { get; set; } = string.Empty;
        public string MatchedJsonAssetPath { get; set; } = string.Empty;
        public int JsonCount { get; set; }
        public int MaterialCount { get; set; }
        public string ValidationMessage { get; set; } = string.Empty;
        public string DetectionSummaryMessage { get; set; } = string.Empty;
        public CharacterPlacementController PlacementController { get; set; }
        public GameObject PlacementActiveModel { get; set; }
        public string PlacementActiveCharacterName { get; set; } = string.Empty;
        public string PlacementActiveGameKey { get; set; } = string.Empty;
        public string[] PlacementManagedCharacterNames { get; set; } = Array.Empty<string>();
        public string[] PlacementTeamCharacterNames { get; set; } = Array.Empty<string>();

        public bool HasSelection => SelectedObjects != null && SelectedObjects.Length > 0;
        public bool HasBatchModels => BatchModelDetections != null && BatchModelDetections.Length > 0;
        public bool HasModelSelection => SelectedModelAsset != null || HasBatchModels;
        public bool HasValidTarget => SelectedModelAsset != null || HasBatchModels;
        public string TargetName => SelectedModelAsset != null ? SelectedModelAsset.name : string.Empty;
    }
}
