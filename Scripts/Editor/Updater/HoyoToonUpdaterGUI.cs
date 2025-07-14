#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;
using System;
using UnityEngine.Networking;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using System.Collections;

namespace HoyoToon
{
    public class HoyoToonUpdaterGUI : EditorWindow
    {
        private string localVersion;
        private string remoteVersion;
        private string downloadSize;
        private string bodyContent;
        private Vector2 scrollPosition;
        private Action OnUpdate;
        private Action onIgnoreUpdate;
        private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private HashSet<string> loadingTextures = new HashSet<string>();
        private static HoyoToonUpdaterGUI window;

        public static void ShowWindow(string localVersion, string remoteVersion, string downloadSize, string bodyContent, System.Action OnUpdate, System.Action onIgnoreUpdate)
        {
            HoyoToonUpdaterGUI window = GetWindow<HoyoToonUpdaterGUI>("HoyoToon Updater");
            window.localVersion = localVersion;
            window.remoteVersion = remoteVersion;
            window.downloadSize = downloadSize;
            window.bodyContent = bodyContent;
            window.OnUpdate = OnUpdate;
            window.onIgnoreUpdate = onIgnoreUpdate;
            window.Show();
        }

        private void OnGUI()
        {
            Rect bgRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(145.0f));
            bgRect.x = 0;
            bgRect.width = EditorGUIUtility.currentViewWidth;
            Rect logoRect = new Rect(bgRect.width / 2 - 375f, bgRect.height / 2 - 65f, 750f, 130f);

            string bgPathProperty = "UI/background";
            string logoPathProperty = "UI/updaterlogo";

            if (!string.IsNullOrEmpty(bgPathProperty))
            {
                Texture2D bg = Resources.Load<Texture2D>(bgPathProperty);

                if (bg != null)
                {
                    GUI.DrawTexture(bgRect, bg, ScaleMode.ScaleAndCrop);
                }
            }

            if (!string.IsNullOrEmpty(logoPathProperty))
            {
                Texture2D logo = Resources.Load<Texture2D>(logoPathProperty);

                if (logo != null)
                {
                    GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit);
                }
            }

            GUILayout.Label("Update Available", EditorStyles.boldLabel, GUILayout.ExpandWidth(true), GUILayout.Height(30));
            EditorGUILayout.Space();

            GUILayout.Label($"Current Version: {localVersion}");
            GUILayout.Label($"Latest Version: {remoteVersion}");
            double downloadSizeMB = double.Parse(downloadSize) / (1024 * 1024);
            GUILayout.Label($"Download Size: {downloadSizeMB.ToString("0.00")} MB");
            EditorGUILayout.Space();

            GUILayout.Label("Release Notes:");
            GUILayout.BeginVertical("box", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            RenderMarkdown(bodyContent);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            EditorGUILayout.Space();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Install Update"))
            {
                OnUpdate?.Invoke();
                Close();
            }
            if (GUILayout.Button("Ignore Update"))
            {
                onIgnoreUpdate?.Invoke();
                Close();
            }
            GUILayout.EndHorizontal();
        }

