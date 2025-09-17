#if UNITY_EDITOR
#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Models
{
    /// <summary>
    /// Strongly-typed, modular FBX importer settings. All fields are optional; only non-null values are applied.
    /// Covers the major import tabs: Model, Rig, Animation, Materials.
    /// </summary>
    [Serializable]
    public class HoyoToonModelImportSettings
    {
        // MODEL TAB
        public float? globalScale;                        // ModelImporter.globalScale
        public bool? useFileScale;                        // ModelImporter.useFileScale
        public bool? importBlendShapes;                   // ModelImporter.importBlendShapes
        public bool? importVisibility;                    // ModelImporter.importVisibility
        public bool? importCameras;                       // ModelImporter.importCameras
        public bool? importLights;                        // ModelImporter.importLights
        public bool? isReadable;                          // ModelImporter.isReadable
        public bool? optimizeMeshPolygons;                // ModelImporter.optimizeMeshPolygons
        public bool? optimizeMeshVertices;                // ModelImporter.optimizeMeshVertices
        public ModelImporterNormals? normals;             // ModelImporterNormals
        public ModelImporterTangents? tangents;           // ModelImporterTangents

        // RIG TAB
        public ModelImporterAnimationType? animationType; // None / Legacy / Generic / Humanoid
        public ModelImporterAvatarSetup? avatarSetup;     // No Avatar / Create From This Model / Copy From Other
        public Avatar? sourceAvatar;                      // if avatarSetup == CopyFromOther
        public bool? bakeAxisConversion;                  // ModelImporter.bakeAxisConversion

        // ANIMATION TAB
        public bool? importAnimation;                     // ModelImporter.importAnimation
        public ModelImporterAnimationCompression? animationCompression; // Keyframe Reduction
        public bool? resampleCurves;                      // ModelImporter.resampleCurves

        // MATERIALS TAB
        public ModelImporterMaterialImportMode? materialImportMode; // None / Imported / Standard
        public ModelImporterMaterialSearch? materialSearch;         // Search options
        public ModelImporterMaterialName? materialName;             // Naming convention
        public ModelImporterMaterialLocation? materialLocation;     // Use Embedded / Use External

        // EXTRA: non-public importer flag accessed via reflection
        // legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes
        public bool? legacyBlendshapeNormals;
    }

    /// <summary>
    /// Lightweight snapshot of an FBX importer and a few scene stats for logging.
    /// </summary>
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
