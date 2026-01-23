#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Shared progress dialog helper for long-running editor tasks.
    /// </summary>
    public static class HoyoToonProgressDialog
    {
        private static HoyoToonDialogWindow s_Window;

        public static void Start(string title, string message)
        {
            try
            {
                if (s_Window == null)
                {
                    s_Window = HoyoToonDialogWindow.ShowProgress(title, message, MessageType.Info);
                }
                else
                {
                    s_Window.SetTitle(title);
                    s_Window.SetMessage(message);
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("ProgressDialog.Start", $"Progress dialog start failed: {ex.Message}");
                s_Window = null;
            }
        }

        public static void Update(float progress, string message = null)
        {
            try
            {
                if (s_Window == null) return;
                if (!string.IsNullOrEmpty(message)) s_Window.SetMessage(message);
                s_Window.UpdateProgress(progress, message);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("ProgressDialog.Update", $"Progress dialog update failed: {ex.Message}");
            }
        }

        public static void End(string completionMessage = null)
        {
            try
            {
                if (s_Window == null) return;
                if (!string.IsNullOrEmpty(completionMessage)) s_Window.CompleteProgress(completionMessage);
                s_Window.Close();
                s_Window = null;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("ProgressDialog.End", $"Progress dialog end failed: {ex.Message}");
                s_Window = null;
            }
        }
    }
}
#endif
