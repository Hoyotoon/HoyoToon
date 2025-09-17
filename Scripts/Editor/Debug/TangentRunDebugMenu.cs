#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;
using HoyoToon.Models;

namespace HoyoToon.Debugging
{
    internal static class TangentRunDebugMenu
    {
        [MenuItem("Assets/HoyoToon/Tangents/Apply Rules For Selection", false, 2195)]
        private static void ApplyRulesForSelection()
        {
            RunForSelection(forcedMode: null, title: "Apply Tangent Rules (Config)");
        }

        [MenuItem("Assets/HoyoToon/Tangents/Reset For Selection", false, 2196)]
        private static void ResetForSelection()
        {
            RunForSelection(TangentRulesApplier.TangentMode.None, "Reset Tangents (No Tangents)");
        }

        [MenuItem("Assets/HoyoToon/Tangents/Generate For Selection", false, 2197)]
        private static void GenerateForSelection()
        {
            RunForSelection(TangentRulesApplier.TangentMode.Generate, "Generate Tangents (ModifyMeshTangents)");
        }

        [MenuItem("Assets/HoyoToon/Tangents/From Vertex Color For Selection", false, 2198)]
        private static void VertexColorForSelection()
        {
            RunForSelection(TangentRulesApplier.TangentMode.FromVertexColor, "Vertex Color → Tangents (MoveColors)");
        }

        private static void RunForSelection(TangentRulesApplier.TangentMode? forcedMode, string title)
        {
            var selected = Selection.gameObjects != null && Selection.gameObjects.Length > 0
                ? Selection.gameObjects.Cast<UnityEngine.Object>().ToArray()
                : Selection.objects; // supports both Hierarchy and Project
            if (selected == null || selected.Length == 0)
            {
                HoyoToonDialogWindow.ShowInfo("HoyoToon Tangents", "Select one or more FBX assets or folders to process.");
                return;
            }

            // Compose a worklist that includes:
            // - Scene GameObjects directly selected (with renderers)
            // - FBX assets under selected Project items
            var work = new List<(string path, GameObject go, bool isScene)>();

            // Scene objects
            foreach (var o in selected)
            {
                if (o is GameObject g && (g.GetComponentInChildren<MeshFilter>(true) != null || g.GetComponentInChildren<SkinnedMeshRenderer>(true) != null))
                {
                    work.Add((path: AssetDatabase.GetAssetPath(g), go: g, isScene: string.IsNullOrEmpty(AssetDatabase.GetAssetPath(g))));
                }
            }

            // Project FBX assets
            var fbxPaths = EnumerateFbxAssetsUnder(selected);
            foreach (var p in fbxPaths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) work.Add((path: p, go: go, isScene: false));
            }

            // Deduplicate by (isScene ? go.GetInstanceID : path)
            var items = work
                .GroupBy(x => x.isScene ? $"scene:{x.go.GetInstanceID()}" : $"asset:{x.path}")
                .Select(g => g.First())
                .ToList();

            if (items.Count == 0)
            {
                HoyoToonDialogWindow.ShowInfo("HoyoToon Tangents", "No model GameObjects found in selection.");
                return;
            }

            int changed = 0;
            var sb = new StringBuilder();
            sb.AppendLine("# HoyoToon Tangents");
            sb.AppendLine();
            sb.AppendLine(forcedMode.HasValue ? $"Mode: {forcedMode.Value}" : "Mode: Config-Driven");
            sb.AppendLine();

            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    float p = (float)i / Math.Max(1, items.Count);
                    EditorUtility.DisplayProgressBar("HoyoToon Tangents", $"Processing: {Path.GetFileName(it.path)}", p);

                    bool ok;
                    if (forcedMode.HasValue)
                        ok = TangentRulesApplier.Apply(it.go, forcedMode.Value);
                    else
                        ok = TangentRulesApplier.TryApplyFromConfig(it.go);

                    if (ok)
                    {
                        changed++;
                        sb.AppendLine($"- Changed: `{it.path}`");
                    }
                    else
                    {
                        sb.AppendLine($"- Skipped/No Change: `{it.path}`");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            sb.AppendLine();
            sb.AppendLine($"Processed {items.Count} model(s). Changed: {changed}.");
            HoyoToonDialogWindow.ShowInfo("HoyoToon Tangents", sb.ToString());
        }

        private static IEnumerable<string> EnumerateFbxAssetsUnder(IEnumerable<UnityEngine.Object> selection)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in selection ?? Array.Empty<UnityEngine.Object>())
            {
                var p = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(p)) continue;

                if (AssetDatabase.IsValidFolder(p))
                {
                    var guids = AssetDatabase.FindAssets("t:Model", new[] { p });
                    foreach (var g in guids)
                    {
                        var ap = AssetDatabase.GUIDToAssetPath(g);
                        if (!string.IsNullOrEmpty(ap) && ap.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                            set.Add(ap);
                    }
                }
                else if (!string.IsNullOrEmpty(p) && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(p);
                }
            }
            return set;
        }
    }
}
#endif
