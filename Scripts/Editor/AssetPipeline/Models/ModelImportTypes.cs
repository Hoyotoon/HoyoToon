#if UNITY_EDITOR
#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.AssetPipeline.Models
{
    [Serializable]
    public class HoyoToonModelImportSettings
    {
        // MODEL TAB
        public float? globalScale;
        public bool? useFileScale;
        public bool? importBlendShapes;
        public bool? importVisibility;
        public bool? importCameras;
        public bool? importLights;
        public bool? isReadable;
        public bool? optimizeMeshPolygons;
        public bool? optimizeMeshVertices;
        public ModelImporterNormals? normals;
        public ModelImporterTangents? tangents;

        // RIG TAB
        public ModelImporterAnimationType? animationType;
        public ModelImporterAvatarSetup? avatarSetup;
        public Avatar? sourceAvatar; // required when avatarSetup == CopyFromOther
        public bool? bakeAxisConversion;

        // ANIMATION TAB
        public bool? importAnimation;
        public ModelImporterAnimationCompression? animationCompression;
        public bool? resampleCurves;

        // MATERIALS TAB
        public ModelImporterMaterialImportMode? materialImportMode;
        public ModelImporterMaterialSearch? materialSearch;
        public ModelImporterMaterialName? materialName;
        public ModelImporterMaterialLocation? materialLocation;

        // Non-public importer flag accessed via reflection (legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes)
        public bool? legacyBlendshapeNormals;
    }

    public struct HoyoToonModelImportSnapshot
    {
        public string assetPath;
        public string assetName;
        public float globalScale;
        public bool importBlendShapes;
        public bool importAnimation;
        public ModelImporterAnimationType animationType;
        public ModelImporterMaterialImportMode materialImportMode;
        public ModelImporterMaterialLocation materialLocation;
        public int meshCount;
        public int skinnedMeshCount;
        public int boneCount;
    }
}
#endif
