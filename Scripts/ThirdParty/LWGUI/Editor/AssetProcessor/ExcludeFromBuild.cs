// using System.Collections.Generic;
// using System.IO;
// using UnityEditor;
// using UnityEditor.Build;
// using UnityEditor.Build.Reporting;
// using UnityEngine;

namespace LWGUI
{
    /*
    /// <summary>
    /// Used to exclude textures referenced by ImageDrawer in Build
    /// </summary>
    public class ExcludeFromBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public static List<string> excludeAssetPaths = new List<string>();
        public static List<string> logs = new List<string>();

        public int callbackOrder => default;

        // You can find the build log at: %LOCALAPPDATA%\Unity\Editor\Editor.log
        private static void OutputLogs()
        {
            string str = string.Empty;
            foreach (var log in logs)
                str += "LWGUI: " + log + "\n";
            Debug.Log(str);
        }

        private static void GetExcludePaths()
        {
            logs.Add("Start searching for assets to exclude...");
            foreach (var shaderInfo in ShaderUtil.GetAllShaderInfo())
            {
                var shader = Shader.Find(shaderInfo.name);
                if (shader == null) continue;

                var mat = new Material(shader);
                var props = MaterialEditor.GetMaterialProperties(new UnityEngine.Object[] { mat });

                foreach (var prop in props)
                {
                    var drawer = ReflectionHelper.GetPropertyDrawer(shader, prop);
                    // The ImageDrawer references the texture via the Property default value, so it must be excluded when building
                    if (drawer != null && drawer is ImageDrawer && prop.textureValue != null)
                    {
                        var docImagePath = AssetDatabase.GetAssetPath(prop.textureValue);
                        logs.Add($"Find an editor-only image in Shader '{shader.name}': {docImagePath}");
                        if (!excludeAssetPaths.Contains(docImagePath))
                            excludeAssetPaths.Add(docImagePath);
                    }
                }
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            excludeAssetPaths.Clear();
            logs.Clear();

            GetExcludePaths();

            logs.Add("Start excluding assets...");
            foreach (var path in excludeAssetPaths)
            {
                string srcAssetPath = path,             dstAssetPath    = path + "~";
                string srcMetaPath  = path + ".meta",   dstMetaPath     = path + ".meta~";
                
                logs.Add($"Renaming '{srcAssetPath}' -> '{dstAssetPath}'");
                if (File.Exists(srcAssetPath) && !File.Exists(dstAssetPath))
                    File.Move(srcAssetPath, dstAssetPath);
                else
                    logs.Add("ERROR: The source path does not exist or the destination path already exists!!!");
                
                logs.Add($"Renaming '{srcMetaPath}' -> '{dstMetaPath}'");
                if (File.Exists(srcMetaPath) && !File.Exists(dstMetaPath))
                    File.Move(srcMetaPath, dstMetaPath);
                else
                    logs.Add("ERROR: The source path does not exist or the destination path already exists!!!");
            }

            // AssetDatabase.Refresh();
            OutputLogs();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            logs.Clear();

            logs.Add("Start restoring assets...");
            foreach (var path in excludeAssetPaths)
            {
                string srcAssetPath = path + "~",        dstAssetPath    = path;
                string srcMetaPath  = path + ".meta~",   dstMetaPath     = path + ".meta";
                
                logs.Add($"Renaming '{srcAssetPath}' -> '{dstAssetPath}'");
                if (File.Exists(srcAssetPath) && !File.Exists(dstAssetPath))
                    File.Move(srcAssetPath, dstAssetPath);
                else
                    logs.Add("ERROR: The source path does not exist or the destination path already exists!!!");
                
                logs.Add($"Renaming '{srcMetaPath}' -> '{dstMetaPath}'");
                if (File.Exists(srcMetaPath) && !File.Exists(dstMetaPath))
                    File.Move(srcMetaPath, dstMetaPath);
                else
                    logs.Add("ERROR: The source path does not exist or the destination path already exists!!!");
            }

            // AssetDatabase.Refresh();
            OutputLogs();
        }
    }
    */
}