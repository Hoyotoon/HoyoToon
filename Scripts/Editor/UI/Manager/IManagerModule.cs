using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Manager
{
    public interface IManagerModule
    {
        string Id { get; }
        string DisplayName { get; }
        int Order { get; }

        VisualElement CreateContent(ModuleContext context);
        void OnSelected(ModuleContext context);
        void OnDeselected(ModuleContext context);
    }
}
