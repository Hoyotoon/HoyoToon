#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor.Animations;

namespace HoyoToon
{
    public class HoyoToonMeshManager : AssetPostprocessor
    {
        private static Dictionary<string, Dictionary<string, (string guid, string meshName)>> originalMeshPaths = new Dictionary<string, Dictionary<string, (string guid, string meshName)>>();
        private static readonly string HoyoToonFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HoyoToon");
        private static readonly string OriginalMeshPathsFile = Path.Combine(HoyoToonFolder, "OriginalMeshPaths.json");
        private static bool isProcessingHumanoid = false;
        private static string processingPath = null;
        private static HoyoToonParseManager.BodyType storedBodyType = HoyoToonParseManager.BodyType.WuWa;
        
        /// <summary>
        /// Maps body types to their corresponding shader keys
        /// </summary>
        /// <param name="bodyType">The body type to map</param>
        /// <returns>The corresponding shader key</returns>
        private static string GetShaderKeyFromBodyType(HoyoToonParseManager.BodyType bodyType)
        {
            switch (bodyType)
            {
                // Honkai Star Rail variants
                case HoyoToonParseManager.BodyType.HSRMaid:
                case HoyoToonParseManager.BodyType.HSRKid:
                case HoyoToonParseManager.BodyType.HSRLad:
                case HoyoToonParseManager.BodyType.HSRMale:
                case HoyoToonParseManager.BodyType.HSRLady:
                case HoyoToonParseManager.BodyType.HSRGirl:
                case HoyoToonParseManager.BodyType.HSRBoy:
                case HoyoToonParseManager.BodyType.HSRMiss:
                    return "HSRShader";
                
                // Genshin Impact variants
                case HoyoToonParseManager.BodyType.GIBoy:
                case HoyoToonParseManager.BodyType.GIGirl:
                case HoyoToonParseManager.BodyType.GILady:
                case HoyoToonParseManager.BodyType.GIMale:
                case HoyoToonParseManager.BodyType.GILoli:
                    return "GIShader";
                
                // Honkai Impact variants
                case HoyoToonParseManager.BodyType.HI3P1:
                    return "HI3Shader";
                case HoyoToonParseManager.BodyType.HI3P2:
                    return "HI3P2Shader";
                
                // Wuthering Waves
                case HoyoToonParseManager.BodyType.WuWa:
                    return "WuWaShader";
                
                // Zenless Zone Zero
                case HoyoToonParseManager.BodyType.ZZZ:
                    return "ZZZShader";
                
                default:
                    return "WuWaShader"; // Default fallback
            }
        }
        
        /// <summary>
        /// Checks if a mesh should be skipped for the current body type
        /// </summary>
        /// <param name="meshName">Name of the mesh to check</param>
        /// <returns>True if the mesh should be skipped, false otherwise</returns>
        private static bool ShouldSkipMesh(string meshName)
        {
            string shaderKey = GetShaderKeyFromBodyType(HoyoToonParseManager.currentBodyType);
            return HoyoToonDataManager.Data.ShouldSkipMesh(shaderKey, meshName);
        }
        
        #region FBX Setup

