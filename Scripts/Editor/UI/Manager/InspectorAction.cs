using System;

namespace HoyoToon.Editor.UI.Manager
{
    public sealed class InspectorAction
    {
        public InspectorAction(string label, Action callback, bool isEnabled, string styleKind)
        {
            Label = label;
            Callback = callback;
            IsEnabled = isEnabled;
            StyleKind = styleKind;
        }

        public string Label;
        public Action Callback;
        public bool IsEnabled;
        public string StyleKind;
    }
}
