using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace HoyoToon.EditorTools.ManagerScene
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class HoyoToonHairShadowMaskRenderer : MonoBehaviour
    {// The name of the single shader that handles both black (occlusion) and white (mask) output.
        private const string UnifiedMaskShaderName = "Hidden/HoyoToon/Utility/WhiteMask";

        private Shader unifiedMaskShader;
        private RenderTexture currentFrameMaskRT;

        // --- REUSED MATERIAL INSTANCES (Only for non-hair occluders) ---
        private Material blackMaskMaterialInstance;

        // Helper structure to track materials for cleanup
        private struct MaterialSwapData
        {
            // We only need to store the original shared materials to restore them later.
            public Material[] originalSharedMaterials;
            // NEW: If this was a hair renderer, this holds the single temporary material instance 
            // created for the mask (null for occluders). THIS MUST BE DESTROYED.
            public Material temporaryMaskMaterial;
        }

        // Stores original materials for ALL renderers that had their materials swapped.
        private Dictionary<Renderer, MaterialSwapData> materialSwapData = new Dictionary<Renderer, MaterialSwapData>();

        // CACHE: List of all active renderers.
        private List<Renderer> activeRenderersCache = new List<Renderer>();

        private Camera maskCamera;

        // Shader property IDs
        private static readonly int HairMaskRT_ID = Shader.PropertyToID("_HairMaskRT");
        private static readonly int MaskColorValue_ID = Shader.PropertyToID("_MaskColorValue");

        // --- PROPERTY IDs for HAIR DISSOLVE/ANIMATION (Used in property copying) ---
        // Textures
        private static readonly int DissolveMap_ID = Shader.PropertyToID("_DissolveMap");
        private static readonly int DissolveMask_ID = Shader.PropertyToID("_DissolveMask");

        // Floats/Vectors
        private static readonly int dissolvegroup_ID = Shader.PropertyToID("_dissolvegroup");
        private static readonly int DissoveON_ID = Shader.PropertyToID("_DissoveON");
        private static readonly int DissolveShadowOff_ID = Shader.PropertyToID("_DissolveShadowOff");
        private static readonly int DissolveRate_ID = Shader.PropertyToID("_DissolveRate");
        private static readonly int DissolveST_ID = Shader.PropertyToID("_DissolveST"); // float4
        private static readonly int DistortionST_ID = Shader.PropertyToID("_DistortionST"); // float4
        private static readonly int DissolveDistortionIntensity_ID = Shader.PropertyToID("_DissolveDistortionIntensity");
        private static readonly int DissolveOutlineSize1_ID = Shader.PropertyToID("_DissolveOutlineSize1");
        private static readonly int DissolveOutlineSize2_ID = Shader.PropertyToID("_DissolveOutlineSize2");
        private static readonly int DissolveOutlineOffset_ID = Shader.PropertyToID("_DissolveOutlineOffset");
        private static readonly int DissolveOutlineColor1_ID = Shader.PropertyToID("_DissolveOutlineColor1"); // float4
        private static readonly int DissolveOutlineColor2_ID = Shader.PropertyToID("_DissolveOutlineColor2"); // float4
        private static readonly int DissoveDirecMask_ID = Shader.PropertyToID("_DissoveDirecMask");
        private static readonly int DissolveMapAdd_ID = Shader.PropertyToID("_DissolveMapAdd");
        private static readonly int DissolveOutlineSmoothStep_ID = Shader.PropertyToID("_DissolveOutlineSmoothStep"); // float4
        private static readonly int DissolveUV_ID = Shader.PropertyToID("_DissolveUV");
        private static readonly int DissolveUVSpeed_ID = Shader.PropertyToID("_DissolveUVSpeed"); // float4
        private static readonly int DissolveComponent_ID = Shader.PropertyToID("_DissolveComponent"); // float4
        private static readonly int DissolvePosMaskPos_ID = Shader.PropertyToID("_DissolvePosMaskPos"); // float4
        private static readonly int DissolvePosMaskWorldON_ID = Shader.PropertyToID("_DissolvePosMaskWorldON");
        private static readonly int DissolvePosMaskRootOffset_ID = Shader.PropertyToID("_DissolvePosMaskRootOffset"); // float4
        private static readonly int DissolvePosMaskFilpOn_ID = Shader.PropertyToID("_DissolvePosMaskFilpOn");
        private static readonly int DissolvePosMaskOn_ID = Shader.PropertyToID("_DissolvePosMaskOn");
        private static readonly int DissolveMaskUVSet_ID = Shader.PropertyToID("_DissolveMaskUVSet");
        private static readonly int DissolveUseDirection_ID = Shader.PropertyToID("_DissolveUseDirection");
        private static readonly int DissolveCenter_ID = Shader.PropertyToID("_DissolveCenter"); // float4
        private static readonly int DissolveDiretcionXYZ_ID = Shader.PropertyToID("_DissolveDiretcionXYZ"); // float4
        private static readonly int DissolvePosMaskGlobalOn_ID = Shader.PropertyToID("_DissolvePosMaskGlobalOn");
        private static readonly int ES_EffCustomLightPosition_ID = Shader.PropertyToID("_ES_EffCustomLightPosition"); // float4

        // Dither/Visibility properties
        private static readonly int HideCharaParts_ID = Shader.PropertyToID("_HideCharaParts");
        private static readonly int ShowPartID_ID = Shader.PropertyToID("_ShowPartID"); // int
        private static readonly int UsingDitherAlpha_ID = Shader.PropertyToID("_UsingDitherAlpha");
        private static readonly int UsingDitherAlphaArt_ID = Shader.PropertyToID("_UsingDitherAlphaArt");
        private static readonly int DitherAlpha_ID = Shader.PropertyToID("_DitherAlpha");
        private static readonly int DITHER_FADE_IN_ID = Shader.PropertyToID("_DITHER_FADE_IN");

        // A temporary material array used for assigning
        private Material[] tempNewMaterials;

        // Flag to ensure all resources are initialized before rendering.
        private bool isInitialized = false;
        private double _lastHairWarningTime;
        private const double HairWarningCooldownSeconds = 5.0;
        private const string HairTagName = "Hair";
        private static bool s_tagChecked;
        private static bool s_hasHairTag;

        public static HoyoToonHairShadowMaskRenderer EnsureForManager(HoyoToonManager manager, Transform parent)
        {
            if (manager == null)
            {
                return null;
            }

            var existing = FindObjectsOfType<HoyoToonHairShadowMaskRenderer>(true)
                .FirstOrDefault(item => item != null && item.transform.IsChildOf(manager.transform));

            if (existing != null)
            {
                if (parent != null && existing.transform.parent != parent)
                {
                    existing.transform.SetParent(parent, false);
                }
                return existing;
            }

            var go = new GameObject("HoyoToon Hair Shadow Mask");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            else
            {
                go.transform.SetParent(manager.transform, false);
            }

            return go.AddComponent<HoyoToonHairShadowMaskRenderer>();
        }


        void Awake()
        {
            InitializeResources();
        }

        /// <summary>
        /// Copies a float property from the source material to the destination material if it exists.
        /// </summary>
        private void CopyFloatProperty(Material source, Material destination, int propertyID)
        {
            if (source == null || destination == null)
            {
                return;
            }

            if (source.HasProperty(propertyID))
            {
                destination.SetFloat(propertyID, source.GetFloat(propertyID));
            }
        }

        /// <summary>
        /// Copies a Vector4 property (including Colors) from the source material to the destination material if it exists.
        /// </summary>
        private void CopyVectorProperty(Material source, Material destination, int propertyID)
        {
            if (source == null || destination == null)
            {
                return;
            }

            if (source.HasProperty(propertyID))
            {
                destination.SetVector(propertyID, source.GetVector(propertyID));
            }
        }

        /// <summary>
        /// Ensures all persistent resources (shader, camera, mask materials) are set up.
        /// Called from Awake and LateUpdate (if initialization fails or resources are lost).
        /// </summary>
        private bool InitializeResources()
        {
            if (isInitialized) return true;

            // 1. Find the unified replacement shader by name
            unifiedMaskShader = Shader.Find(UnifiedMaskShaderName);
            if (unifiedMaskShader == null)
            {
                Debug.LogError($"Required unified mask shader '{UnifiedMaskShaderName}' could not be found. Please ensure the file 'UnifiedMaskShader.shader' is in your project.");
                enabled = false;
                return false;
            }

            // --- 2. Setup Temporary Camera (Persistent) ---
            if (maskCamera == null && enabled)
            {
                GameObject camGO = new GameObject("Mask_Render_Camera");
                // Important: HideAndDontSave prevents the camera object from being saved with the scene,
                // and makes it easy to spot/ignore in the Hierarchy.
                camGO.hideFlags = HideFlags.HideAndDontSave;
                maskCamera = camGO.AddComponent<Camera>();
                maskCamera.enabled = false; // Only render when explicitly called
            }

            // --- 3. Instantiate Shared Mask Material (Only Black Occluder) ---
            CleanupSharedMaskMaterials(destroyImmediately: true);

            // Only instantiate the shared black material instance.
            blackMaskMaterialInstance = new Material(unifiedMaskShader);
            blackMaskMaterialInstance.SetFloat(MaskColorValue_ID, 0.0f);

            isInitialized = true;
            return true;
        }

        void OnEnable()
        {
            // Re-initialize resources and cache on enabling (important for entering Play Mode)
            InitializeResources();
            RefreshRendererCache();

#if UNITY_EDITOR
        // Subscribe to Editor update loop when not playing to ensure continuous rendering
        if (!Application.isPlaying)
        {
            EditorApplication.update += EditorUpdate;
            // Subscribe to hierarchy changes to keep the renderer cache up-to-date
            EditorApplication.hierarchyChanged += RefreshRendererCache; 
        }
#endif
        }

        void OnDisable()
        {
            // Unsubscribe from Editor update loop and perform cleanup
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
        EditorApplication.hierarchyChanged -= RefreshRendererCache;
#endif

            RestoreOriginalMaterials();
            CleanupMaskCamera(destroyImmediately: true);
            CleanupSharedMaskMaterials(destroyImmediately: true);

            if (currentFrameMaskRT != null)
            {
                RenderTexture.ReleaseTemporary(currentFrameMaskRT);
                currentFrameMaskRT = null;
            }
            Shader.SetGlobalTexture(HairMaskRT_ID, null);
            isInitialized = false;
        }

#if UNITY_EDITOR
    /// <summary>
    /// Executes continuously in Edit Mode to force a LateUpdate cycle.
    /// </summary>
    void EditorUpdate()
    {
        // Only run the custom update logic when not in play mode
        if (!Application.isPlaying)
        {
            LateUpdate();
        }
    }
#endif

        /// <summary>
        /// Scans the entire scene for renderers and caches them. 
        /// This is called infrequently (on start/hierarchy change) to avoid lag.
        /// </summary>
        private void RefreshRendererCache()
        {
            activeRenderersCache.Clear();
            // NOTE: FindObjectsOfType is slow, but we only call it here (infrequently).
            Renderer[] allRenderers = FindObjectsOfType<Renderer>();

            foreach (Renderer renderer in allRenderers)
            {
                // Only cache renderers that are potentially relevant
                if (renderer.gameObject.hideFlags.HasFlag(HideFlags.HideAndDontSave))
                {
                    continue;
                }
                activeRenderersCache.Add(renderer);
            }
        }


        /// <summary>
        /// Finds the camera to mirror based on whether the application is in Play Mode (Main Camera) or Edit Mode (Scene View).
        /// </summary>
        private Camera GetActiveCamera()
        {
            if (Application.isPlaying)
            {
#if UNITY_EDITOR
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null && sceneView.camera != null && sceneView.hasFocus)
                {
                    return sceneView.camera;
                }
#endif
                // Play Mode: Use the camera tagged "MainCamera"
                return Camera.main;
            }
#if UNITY_EDITOR
        else
        {
            // Edit Mode: Use the camera from the last active Scene View.
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                return sceneView.camera;
            }
        }
