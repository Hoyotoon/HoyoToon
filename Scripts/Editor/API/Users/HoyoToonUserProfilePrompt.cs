using System;
using HoyoToon.Editor.UI.Dialogs;
using HoyoToon.Runtime.ScriptableObjects.Users;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.API.Users
{
    internal static class HoyoToonUserProfilePrompt
    {
        private const string DefaultUsername = "Traveler";

        internal static EditorWindow Show(Action<string> onSubmit, Action onClosed = null)
        {
            HoyoToonUserProfileSO localProfile = HoyoToonUserProfileStorage.GetLocalProfile();
            string initialUsername = !string.IsNullOrWhiteSpace(localProfile?.Username)
                ? localProfile.Username
                : DefaultUsername;
            TextField usernameField = null;
            Label validationLabel = null;

            return HoyoToonDialog.Show(new HoyoToonDialogOptions
            {
                Title = "Create HoyoToon Profile",
                Subtitle = "Local user setup",
                Message =
                    "Choose the username shown in your HoyoToon Manager. The HoyoToon API will assign the next numeric UID, and this install will store only your local profile as a ScriptableObject.",
                DialogType = HoyoToonDialogType.Question,
                Size = new Vector2(520f, 360f),
                Buttons = new[]
                {
                    HoyoToonDialogButton.Primary("Create Profile", 0),
                    HoyoToonDialogButton.Secondary("Cancel", 1),
                },
                BuildBody = body =>
                {
                    usernameField = new TextField("Username")
                    {
                        value = initialUsername,
                        isDelayed = false,
                    };
                    usernameField.AddToClassList("ht-field");
                    body.Add(usernameField);

                    validationLabel = new Label();
                    validationLabel.AddToClassList("ht-caption");
                    validationLabel.style.whiteSpace = WhiteSpace.Normal;
                    body.Add(validationLabel);

                    usernameField.RegisterValueChangedCallback(_ => UpdateValidation(usernameField, validationLabel));
                    UpdateValidation(usernameField, validationLabel);
                    usernameField.schedule.Execute(() =>
                    {
                        usernameField.Focus();
                    });
                },
                OnClosed = result =>
                {
                    try
                    {
                        if (result == 0)
                        {
                            onSubmit?.Invoke(usernameField != null ? usernameField.value : string.Empty);
                        }
                    }
                    finally
                    {
                        onClosed?.Invoke();
                    }
                },
            });
        }

        internal static EditorWindow ShowAvatarEditor(
            HoyoToonUserProfileSO profile,
            Action<string> onSubmit,
            Action onClosed = null)
        {
            string initialAvatar = string.IsNullOrWhiteSpace(profile?.Avatar)
                ? HoyoToonApi.DefaultUserAvatar
                : profile.Avatar.Trim();
            TextField avatarField = null;
            Label validationLabel = null;

            return HoyoToonDialog.Show(new HoyoToonDialogOptions
            {
                Title = "Edit HoyoToon Avatar",
                Subtitle = "Profile avatar URL",
                Message =
                    "Paste a public image URL for your profile avatar.",
                DialogType = HoyoToonDialogType.Question,
                Size = new Vector2(520f, 320f),
                Buttons = new[]
                {
                    HoyoToonDialogButton.Primary("Update Avatar", 0),
                    HoyoToonDialogButton.Ghost("Use Default", 2),
                    HoyoToonDialogButton.Secondary("Cancel", 1),
                },
                BuildBody = body =>
                {
                    avatarField = new TextField("Avatar URL")
                    {
                        value = initialAvatar,
                        isDelayed = false,
                    };
                    avatarField.AddToClassList("ht-field");
                    body.Add(avatarField);

                    validationLabel = new Label();
                    validationLabel.AddToClassList("ht-caption");
                    validationLabel.style.whiteSpace = WhiteSpace.Normal;
                    body.Add(validationLabel);

                    avatarField.RegisterValueChangedCallback(_ => UpdateAvatarValidation(avatarField, validationLabel));
                    UpdateAvatarValidation(avatarField, validationLabel);
                    avatarField.schedule.Execute(() =>
                    {
                        avatarField.Focus();
                    });
                },
                OnClosed = result =>
                {
                    try
                    {
                        if (result == 0)
                        {
                            onSubmit?.Invoke(avatarField != null ? avatarField.value : string.Empty);
                        }
                        else if (result == 2)
                        {
                            onSubmit?.Invoke(HoyoToonApi.DefaultUserAvatar);
                        }
                    }
                    finally
                    {
                        onClosed?.Invoke();
                    }
                },
            });
        }

        private static void UpdateValidation(TextField usernameField, Label validationLabel)
        {
            if (validationLabel == null)
            {
                return;
            }

            string value = usernameField != null ? usernameField.value : string.Empty;
            validationLabel.text = HoyoToonUserProfileService.TryNormalizeUsername(
                value,
                out string normalizedUsername,
                out string validationMessage)
                ? "This will be saved as '" + normalizedUsername + "'."
                : validationMessage;
        }

        private static void UpdateAvatarValidation(TextField avatarField, Label validationLabel)
        {
            if (validationLabel == null)
            {
                return;
            }

            string value = avatarField != null ? avatarField.value : string.Empty;
            validationLabel.text = HoyoToonUserProfileService.TryNormalizeAvatarUrl(
                value,
                out string normalizedAvatarUrl,
                out string validationMessage)
                ? "Avatar will be updated to:\n" + normalizedAvatarUrl
                : validationMessage;
        }
    }
}
