#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.Windows
{
    public static class ProgressDialog
    {
        private static DialogWindow s_Window;

        public static void Start(string title, string message, Action onCancel = null)
        {
            try
            {
                if (s_Window == null)
                {
                    s_Window = DialogWindow.ShowProgress(title, message, MessageType.Info, onCancel: onCancel);
                }
                else
                {
                    s_Window.SetTitle(title);
                    s_Window.ConfigureProgressCancellation(onCancel);
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
                s_Window.SuppressProgressCloseCancel();
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