#endif
            return null;
        }

        /// <summary>
        /// Executes every frame, swapping materials on all meshes for a single-pass render.
        /// </summary>
        void LateUpdate()
        {
            // Critical: Re-initialize if resources are somehow lost during scene transition
            if (!isInitialized && !InitializeResources())
            {
                Shader.SetGlobalTexture(HairMaskRT_ID, null);
                return;
            }

            Camera sourceCamera = GetActiveCamera();

            if (sourceCamera == null)
            {
                if (Application.isPlaying)
                {
                    // Most common failure point: Main camera is not tagged correctly.
                    Debug.LogWarning("Mask not updating in Play Mode: Camera.main not found. Please ensure your primary camera is tagged 'MainCamera'.");
                }
                Shader.SetGlobalTexture(HairMaskRT_ID, null);
                return;
            }

            // --- 1. Setup RenderTexture (Resolution Check) ---
            // Ensure the RT matches the current active camera's resolution
            bool needsNewRT = currentFrameMaskRT == null ||
                              currentFrameMaskRT.width != sourceCamera.pixelWidth ||
                              currentFrameMaskRT.height != sourceCamera.pixelHeight;

            if (needsNewRT)
            {
                if (currentFrameMaskRT != null) RenderTexture.ReleaseTemporary(currentFrameMaskRT);
                // ARGB32 for Red (Mask) and Green (Depth)
                currentFrameMaskRT = RenderTexture.GetTemporary(
                    sourceCamera.pixelWidth,
                    sourceCamera.pixelHeight,
                    16, // Depth bits
                    RenderTextureFormat.ARGB32
                );
                currentFrameMaskRT.name = "HairMaskRT_Temp";
            }


            // --- 2. Configure Temporary Camera ---
            maskCamera.CopyFrom(sourceCamera);
            maskCamera.targetTexture = currentFrameMaskRT;
            maskCamera.projectionMatrix = sourceCamera.projectionMatrix;

            // Clear Color to BLACK and clear Depth for a clean start
            maskCamera.clearFlags = CameraClearFlags.SolidColor;
            maskCamera.backgroundColor = Color.black;

            // Culling Mask is set to render EVERYTHING the main camera renders.
            maskCamera.cullingMask = sourceCamera.cullingMask;


            // --- 3. Single-Pass Setup: Swap Materials on ALL Renderers ---
            RestoreOriginalMaterials(); // Clean up previous frame's dictionary

            if (!HasAnyHairRenderers())
            {
                Shader.SetGlobalTexture(HairMaskRT_ID, null);
                return;
            }

            bool hairFound = false;

            // Optimized: Iterate over the pre-cached list instead of scanning the whole scene
            foreach (Renderer renderer in activeRenderersCache)
            {
                // Critical Check: If the renderer or its gameObject was destroyed mid-frame (common in Play Mode), skip it.
                if (renderer == null || renderer.gameObject == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                bool isHair = IsHairRenderer(renderer);
                if (isHair) hairFound = true;

                Material sharedMaskMat;
                Material temporaryMaskMaterial = null; // Used only for hair
                Material[] originalSharedMats = renderer.sharedMaterials;
                if (originalSharedMats == null || originalSharedMats.Length == 0)
                {
                    continue;
                }

                int materialCount = originalSharedMats.Length;


                if (isHair)
                {
                    // --- HAIR: CREATE UNIQUE TEMPORARY MATERIAL INSTANCE ---
                    temporaryMaskMaterial = new Material(unifiedMaskShader);
                    temporaryMaskMaterial.SetFloat(MaskColorValue_ID, 1.0f); // Set to white

                    // --- PROPERTY COPYING for DISSOLVE/ANIMATION ---
                    // We assume all hair submeshes use the same unique properties. 
                    // We copy properties from the FIRST original material (slot 0).
                    Material originalMat = originalSharedMats[0];

                    // --- TEXTURE COPYING ---
                    if (originalMat.HasProperty(DissolveMap_ID))
                    {
                        temporaryMaskMaterial.SetTexture(DissolveMap_ID, originalMat.GetTexture(DissolveMap_ID));
                    }
                    if (originalMat.HasProperty(DissolveMask_ID))
                    {
                        temporaryMaskMaterial.SetTexture(DissolveMask_ID, originalMat.GetTexture(DissolveMask_ID));
                    }

                    // --- FLOAT/INT/VECTOR/COLOR COPYING ---

                    // --- FLOAT/INT Properties ---
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, dissolvegroup_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissoveON_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveShadowOff_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveRate_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveDistortionIntensity_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveOutlineSize1_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveOutlineSize2_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveOutlineOffset_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissoveDirecMask_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveMapAdd_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveUV_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskWorldON_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskFilpOn_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskOn_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveMaskUVSet_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveUseDirection_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskGlobalOn_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, HideCharaParts_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, ShowPartID_ID); // Int
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, UsingDitherAlpha_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, UsingDitherAlphaArt_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DitherAlpha_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DITHER_FADE_IN_ID);

                    // --- VECTOR (float4) Properties ---
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveST_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DistortionST_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveOutlineColor1_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveOutlineColor2_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveOutlineSmoothStep_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveUVSpeed_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveComponent_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskPos_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskRootOffset_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveCenter_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveDiretcionXYZ_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, ES_EffCustomLightPosition_ID);

                    sharedMaskMat = temporaryMaskMaterial;

                }
                else
                {
                    // --- OCCLUDER: Use SHARED BLACK MATERIAL INSTANCE ---
                    // --- HAIR: CREATE UNIQUE TEMPORARY MATERIAL INSTANCE ---
                    temporaryMaskMaterial = new Material(unifiedMaskShader);
                    temporaryMaskMaterial.SetFloat(MaskColorValue_ID, 0.0f); // Set to white

                    // --- PROPERTY COPYING for DISSOLVE/ANIMATION ---
                    // We assume all hair submeshes use the same unique properties. 
                    // We copy properties from the FIRST original material (slot 0).
                    Material originalMat = originalSharedMats[0];

                    // // --- TEXTURE COPYING ---
                    // if (originalMat.HasProperty(DissolveMap_ID))
                    // {
                    //     temporaryMaskMaterial.SetTexture(DissolveMap_ID, originalMat.GetTexture(DissolveMap_ID));
                    // }
                    // if (originalMat.HasProperty(DissolveMask_ID))
                    // {
                    //     temporaryMaskMaterial.SetTexture(DissolveMask_ID, originalMat.GetTexture(DissolveMask_ID));
                    // }

                    // --- FLOAT/INT/VECTOR/COLOR COPYING ---

                    // --- FLOAT/INT Properties ---
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, dissolvegroup_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissoveON_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveShadowOff_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveRate_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveDistortionIntensity_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveOutlineSize1_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveOutlineSize2_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveOutlineOffset_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissoveDirecMask_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveMapAdd_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveUV_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskWorldON_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskFilpOn_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskOn_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveMaskUVSet_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolveUseDirection_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskGlobalOn_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, HideCharaParts_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, ShowPartID_ID); // Int
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, UsingDitherAlpha_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, UsingDitherAlphaArt_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DitherAlpha_ID);
                    CopyFloatProperty(originalMat, temporaryMaskMaterial, DITHER_FADE_IN_ID);

                    // --- VECTOR (float4) Properties ---
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveST_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DistortionST_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveOutlineColor1_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveOutlineColor2_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveOutlineSmoothStep_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveUVSpeed_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveComponent_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskPos_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolvePosMaskRootOffset_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveCenter_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, DissolveDiretcionXYZ_ID);
                    CopyVectorProperty(originalMat, temporaryMaskMaterial, ES_EffCustomLightPosition_ID);

                    sharedMaskMat = temporaryMaskMaterial;
                }


                // --- 3.2. Prepare Swap Data and Array ---
                MaterialSwapData data = new MaterialSwapData
                {
                    originalSharedMaterials = originalSharedMats,
                    temporaryMaskMaterial = temporaryMaskMaterial // Null for occluders, instance for hair
                };

                // Reuse tempNewMaterials array only if size matches.
                if (tempNewMaterials == null || tempNewMaterials.Length != materialCount)
                {
                    tempNewMaterials = new Material[materialCount];
                }

                // Assign the mask material instance to all slots for the renderer
                for (int i = 0; i < materialCount; i++)
                {
                    tempNewMaterials[i] = sharedMaskMat;
                }

                // --- 3.3. Perform Swap ---
                materialSwapData.Add(renderer, data);

                // Assign the new materials.
                renderer.materials = tempNewMaterials;
            }

            if (!hairFound)
            {
                Shader.SetGlobalTexture(HairMaskRT_ID, null);
                return;
            }


            // --- 4. Render All Objects in a Single Pass ---
            maskCamera.Render();

            // --- 5. Cleanup & Output ---
            RestoreOriginalMaterials();

            // Make the resulting texture globally available to other shaders for the rest of this frame
            Shader.SetGlobalTexture(HairMaskRT_ID, currentFrameMaskRT);
        }

        private void CleanupMaskCamera(bool destroyImmediately = false)
        {
            if (maskCamera != null)
            {
                if (destroyImmediately || Application.isEditor)
                {
                    DestroyImmediate(maskCamera.gameObject);
                }
                else
                {
                    Destroy(maskCamera.gameObject);
                }
                maskCamera = null;
            }
        }

        private void CleanupSharedMaskMaterials(bool destroyImmediately = false)
        {
            // Only need to cleanup the black shared material instance
            if (blackMaskMaterialInstance != null)
            {
                if (destroyImmediately || Application.isEditor) DestroyImmediate(blackMaskMaterialInstance);
                else Destroy(blackMaskMaterialInstance);
                blackMaskMaterialInstance = null;
            }
            // NOTE: Unique hair materials are destroyed in RestoreOriginalMaterials
        }

        private void RestoreOriginalMaterials()
        {
            // Clean up the temporary materials created in the previous frame
            foreach (var pair in materialSwapData)
            {
                Renderer renderer = pair.Key;
                MaterialSwapData data = pair.Value;

                // Critical check: The renderer might have been destroyed mid-frame, check for null
                if (renderer != null)
                {
                    // Restore the original materials using sharedMaterials (safe write)
                    renderer.sharedMaterials = data.originalSharedMaterials;
                }

                // CRITICAL: Destroy the temporary material instance created ONLY for hair
                if (data.temporaryMaskMaterial != null)
                {
                    if (Application.isEditor)
                    {
                        DestroyImmediate(data.temporaryMaskMaterial);
                    }
                    else
                    {
                        Destroy(data.temporaryMaskMaterial);
                    }
                }
            }
            materialSwapData.Clear();
        }

        private bool HasAnyHairRenderers()
        {
            foreach (var renderer in activeRenderersCache)
            {
                if (renderer == null || renderer.gameObject == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (IsHairRenderer(renderer))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsHairRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.gameObject == null)
            {
                return false;
            }

            if (!HasHairTag())
            {
                WarnMissingHairTag();
                return false;
            }

            try
            {
                return renderer.CompareTag(HairTagName);
            }
            catch (UnityException)
            {
                WarnMissingHairTag();
                return false;
            }
        }

        private bool HasHairTag()
        {
            if (s_tagChecked)
            {
                return s_hasHairTag;
            }

            s_tagChecked = true;

#if UNITY_EDITOR
            try
            {
                var tags = InternalEditorUtility.tags;
                s_hasHairTag = tags != null && tags.Contains(HairTagName);
            }
            catch
            {
                s_hasHairTag = false;
            }
#else
            s_hasHairTag = true;
#endif

            return s_hasHairTag;
        }

        private void WarnMissingHairTag()
            if (!HasHairTag())
#if UNITY_EDITOR
                TryCreateHairTag();
                if (!HasHairTag())
                {
                    WarnMissingHairTag();
                    return false;
                }
            }

            try
            {
                return renderer.CompareTag(HairTagName);
            }
            catch (UnityException)
            {
                TryCreateHairTag();
                if (!HasHairTag())
                {
                    WarnMissingHairTag();
                    return false;
                }
                return renderer.CompareTag(HairTagName);
            }
        }

        private void TryCreateHairTag()
        {
#if UNITY_EDITOR
            if (HasHairTag())
            {
                return;
            }

            try
            {
                var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var tagsProp = tagManager.FindProperty("tags");
                bool exists = false;
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    var tagProp = tagsProp.GetArrayElementAtIndex(i);
                    if (tagProp != null && tagProp.stringValue == HairTagName)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                    var newTagProp = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
                    newTagProp.stringValue = HairTagName;
                    tagManager.ApplyModifiedProperties();
                }

                s_tagChecked = false;
                _lastHairWarningTime = 0;
            }
            catch
            {
                s_tagChecked = false;
            }
#endif
        }
            Debug.LogWarning("[HoyoToon] Tag 'Hair' not found. Hair shadow mask will be disabled until the tag is created.");
        }
    }
}