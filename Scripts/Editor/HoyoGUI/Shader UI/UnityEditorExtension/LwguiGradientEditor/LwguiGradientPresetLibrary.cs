// Copyright (c) Jason Ma

using System.Collections.Generic;
using LWGUI.Runtime.LwguiGradient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace LWGUI.LwguiGradientEditor
{
    [ExcludeFromPreset]
    class LwguiGradientPresetLibrary : PresetLibrary
    {
        [SerializeField]
        List<LwguiGradientPreset> m_Presets = new List<LwguiGradientPreset>();

        public override int Count()
        {
            return m_Presets.Count;
        }

        public override object GetPreset(int index)
        {
            return m_Presets[index].lwguiGradient;
        }

        public override void Add(object presetObject, string presetName)
        {
            LwguiGradient gradient = presetObject as LwguiGradient;
            if (gradient == null)
            {
                Debug.LogError("Wrong type used in LwguiGradientPresetLibrary");
                return;
            }

            m_Presets.Add(new LwguiGradientPreset(new LwguiGradient(gradient), presetName));
        }

        public override void Replace(int index, object newPresetObject)
        {
            LwguiGradient gradient = newPresetObject as LwguiGradient;
            if (gradient == null)
            {
                Debug.LogError("Wrong type used in LwguiGradientPresetLibrary");
                return;
            }

            m_Presets[index].lwguiGradient = new LwguiGradient(gradient);
        }

        public override void Remove(int index)
        {
            m_Presets.RemoveAt(index);
        }

        public override void Move(int index, int destIndex, bool insertAfterDestIndex)
        {
            PresetLibraryHelpers.MoveListItem(m_Presets, index, destIndex, insertAfterDestIndex);
        }

        public override void Draw(Rect rect, int index)
        {
            Draw(rect, m_Presets[index].lwguiGradient, ColorSpace.Gamma, LwguiGradient.ChannelMask.All);
        }

        public override void Draw(Rect rect, object presetObject)
        {
            Draw(rect, presetObject as LwguiGradient, ColorSpace.Gamma, LwguiGradient.ChannelMask.All);
        }

        public void Draw(Rect rect, LwguiGradient gradient, ColorSpace colorSpace, LwguiGradient.ChannelMask viewChannelMask)
        {
            if (gradient == null)
                return;
            LwguiGradientEditorHelper.DrawGradientWithSeparateAlphaChannel(rect, gradient, colorSpace, viewChannelMask);
        }

        public override string GetName(int index)
        {
            return m_Presets[index].name;
        }

        public override void SetName(int index, string presetName)
        {
            m_Presets[index].name = presetName;
        }

        
        [System.Serializable]
        class LwguiGradientPreset
        {
            [SerializeField]
            string m_Name;

            [SerializeField]
            LwguiGradient m_LwguiGradient;

            public LwguiGradientPreset(LwguiGradient preset, string presetName)
            {
                lwguiGradient = preset;
                name = presetName;
            }

            public LwguiGradient lwguiGradient
            {
                get => m_LwguiGradient;
                set => m_LwguiGradient = value;
            }

            public string name
            {
                get => m_Name;
                set => m_Name = value;
            }
        }
    }
}