        private void RenderMarkdown(string markdown)
        {
            string richText = ConvertMarkdownToRichText(markdown);
            string[] lines = richText.Split(new[] { "\n\n" }, StringSplitOptions.None);

            Texture2D hsrLogo = Resources.Load<Texture2D>("UI/hsrlogo");
            Texture2D gilogo = Resources.Load<Texture2D>("UI/gilogo");
            Texture2D hi3p1Logo = Resources.Load<Texture2D>("UI/hi3p1logo");
            Texture2D HI3P2Logo = Resources.Load<Texture2D>("UI/hi3p2logo");
            Texture2D wuwalogo = Resources.Load<Texture2D>("UI/wuwalogo");
            Texture2D zzzlogo = Resources.Load<Texture2D>("UI/zzzlogo");
            Texture2D scriptslogo = Resources.Load<Texture2D>("UI/scriptslogo");
            Texture2D uilogo = Resources.Load<Texture2D>("UI/uilogo");
            Texture2D resourceslogo = Resources.Load<Texture2D>("UI/resourceslogo");
            Texture2D PostProcessinglogo = Resources.Load<Texture2D>("UI/postlogo");
            Texture2D editorScreenshot = Resources.Load<Texture2D>("UI/screenshotlogo");

            foreach (string line in lines)
            {
                if (line.StartsWith("<h1>"))
                {
                    if (line.Contains("Honkai Star Rail") && hsrLogo != null)
                    {
                        DrawHeaderImage(hsrLogo);
                    }
                    else if (line.Contains("Genshin Impact") && gilogo != null)
                    {
                        DrawHeaderImage(gilogo);
                    }
                    else if (line.Contains("Honkai Impact Part 1") && hi3p1Logo != null)
                    {
                        DrawHeaderImage(hi3p1Logo);
                    }
                    else if (line.Contains("Honkai Impact Part 2") && HI3P2Logo != null)
                    {
                        DrawHeaderImage(HI3P2Logo);
                    }
                    else if (line.Contains("Wuthering Waves") && wuwalogo != null)
                    {
                        DrawHeaderImage(wuwalogo);
                    }
                    else if (line.Contains("Zenless Zone Zero") && zzzlogo != null)
                    {
                        DrawHeaderImage(zzzlogo);
                    }
                    else if (line.Contains("Scripts") && scriptslogo != null)
                    {
                        DrawHeaderImage(scriptslogo);
                    }
                    else if (line.Contains("UI") && uilogo != null)
                    {
                        DrawHeaderImage(uilogo);
                    }
                    else if (line.Contains("Resources") && resourceslogo != null)
                    {
                        DrawHeaderImage(resourceslogo);
                    }
                    else if (line.Contains("Post Processing") && PostProcessinglogo != null)
                    {
                        DrawHeaderImage(PostProcessinglogo);
                    }
                    else if (line.Contains("Editor Screenshot") && editorScreenshot != null)
                    {
                        DrawHeaderImage(editorScreenshot);
                    }
                    else
                    {
                        GUIStyle h1Style = new GUIStyle(EditorStyles.boldLabel);
                        h1Style.fontSize = 24;
                        GUILayout.Label(line.Replace("<h1>", "").Replace("</h1>", ""), h1Style, GUILayout.Width(position.width));
                    }
                }
                else if (line.StartsWith("<h2>"))
                {
                    GUIStyle h2Style = new GUIStyle(EditorStyles.label);
                    h2Style.fontSize = 20;
                    GUILayout.Label(line.Replace("<h2>", "").Replace("</h2>", ""), h2Style, GUILayout.Width(position.width));
                }
                else if (line.StartsWith("<h3>"))
                {
                    GUIStyle h3Style = new GUIStyle(EditorStyles.miniLabel);
                    h3Style.fontSize = 16;
                    GUILayout.Label(line.Replace("<h3>", "").Replace("</h3>", ""), h3Style, GUILayout.Width(position.width));
                }
                else if (line.StartsWith("<li>"))
                {
                    GUIStyle listItemStyle = new GUIStyle(EditorStyles.label);
                    listItemStyle.richText = true;
                    GUILayout.Label("• " + line.Replace("<li>", "").Replace("</li>", ""), listItemStyle, GUILayout.Width(position.width));
                }
                else if (line.StartsWith("<b>"))
                {
                    GUIStyle boldStyle = new GUIStyle(EditorStyles.label);
                    boldStyle.fontStyle = FontStyle.Bold;
                    GUILayout.Label(line.Replace("<b>", "").Replace("</b>", ""), boldStyle, GUILayout.Width(position.width));
                }
                else if (line.StartsWith("<i>"))
                {
                    GUIStyle italicStyle = new GUIStyle(EditorStyles.label);
                    italicStyle.fontStyle = FontStyle.Italic;
                    GUILayout.Label(line.Replace("<i>", "").Replace("</i>", ""), italicStyle, GUILayout.Width(position.width));
                }
                else if (line.StartsWith("<a href="))
                {
                    string url = Regex.Match(line, @"<a href=""(.+?)"">").Groups[1].Value;
                    string linkText = Regex.Match(line, @">(.+?)</a>").Groups[1].Value;
                    if (GUILayout.Button(linkText, EditorStyles.linkLabel, GUILayout.Width(position.width)))
                    {
                        Application.OpenURL(url);
                    }
                }
                else if (line.StartsWith("<img src="))
                {
                    string url = Regex.Match(line, @"<img src=""(.+?)""").Groups[1].Value;
                    if (textureCache.ContainsKey(url))
                    {
                        Texture2D texture = textureCache[url];
                        float maxWidth = position.width * 0.75f;
                        float maxHeight = position.height * 0.75f;
                        float aspectRatio = (float)texture.height / texture.width;

                        float width = texture.width;
                        float height = texture.height;

                        if (width > maxWidth)
                        {
                            width = maxWidth;
                            height = maxWidth * aspectRatio;
                        }
                        if (height > maxHeight)
                        {
                            height = maxHeight;
                            width = maxHeight / aspectRatio;
                        }

                        GUILayout.Box(texture, GUILayout.Width(width), GUILayout.Height(height));
                    }
                    else
                    {
                        if (!loadingTextures.Contains(url))
                        {
                            loadingTextures.Add(url);
                            EditorCoroutineUtility.StartCoroutine(LoadTextureCoroutine(url), this);
                        }
                        GUILayout.Box("Loading...", GUILayout.Width(position.width * 0.5f), GUILayout.Height(50));
                    }
                }
                else
                {
                    GUILayout.Label(line, EditorStyles.wordWrappedLabel, GUILayout.Width(position.width));
                }
            }

            void DrawHeaderImage(Texture2D texture)
            {
                float fixedWidth = texture.width;
                float fixedHeight = texture.height;

                GUIStyle imageStyle = new GUIStyle();
                imageStyle.normal.background = null;

                GUILayout.BeginHorizontal();
                GUILayout.Space(-200);
                GUILayout.Label(texture, imageStyle, GUILayout.Width(fixedWidth), GUILayout.Height(fixedHeight));
                GUILayout.EndHorizontal();
            }
        }

