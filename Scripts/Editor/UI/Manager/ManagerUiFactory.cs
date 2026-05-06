#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Manager
{
    internal static class ManagerUiFactory
    {
        internal static VisualElement CreateDivider()
        {
            VisualElement divider = new VisualElement();
            divider.AddToClassList("ht-divider");
            return divider;
        }

        internal static VisualElement CreateActionRow(params VisualElement[] children)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ht-row");
            row.AddToClassList("ht-gap-8");

            for (int i = 0; i < (children != null ? children.Length : 0); ++i)
            {
                if (children[i] != null)
                    row.Add(children[i]);
            }

            return row;
        }

        internal static Button CreateIconButton(Texture2D icon, string tooltip, Action clicked)
        {
            Button button = new Button(clicked);
            button.AddToClassList("ht-icon-button");
            button.tooltip = tooltip ?? string.Empty;

            if (icon != null)
            {
                Image image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("ht-icon-button-image");
                button.Add(image);
            }

            return button;
        }

        internal static Foldout CreatePersistentFoldout(string title, string prefsKey, bool defaultValue = true)
        {
            Foldout foldout = new Foldout
            {
                text = title ?? string.Empty,
                value = !string.IsNullOrWhiteSpace(prefsKey)
                    ? EditorPrefs.GetBool(prefsKey, defaultValue)
                    : defaultValue
            };

            if (!string.IsNullOrWhiteSpace(prefsKey))
            {
                foldout.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(prefsKey, evt.newValue);
                });
            }

            return foldout;
        }
    }
}
#endif
