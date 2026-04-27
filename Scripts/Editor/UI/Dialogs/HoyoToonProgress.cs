#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.UI.Dialogs
{
    public static class HoyoToonProgress
    {
        public static void DisplayProgressBar(string title, string message, float progress)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            HoyoToonProgressWindow.GetOrCreate(title)
                .SetProgress(
                    NormalizeTitle(title),
                    NormalizeMessage(message),
                    NormalizeProgress(progress),
                    false);
        }

        public static bool DisplayCancelableProgressBar(string title, string message, float progress)
        {
            if (Application.isBatchMode)
            {
                return false;
            }

            HoyoToonProgressWindow progressWindow = HoyoToonProgressWindow.GetOrCreate(title);
            progressWindow.SetProgress(
                NormalizeTitle(title),
                NormalizeMessage(message),
                NormalizeProgress(progress),
                true);
            return progressWindow.CancelRequested;
        }

        public static void ClearProgressBar()
        {
            EditorUtility.ClearProgressBar();
            HoyoToonProgressWindow.CloseCurrent();
        }

        public static HoyoToonProgressScope Scope(string title)
        {
            return new HoyoToonProgressScope(title);
        }

        private static string NormalizeTitle(string title)
        {
            return string.IsNullOrWhiteSpace(title) ? "HoyoToon Progress" : title.Trim();
        }

        private static string NormalizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message) ? "Working..." : message.Trim();
        }

        private static float NormalizeProgress(float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress))
            {
                return 0f;
            }

            return Mathf.Clamp01(progress);
        }
    }

    public sealed class HoyoToonProgressScope : IDisposable
    {
        private readonly string title;
        private bool disposed;

        internal HoyoToonProgressScope(string title)
        {
            this.title = string.IsNullOrWhiteSpace(title) ? "HoyoToon Progress" : title.Trim();
        }

        public void Report(string message, float progress)
        {
            if (disposed)
            {
                return;
            }

            HoyoToonProgress.DisplayProgressBar(title, message, progress);
        }

        public bool ReportCancelable(string message, float progress)
        {
            if (disposed)
            {
                return false;
            }

            return HoyoToonProgress.DisplayCancelableProgressBar(title, message, progress);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            HoyoToonProgress.ClearProgressBar();
        }
    }
}
#endif
