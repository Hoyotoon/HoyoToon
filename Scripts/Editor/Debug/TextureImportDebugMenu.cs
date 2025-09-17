#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon.API;
using HoyoToon.Utilities;
using HoyoToon.Textures;

namespace HoyoToon.Debugging
{
	internal static class TextureImportDebugMenu
	{
		// Apply rules to all Texture2D assets inside the selected folder(s)
		[MenuItem("Assets/HoyoToon/Textures/Apply Import Rules In Selected Folder(s)", validate = true)]
		private static bool Validate_Menu_ApplyForFolders()
		{
			var guids = Selection.assetGUIDs ?? Array.Empty<string>();
			if (guids.Length == 0) return false;
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
					return true;
			}
			return false;
		}

		[MenuItem("Assets/HoyoToon/Textures/Apply Import Rules In Selected Folder(s)")]
		private static void Menu_ApplyForFolders()
		{
			var guids = Selection.assetGUIDs ?? Array.Empty<string>();
			var folderPaths = new List<string>();
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
					folderPaths.Add(path);
			}

			if (folderPaths.Count == 0)
			{
				HoyoToonDialogWindow.ShowInfo("HoyoToon Texture Rules",
					"No folder selected. Select one or more folders in the Project window and run again.");
				return;
			}

			// Find all Texture2D assets recursively under the selected folders
			var texturePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var folder in folderPaths)
			{
				string[] tGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
				foreach (var tg in tGuids)
				{
					var tPath = AssetDatabase.GUIDToAssetPath(tg);
					if (string.IsNullOrEmpty(tPath)) continue;
					if (AssetImporter.GetAtPath(tPath) is TextureImporter)
						texturePaths.Add(tPath);
				}
			}

			int total = texturePaths.Count;
			if (total == 0)
			{
				HoyoToonDialogWindow.ShowInfo("HoyoToon Texture Rules",
					"No Texture2D assets were found under the selected folder(s).");
				return;
			}

			var win = HoyoToonDialogWindow.ShowProgress("HoyoToon Texture Rules",
				$"Applying rules to {total} texture(s) found in selected folder(s)…");

			int changed = TextureImportRulesApplier.TryApplyForAssetsBatch(texturePaths);

			HoyoToonLogger.TextureInfo($"Texture rules: Processed {total} texture(s) from folders, applied to {changed}.");

			var listPreview = string.Join("\n", texturePaths.Take(20).Select(p => $"- {Path.GetFileName(p)}"));
			if (win != null)
			{
				win.SetMessage($"Applied rules. Processed {total}, applied to {changed}.\n\nShowing up to 20 items:\n{listPreview}");
				win.CompleteProgress("Done");
			}

			var report = TextureImportRulesApplier.GetLastBatchReport();
			if (win != null)
			{
				var message = $"# Texture Import Rules\n\n" +
							  $"- Processed: **{total}**\n" +
							  $"- Applied/Reimported: **{changed}**\n\n" +
							  "### Preview (first 20)\n" + listPreview +
							  (string.IsNullOrEmpty(report) ? "" : "\n\n### Details\n" + report);
				win.SetMessage(message);
			}
		}

		[MenuItem("Assets/HoyoToon/Textures/Apply Import Rules For Selection")]
		private static void Menu_ApplyForSelection()
		{
			var guids = Selection.assetGUIDs ?? Array.Empty<string>();
			var texturePaths = new List<string>();
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(path)) continue;
				if (AssetImporter.GetAtPath(path) is TextureImporter)
				{
					texturePaths.Add(path);
				}
			}

			int total = texturePaths.Count;
			if (total == 0)
			{
				HoyoToonDialogWindow.ShowInfo("HoyoToon Texture Rules",
					"No textures selected. Select one or more Texture2D assets and run again.");
				return;
			}

			var win = HoyoToonDialogWindow.ShowProgress("HoyoToon Texture Rules",
				$"Applying rules to {total} selected texture(s)…");

			int changed = TextureImportRulesApplier.TryApplyForAssetsBatch(texturePaths);

			HoyoToonLogger.TextureInfo($"Texture rules: Processed {total} texture(s), applied to {changed}.");

			var listPreview = string.Join("\n", texturePaths.Take(20).Select(p => $"- {Path.GetFileName(p)}"));
			if (win != null)
			{
				win.SetMessage($"Applied rules. Processed {total}, applied to {changed}.\n\nShowing up to 20 items:\n{listPreview}");
				win.CompleteProgress("Done");
			}

			var report = TextureImportRulesApplier.GetLastBatchReport();
			if (win != null)
			{
				var message = $"# Texture Import Rules\n\n" +
							  $"- Processed: **{total}**\n" +
							  $"- Applied/Reimported: **{changed}**\n\n" +
							  "### Preview (first 20)\n" + listPreview +
							  (string.IsNullOrEmpty(report) ? "" : "\n\n### Details\n" + report);
				win.SetMessage(message);
			}
		}
	}
}
#endif
