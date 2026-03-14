#if UNITY_EDITOR
using UnityEditor;

namespace HoyoToon.Editor.Utilities
{

    internal static class PrefsKeys
    {
        public const string UpdaterCurrentBranch = "HoyoToon.Updater.CurrentBranch";
        public const string UpdaterCleanOnSwitch = "HoyoToon.Updater.CleanOnSwitch";
        public const string DebugEnabled = "HoyoToon.Debug.Enabled";
        public const string ShaderPreprocessorLastVRC = "HoyoToon.ShaderPreprocessor.LastVRC";
        public const string RenderPipelineLast = "HoyoToon.RenderPipeline.Last";
        public const string ResourceFirstTimeSetup = "HoyoToon.Resource.FirstTimeSetup";
        public const string ResourceSuppressNotifications = "HoyoToon.Resource.SuppressNotifications";
        public const string PopupsShown = "HoyoToon.Popups.Shown";
        public const string RenderPrefix = "HoyoToon.Render.";
        public const string RenderResWidth = RenderPrefix + "ResWidth";
        public const string RenderResHeight = RenderPrefix + "ResHeight";
        public const string RenderScale = RenderPrefix + "Scale";
        public const string RenderSavePath = RenderPrefix + "SavePath";
        public const string RenderTransparent = RenderPrefix + "Transparent";
        public const string RenderOpenAfter = RenderPrefix + "OpenAfter";
        public const string RenderWatermark = RenderPrefix + "Watermark";
        public const string RenderCameraId = RenderPrefix + "CameraId";
        public const string ModelsDownloadRoot = "HoyoToon.ModelsDownloader.DownloadRoot";
        public const string ModelsAutoSetup = "HoyoToon.ModelsDownloader.AutoSetupAfterDownload";
        public const string TourShown = "HoyoToon.Tour.Shown";

        public static bool GetResourceFirstTimeSetupCompleted()
        {
            return EditorPrefs.GetBool(ResourceFirstTimeSetup, false);
        }

        public static void SetResourceFirstTimeSetupCompleted(bool value)
        {
            EditorPrefs.SetBool(ResourceFirstTimeSetup, value);
        }

        public static void ClearResourceFirstTimeSetup()
        {
            EditorPrefs.DeleteKey(ResourceFirstTimeSetup);
        }

        public static bool GetTourShown()
        {
            return EditorPrefs.GetBool(TourShown, false);
        }

        public static void SetTourShown(bool value)
        {
            EditorPrefs.SetBool(TourShown, value);
        }
    }
}
#endif