                public static void SetFBXImportSettings(IEnumerable<string> paths)
        {
            // Load HoyoToon data and determine body type once at the beginning
            HoyoToonDataManager.GetHoyoToonData();
            HoyoToonParseManager.DetermineBodyType();
            storedBodyType = HoyoToonParseManager.currentBodyType;
            
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var p in paths)
                {
                    var fbx = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                    if (!fbx) continue;

                    ModelImporter importer = AssetImporter.GetAtPath(p) as ModelImporter;
                    if (!importer) continue;

                    // Set basic import settings
                    importer.globalScale = 1;
                    importer.isReadable = true;
                    importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
                    
                    // Configure humanoid
                    ConfigureHumanoidAvatar(importer);

                    // Set legacy compute normals
                    string pName = "legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes";
                    PropertyInfo prop = importer.GetType().GetProperty(pName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    prop.SetValue(importer, true);

                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureHumanoidAvatar(ModelImporter importer)
        {
            HoyoToonLogs.LogDebug($"Configuring avatar for: {importer.assetPath}");
            
            // Set up for humanoid conversion
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            
            // Mark for post-processing
            isProcessingHumanoid = true;
            processingPath = importer.assetPath;
            
            // Apply initial settings
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private void OnPreprocessModel()
        {
            // Only process if this is our target model
            if (assetPath != processingPath)
                return;

            ModelImporter modelImporter = assetImporter as ModelImporter;
            if (modelImporter == null)
                return;

            HoyoToonLogs.LogDebug($"Pre-processing model: {assetPath}");
        }

        private void OnPostprocessModel(GameObject root)
        {
            // Only process if this is our target model
            if (assetPath != processingPath || !isProcessingHumanoid)
                return;

            ModelImporter modelImporter = assetImporter as ModelImporter;
            if (modelImporter == null)
                return;

            HoyoToonLogs.LogDebug($"Post-processing model: {assetPath}");

            // Get and modify human description
            var description = modelImporter.humanDescription;
            var human = description.human.ToList();
            var skeleton = description.skeleton.ToList();
            bool modified = false;

            // Remove unwanted bones
            modified |= human.RemoveAll(bone => bone.humanName == "Jaw") > 0;

            // Always map eye bones
            modified |= MapEyeBones(human, root);

            // Fix leg bone rotations
            modified |= FixLegBoneRotations(human, skeleton);

            // Fix WuWa finger bones if needed
            if (storedBodyType == HoyoToonParseManager.BodyType.WuWa)
            {
                modified |= FixWuWaFingerBones(human, root);
            }

            if (modified)
            {
                HoyoToonLogs.LogDebug("Applying bone mapping and rotation modifications");
                description.human = human.ToArray();
                description.skeleton = skeleton.ToArray();
                modelImporter.humanDescription = description;
                
                // Apply changes
                EditorUtility.SetDirty(modelImporter);
                modelImporter.SaveAndReimport();
            }

            // Reset processing flags after we're done
            isProcessingHumanoid = false;
            processingPath = null;
            storedBodyType = HoyoToonParseManager.BodyType.WuWa;
        }

        private bool MapEyeBones(List<HumanBone> human, GameObject root)
        {
            bool modified = false;

            // Find the Left Eye and Right Eye transforms
            Transform leftEyeBone = FindRecursive(root.transform, "Left Eye");
            Transform rightEyeBone = FindRecursive(root.transform, "Right Eye");

            HoyoToonLogs.LogDebug($"Found Left Eye transform: {leftEyeBone != null}");
            HoyoToonLogs.LogDebug($"Found Right Eye transform: {rightEyeBone != null}");
            HoyoToonLogs.LogDebug($"Total human bones: {human.Count}");

            // Update existing eye bone mappings
            for (int i = 0; i < human.Count; i++)
            {
                var bone = human[i];
                
                if (bone.humanName == "LeftEye")
                {
                    HoyoToonLogs.LogDebug($"Found Left Eye slot with boneName: '{bone.boneName}' (empty: {string.IsNullOrEmpty(bone.boneName)})");
                    if (leftEyeBone != null)
                    {
                        bone.boneName = "Left Eye";
                        human[i] = bone;
                        HoyoToonLogs.LogDebug("Updated Left Eye slot to: Left Eye");
                        modified = true;
                    }
                }
                else if (bone.humanName == "RightEye")
                {
                    HoyoToonLogs.LogDebug($"Found Right Eye slot with boneName: '{bone.boneName}' (empty: {string.IsNullOrEmpty(bone.boneName)})");
                    if (rightEyeBone != null)
                    {
                        bone.boneName = "Right Eye";
                        human[i] = bone;
                        HoyoToonLogs.LogDebug("Updated Right Eye slot to: Right Eye");
                        modified = true;
                    }
                }
            }

            return modified;
        }



        private bool FixLegBoneRotations(List<HumanBone> human, List<SkeletonBone> skeleton)
        {
            bool modified = false;

            // Find the human bones for upper legs
            var leftUpperLeg = human.FirstOrDefault(h => h.humanName == "LeftUpperLeg");
            var rightUpperLeg = human.FirstOrDefault(h => h.humanName == "RightUpperLeg");

            // Fix left upper leg rotation
            if (!string.IsNullOrEmpty(leftUpperLeg.boneName))
            {
                for (int i = 0; i < skeleton.Count; i++)
                {
                    if (skeleton[i].name == leftUpperLeg.boneName)
                    {
                        var bone = skeleton[i];
                        bone.rotation = Quaternion.Euler(180f, 0f, 0f);
                        skeleton[i] = bone;
                        HoyoToonLogs.LogDebug($"Fixed rotation for Left Upper Leg: {leftUpperLeg.boneName}");
                        modified = true;
                        break;
                    }
                }
            }

            // Fix right upper leg rotation
            if (!string.IsNullOrEmpty(rightUpperLeg.boneName))
            {
                for (int i = 0; i < skeleton.Count; i++)
                {
                    if (skeleton[i].name == rightUpperLeg.boneName)
                    {
                        var bone = skeleton[i];
                        bone.rotation = Quaternion.Euler(180f, 0f, 0f);
                        skeleton[i] = bone;
                        HoyoToonLogs.LogDebug($"Fixed rotation for Right Upper Leg: {rightUpperLeg.boneName}");
                        modified = true;
                        break;
                    }
                }
            }

            return modified;
        }

        private bool FixWuWaFingerBones(List<HumanBone> human, GameObject root)
        {
            // Check if WuWa finger remapping is needed
            bool needsRemapping = human.Any(bone => 
                (bone.boneName == "Left Thumb" || bone.boneName == "Right Thumb") && 
                (bone.humanName.Contains("Thumb") || bone.humanName.Contains("Index") || 
                 bone.humanName.Contains("Middle") || bone.humanName.Contains("Ring") || 
                 bone.humanName.Contains("Little")));

            if (!needsRemapping)
                return false;

            HoyoToonLogs.LogDebug("Fixing WuWa finger bone mappings");

            // Remove all finger bones to start fresh
            human.RemoveAll(bone => 
                bone.humanName.Contains("Thumb") || 
                bone.humanName.Contains("Index") || 
                bone.humanName.Contains("Middle") || 
                bone.humanName.Contains("Ring") || 
                bone.humanName.Contains("Little"));

            // Define proper finger bone mapping
            var fingerMappings = new Dictionary<string, string[]>
            {
                { "Left Thumb Proximal", new[] { "Thumb1_L", "thumb1_l", "LeftThumb1" } },
                { "Left Thumb Intermediate", new[] { "Thumb2_L", "thumb2_l", "LeftThumb2" } },
                { "Left Thumb Distal", new[] { "Thumb3_L", "thumb3_l", "LeftThumb3" } },
                { "Left Index Proximal", new[] { "Index1_L", "index1_l", "LeftIndex1" } },
                { "Left Index Intermediate", new[] { "Index2_L", "index2_l", "LeftIndex2" } },
                { "Left Index Distal", new[] { "Index3_L", "index3_l", "LeftIndex3" } },
                { "Left Middle Proximal", new[] { "Middle1_L", "middle1_l", "LeftMiddle1" } },
                { "Left Middle Intermediate", new[] { "Middle2_L", "middle2_l", "LeftMiddle2" } },
                { "Left Middle Distal", new[] { "Middle3_L", "middle3_l", "LeftMiddle3" } },
                { "Left Ring Proximal", new[] { "Ring1_L", "ring1_l", "LeftRing1" } },
                { "Left Ring Intermediate", new[] { "Ring2_L", "ring2_l", "LeftRing2" } },
                { "Left Ring Distal", new[] { "Ring3_L", "ring3_l", "LeftRing3" } },
                { "Left Little Proximal", new[] { "Little1_L", "little1_l", "LeftLittle1", "Pinky1_L" } },
                { "Left Little Intermediate", new[] { "Little2_L", "little2_l", "LeftLittle2", "Pinky2_L" } },
                { "Left Little Distal", new[] { "Little3_L", "little3_l", "LeftLittle3", "Pinky3_L" } },
                { "Right Thumb Proximal", new[] { "Thumb1_R", "thumb1_r", "RightThumb1" } },
                { "Right Thumb Intermediate", new[] { "Thumb2_R", "thumb2_r", "RightThumb2" } },
                { "Right Thumb Distal", new[] { "Thumb3_R", "thumb3_r", "RightThumb3" } },
                { "Right Index Proximal", new[] { "Index1_R", "index1_r", "RightIndex1" } },
                { "Right Index Intermediate", new[] { "Index2_R", "index2_r", "RightIndex2" } },
                { "Right Index Distal", new[] { "Index3_R", "index3_r", "RightIndex3" } },
                { "Right Middle Proximal", new[] { "Middle1_R", "middle1_r", "RightMiddle1" } },
                { "Right Middle Intermediate", new[] { "Middle2_R", "middle2_r", "RightMiddle2" } },
                { "Right Middle Distal", new[] { "Middle3_R", "middle3_r", "RightMiddle3" } },
                { "Right Ring Proximal", new[] { "Ring1_R", "ring1_r", "RightRing1" } },
                { "Right Ring Intermediate", new[] { "Ring2_R", "ring2_r", "RightRing2" } },
                { "Right Ring Distal", new[] { "Ring3_R", "ring3_r", "RightRing3" } },
                { "Right Little Proximal", new[] { "Little1_R", "little1_r", "RightLittle1", "Pinky1_R" } },
                { "Right Little Intermediate", new[] { "Little2_R", "little2_r", "RightLittle2", "Pinky2_R" } },
                { "Right Little Distal", new[] { "Little3_R", "little3_r", "RightLittle3", "Pinky3_R" } }
            };

            int mappedCount = 0;
            foreach (var mapping in fingerMappings)
            {
                Transform bone = FindBoneByPatterns(root.transform, mapping.Value);
                if (bone != null)
                {
                    human.Add(new HumanBone
                    {
                        humanName = mapping.Key,
                        boneName = bone.name,
                        limit = new HumanLimit { useDefaultValues = true }
                    });
                    mappedCount++;
                }
            }

            HoyoToonLogs.LogDebug($"Mapped {mappedCount} WuWa finger bones");
            return mappedCount > 0;
        }

        private Transform FindBoneByPatterns(Transform root, string[] patterns)
        {
            foreach (string pattern in patterns)
            {
                Transform bone = FindRecursive(root, pattern);
                if (bone != null)
                    return bone;
            }
            return null;
        }

        private void OnPostprocessAvatar(GameObject root)
        {
            // Only process if this is our target model
            if (assetPath != processingPath || !isProcessingHumanoid)
                return;

            HoyoToonLogs.LogDebug($"Post-processing avatar for: {assetPath}");
            
            // Get the avatar
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .FirstOrDefault(x => x is Avatar) as Avatar;
                
            if (avatar != null)
            {
                HoyoToonLogs.LogDebug($"Avatar configuration - Name: {avatar.name}, Valid: {avatar.isValid}, Human: {avatar.isHuman}");
            }
        }

        #endregion

        #region Tangent Generation

        public static void GenTangents(GameObject selectedObject)
        {
            HoyoToonParseManager.DetermineBodyType();

            GameObject rootObject = GetRootParent(selectedObject);
            StoreOriginalMeshes(rootObject);

            bool processAllChildren = selectedObject == rootObject;

            ProcessMeshComponents<MeshFilter>(selectedObject, processAllChildren, (meshFilter) =>
            {
                if (meshFilter.sharedMesh != null)
                {
                    meshFilter.sharedMesh = ProcessAndSaveMesh(meshFilter.sharedMesh, meshFilter.name);
                }
            });

            ProcessMeshComponents<SkinnedMeshRenderer>(selectedObject, processAllChildren, (skinMeshRender) =>
            {
                if (skinMeshRender.sharedMesh != null)
                {
                    skinMeshRender.sharedMesh = ProcessAndSaveMesh(skinMeshRender.sharedMesh, skinMeshRender.name);
                }
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ProcessMeshComponents<T>(GameObject obj, bool processAllChildren, System.Action<T> processComponent) where T : Component
        {
            if (processAllChildren)
            {
                T[] components = obj.GetComponentsInChildren<T>();
                foreach (var component in components)
                {
                    processComponent(component);
                }
            }
            else
            {
                T component = obj.GetComponent<T>();
                if (component != null)
                {
                    processComponent(component);
                }
            }
        }

        private static Mesh ProcessAndSaveMesh(Mesh mesh, string componentName)
        {
            if (mesh == null) return null;

            Mesh newMesh;
            if (HoyoToonParseManager.currentBodyType == HoyoToonParseManager.BodyType.HI3P2)
            {
                newMesh = MoveColors(mesh);
            }
            else
            {
                if (ShouldSkipMesh(componentName))
                {
                    return mesh;
                }
                else
                {
                    newMesh = ModifyMeshTangents(mesh);
                }
            }
            newMesh.name = mesh.name;

            string path = AssetDatabase.GetAssetPath(mesh);
            string folderPath = Path.GetDirectoryName(path) + "/Meshes";
            if (!Directory.Exists(folderPath))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path), "Meshes");
            }
            path = folderPath + "/" + newMesh.name + ".asset";

            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(newMesh, path);
            return newMesh;
        }

        private static Mesh ModifyMeshTangents(Mesh mesh)
        {
            Mesh newMesh = UnityEngine.Object.Instantiate(mesh);

            var vertices = newMesh.vertices;
            var triangles = newMesh.triangles;
            var unmerged = new Vector3[newMesh.vertexCount];
            var merged = new Dictionary<Vector3, Vector3>();
            var tangents = new Vector4[newMesh.vertexCount];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var i0 = triangles[i + 0];
                var i1 = triangles[i + 1];
                var i2 = triangles[i + 2];

                var v0 = vertices[i0] * 100;
                var v1 = vertices[i1] * 100;
                var v2 = vertices[i2] * 100;

                var normal_ = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                unmerged[i0] += normal_ * Vector3.Angle(v1 - v0, v2 - v0);
                unmerged[i1] += normal_ * Vector3.Angle(v0 - v1, v2 - v1);
                unmerged[i2] += normal_ * Vector3.Angle(v0 - v2, v1 - v2);
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                if (!merged.ContainsKey(vertices[i]))
                {
                    merged[vertices[i]] = unmerged[i];
                }
                else
                {
                    merged[vertices[i]] += unmerged[i];
                }
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                var normal = merged[vertices[i]].normalized;
                tangents[i] = new Vector4(normal.x, normal.y, normal.z, 0);
            }

            newMesh.tangents = tangents;

            return newMesh;
        }

        private static Mesh MoveColors(Mesh mesh)
        {
            Mesh newMesh = UnityEngine.Object.Instantiate(mesh);

            var vertices = newMesh.vertices;
            var tangents = newMesh.tangents;
            var colors = newMesh.colors;

            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = Color.white;
                }
                newMesh.colors = colors;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                tangents[i].x = colors[i].r * 2 - 1;
                tangents[i].y = colors[i].g * 2 - 1;
                tangents[i].z = colors[i].b * 2 - 1;
            }
            newMesh.SetTangents(tangents);

            return newMesh;
        }