        private string ConvertMarkdownToRichText(string markdown)
        {
            string richText = markdown;

            richText = Regex.Replace(richText, @"\*\*\*(.+?)\*\*\*", "<b><i>$1</i></b>");
            richText = Regex.Replace(richText, @"\*\*(.+?)\*\*", "<b>$1</b>");
            richText = Regex.Replace(richText, @"\*(.+?)\*", "<i>$1</i>");
            richText = Regex.Replace(richText, @"_(.+?)_", "<i>$1</i>");

            richText = Regex.Replace(richText, @"^# (.+)$", "<h1>$1</h1>", RegexOptions.Multiline);
            richText = Regex.Replace(richText, @"^## (.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
            richText = Regex.Replace(richText, @"^### (.+)$", "<h3>$1</h3>", RegexOptions.Multiline);

            richText = Regex.Replace(richText, @"^- (.+)$", "<li>$1</li>", RegexOptions.Multiline);

            richText = Regex.Replace(richText, @"!\[.*?\]\((.+?)\)", "<img src=\"$1\">");

            richText = richText.Replace("\n", "\n\n");

            return richText;
        }

        private IEnumerator LoadTextureCoroutine(string url)
        {
            int maxRetries = 3;
            int retries = 0;
            bool success = false;

            while (retries < maxRetries && !success)
            {
                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    www.timeout = 10;
                    yield return www.SendWebRequest();

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        HoyoToonLogs.ErrorDebug("Failed to load texture from URL: " + www.error);
                        retries++;
                        yield return new WaitForSeconds(1);
                    }
                    else
                    {
                        Texture2D texture = DownloadHandlerTexture.GetContent(www);
                        textureCache[url] = texture;
                        success = true;
                    }
                }
            }

            if (!success)
            {
                HoyoToonLogs.ErrorDebug("Failed to load texture after " + maxRetries + " attempts: " + url);
            }
        }
    }
}
#endif