#if UNITY_EDITOR
using HoyoToon.Editor.Utilities.Editor;
using HoyoToon.Editor.Utilities.Renders;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Renders
{
    internal sealed class RenderCaptureViewModel
    {
        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;
        private const int DefaultScale = 1;
        private const int MinScale = 1;
        private const int MaxScale = 4;
        private const int DefaultTurnaroundGap = 24;
        private const float DefaultTurnaroundPaddingMultiplier = 1.15f;

        internal Camera CaptureCamera;
        internal GameObject ModelOverride;
        internal Texture2D WatermarkTexture;
        internal Texture2D TurnaroundBackgroundTexture;
        internal string SavePath = "HoyoToon Captures";
        internal string LastCapturePath = string.Empty;
        internal int Width = DefaultWidth;
        internal int Height = DefaultHeight;
        internal int Scale = DefaultScale;
        internal int TurnaroundGap = DefaultTurnaroundGap;
        internal float TurnaroundPaddingMultiplier = DefaultTurnaroundPaddingMultiplier;
        internal bool TransparentBackground = true;
        internal bool OpenAfterCapture;
        internal bool EnableWatermark = true;
        internal bool SyncWithSceneView;
        internal bool TurnaroundEnabled;

        internal int FinalWidth => Mathf.Max(1, Width * Mathf.Clamp(Scale, MinScale, MaxScale));
        internal int FinalHeight => Mathf.Max(1, Height * Mathf.Clamp(Scale, MinScale, MaxScale));

        internal void Load(string keyPrefix)
        {
            string prefix = NormalizeKeyPrefix(keyPrefix);
            Width = EditorPrefs.GetInt(PrefsKey(prefix, "Width"), Width);
            Height = EditorPrefs.GetInt(PrefsKey(prefix, "Height"), Height);
            Scale = Mathf.Clamp(EditorPrefs.GetInt(PrefsKey(prefix, "Scale"), Scale), MinScale, MaxScale);
            SavePath = EditorPrefs.GetString(PrefsKey(prefix, "SavePath"), SavePath);
            TransparentBackground = EditorPrefs.GetBool(PrefsKey(prefix, "Transparent"), TransparentBackground);
            OpenAfterCapture = EditorPrefs.GetBool(PrefsKey(prefix, "OpenAfter"), OpenAfterCapture);
            EnableWatermark = EditorPrefs.GetBool(PrefsKey(prefix, "Watermark"), EnableWatermark);
            TurnaroundEnabled = EditorPrefs.GetBool(PrefsKey(prefix, "Turnaround"), TurnaroundEnabled);
            TurnaroundGap = EditorPrefs.GetInt(PrefsKey(prefix, "TurnaroundGap"), TurnaroundGap);
            TurnaroundPaddingMultiplier = EditorPrefs.GetFloat(PrefsKey(prefix, "TurnaroundPadding"), TurnaroundPaddingMultiplier);
            CaptureCamera = ResolveObject<Camera>(EditorPrefs.GetString(PrefsKey(prefix, "Camera"), string.Empty));
            ModelOverride = ResolveObject<GameObject>(EditorPrefs.GetString(PrefsKey(prefix, "Model"), string.Empty));
            WatermarkTexture = ResolveObject<Texture2D>(EditorPrefs.GetString(PrefsKey(prefix, "WatermarkTexture"), string.Empty));
            TurnaroundBackgroundTexture = ResolveObject<Texture2D>(EditorPrefs.GetString(PrefsKey(prefix, "TurnaroundBackground"), string.Empty));
        }

        internal void Save(string keyPrefix)
        {
            string prefix = NormalizeKeyPrefix(keyPrefix);
            EditorPrefs.SetInt(PrefsKey(prefix, "Width"), Width);
            EditorPrefs.SetInt(PrefsKey(prefix, "Height"), Height);
            EditorPrefs.SetInt(PrefsKey(prefix, "Scale"), Scale);
            EditorPrefs.SetString(PrefsKey(prefix, "SavePath"), SavePath ?? string.Empty);
            EditorPrefs.SetBool(PrefsKey(prefix, "Transparent"), TransparentBackground);
            EditorPrefs.SetBool(PrefsKey(prefix, "OpenAfter"), OpenAfterCapture);
            EditorPrefs.SetBool(PrefsKey(prefix, "Watermark"), EnableWatermark);
            EditorPrefs.SetBool(PrefsKey(prefix, "Turnaround"), TurnaroundEnabled);
            EditorPrefs.SetInt(PrefsKey(prefix, "TurnaroundGap"), TurnaroundGap);
            EditorPrefs.SetFloat(PrefsKey(prefix, "TurnaroundPadding"), TurnaroundPaddingMultiplier);
            EditorPrefs.SetString(PrefsKey(prefix, "Camera"), SerializeObject(CaptureCamera));
            EditorPrefs.SetString(PrefsKey(prefix, "Model"), SerializeObject(ModelOverride));
            EditorPrefs.SetString(PrefsKey(prefix, "WatermarkTexture"), SerializeObject(WatermarkTexture));
            EditorPrefs.SetString(PrefsKey(prefix, "TurnaroundBackground"), SerializeObject(TurnaroundBackgroundTexture));
        }

        internal bool TrySyncCaptureCameraWithSceneView()
        {
            if (CaptureCamera == null || SceneView.lastActiveSceneView == null || SceneView.lastActiveSceneView.camera == null)
                return false;

            Camera sceneViewCamera = SceneView.lastActiveSceneView.camera;
            TurnaroundCaptureUtility.ApplyCameraState(
                CaptureCamera,
                TurnaroundCaptureUtility.CaptureState(sceneViewCamera));
            return true;
        }

        private static string NormalizeKeyPrefix(string keyPrefix)
        {
            return string.IsNullOrWhiteSpace(keyPrefix) ? "RenderCapture" : keyPrefix.Trim();
        }

        private static string PrefsKey(string keyPrefix, string key)
        {
            return HoyoToonEditorPrefs.ProjectKey($"{keyPrefix}.{key}");
        }

        private static string SerializeObject(Object target)
        {
            if (target == null)
                return string.Empty;

            return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
        }

        private static T ResolveObject<T>(string id)
            where T : Object
        {
            if (string.IsNullOrWhiteSpace(id) || !GlobalObjectId.TryParse(id, out GlobalObjectId globalObjectId))
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as T;
        }
    }
}
#endif
