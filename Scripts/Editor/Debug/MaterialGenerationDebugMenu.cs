#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.API;
using HoyoToon.Utilities;

namespace HoyoToon.Debugging
{
    public static class MaterialGenerationDebugMenu
    {
        [MenuItem("Assets/HoyoToon/Material/Generate Materials (Auto)")]
        public static void GenerateAutoFromSelection()
        {
            var selection = Selection.objects;
            HoyoToonLogger.MaterialInfo("MaterialGeneration: Starting auto generation from selection/context...");
            if (selection != null && selection.Length > 1)
            {
                MaterialGeneration.GenerateAuto((object)selection, pathOrJson: null, outputDir: null, materialName: null, showFailureWindow: true);
            }
            else
            {
                var sel = Selection.activeObject;
                MaterialGeneration.GenerateAuto((object)sel, pathOrJson: null, outputDir: null, materialName: null, showFailureWindow: true);
            }
            HoyoToonLogger.MaterialInfo("MaterialGeneration: Auto generation completed. See console for details or a dialog if failures occurred.");
        }

        [MenuItem("Assets/HoyoToon/Material/Clear Generated Materials")]
        public static void ClearGeneratedMaterialsFromSelection()
        {
            var selection = Selection.objects;
            int deleted = 0;
            if (selection != null && selection.Length > 1)
            {
                deleted = MaterialGeneration.ClearMaterialsFromContexts(selection);
            }
            else
            {
                var sel = Selection.activeObject;
                deleted = MaterialGeneration.ClearMaterialsFromContext(sel, null);
            }

            HoyoToonDialogWindow.ShowInfo("Clear Generated Materials", $"Deleted {deleted} material(s) from the selected context.");
        }
    }
}
#endif