        private static void StoreOriginalMeshes(GameObject rootObject)
        {
            LoadOriginalMeshPaths(); // Load existing data

            string modelName = rootObject.name;
            if (!originalMeshPaths.ContainsKey(modelName))
            {
                originalMeshPaths[modelName] = new Dictionary<string, (string guid, string meshName)>();
            }

            MeshFilter[] meshFilters = rootObject.GetComponentsInChildren<MeshFilter>();
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null)
                {
                    StoreMeshGUID(modelName, meshFilter.sharedMesh);
                }
            }

            SkinnedMeshRenderer[] skinMeshRenderers = rootObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinMeshRenderer in skinMeshRenderers)
            {
                if (skinMeshRenderer.sharedMesh != null)
                {
                    StoreMeshGUID(modelName, skinMeshRenderer.sharedMesh);
                }
            }

            SaveOriginalMeshPaths(); // Save the updated data
        }

        private static void StoreMeshGUID(string modelName, Mesh mesh)
        {
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            originalMeshPaths[modelName][mesh.name] = (guid, mesh.name);
        }

        public static void ResetTangents(GameObject selectedObject)
        {
            HoyoToonParseManager.DetermineBodyType();

            GameObject rootObject = GetRootParent(selectedObject);
            LoadOriginalMeshPaths();

            string modelName = rootObject.name;
            if (!originalMeshPaths.ContainsKey(modelName))
            {
                HoyoToonLogs.ErrorDebug($"No stored mesh paths found for model: {modelName}");
                return;
            }

            bool processAllChildren = selectedObject == rootObject;

            ProcessMeshComponents<MeshFilter>(selectedObject, processAllChildren, (meshFilter) =>
            {
                if (meshFilter.sharedMesh != null)
                {
                    meshFilter.sharedMesh = RestoreOriginalMesh(modelName, meshFilter.sharedMesh.name);
                }
            });

            ProcessMeshComponents<SkinnedMeshRenderer>(selectedObject, processAllChildren, (skinMeshRender) =>
            {
                if (skinMeshRender.sharedMesh != null)
                {
                    skinMeshRender.sharedMesh = RestoreOriginalMesh(modelName, skinMeshRender.sharedMesh.name);
                }
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Mesh RestoreOriginalMesh(string modelName, string meshName)
        {
            if (originalMeshPaths[modelName].TryGetValue(meshName, out var meshInfo))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(meshInfo.guid);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (Object asset in assets)
                {
                    if (asset is Mesh originalMesh && originalMesh.name == meshInfo.meshName)
                    {
                        return originalMesh;
                    }
                }
            }

            HoyoToonLogs.ErrorDebug($"Original mesh not found for {meshName}. Unable to reset.");
            return null;
        }

        private static GameObject GetRootParent(GameObject obj)
        {
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
            }
            return obj;
        }

        private static void LoadOriginalMeshPaths()
        {
            if (File.Exists(OriginalMeshPathsFile))
            {
                string json = File.ReadAllText(OriginalMeshPathsFile);
                var deserializedDictionary = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string[]>>>(json);

                originalMeshPaths = deserializedDictionary.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToDictionary(
                        innerKvp => innerKvp.Key,
                        innerKvp => (innerKvp.Value[0], innerKvp.Value[1])
                    )
                );
            }
        }

        private static void SaveOriginalMeshPaths()
        {
            if (!Directory.Exists(HoyoToonFolder))
            {
                Directory.CreateDirectory(HoyoToonFolder);
            }

            var serializableDictionary = originalMeshPaths.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToDictionary(
                    innerKvp => innerKvp.Key,
                    innerKvp => new[] { innerKvp.Value.guid, innerKvp.Value.meshName }
                )
            );

            string json = JsonConvert.SerializeObject(serializableDictionary, Formatting.Indented);
            File.WriteAllText(OriginalMeshPathsFile, json);
        }

        #endregion

        #region Helper Methods

        private static Transform FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) 
                return parent;
            
            foreach (Transform child in parent)
            {
                Transform found = FindRecursive(child, name);
                if (found != null) 
                    return found;
            }
            
            return null;
        }

        #endregion
    }
}
#endif