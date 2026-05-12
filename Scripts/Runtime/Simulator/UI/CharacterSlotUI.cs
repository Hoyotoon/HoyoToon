using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoyoToon.Runtime.Simulator.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Simulator/UI/Character Slot UI")]
    public sealed class CharacterSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const float DefaultSlotSize = 112f;
        private const string TemplateResourcePath = "UI/Game UI/Template";
        private const string TemplateAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/UI/Game UI/Template.prefab";

        [Header("Prefab References")]
        [SerializeField] private RectTransform rarityRight;
        [SerializeField] private GameObject selectedFront;
        [SerializeField] private Image iconMaskImage;
        [SerializeField] private Image selectedIconBackImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private RawImage iconRawImage;

        private CharacterRowUI m_Row;
        private GameObject m_Model;
        private Sprite m_Icon;
        private Animation m_SelectedFrontAnimation;
        private LayoutElement m_LayoutElement;
        private Image m_InputSurface;
        private Toggle m_Toggle;
        private Button m_Button;
        private EventTrigger m_EventTrigger;
        private Vector2 m_RarityRightUnselectedPosition;
        private Color m_SelectedIconBackUnselectedColor;
        private float m_LayoutSize = -1f;
        private bool m_InputSurfaceConfigured;
        private bool m_HasRarityRightUnselectedPosition;
        private bool m_HasSelectedIconBackUnselectedColor;
        private bool m_HasReferences;
        private bool m_Selected;
        private int m_Index = -1;

        public GameObject Model => m_Model;
        public int Index => m_Index;

        public static CharacterSlotUI Create(RectTransform parent, CharacterSlotUI template = null)
        {
            CharacterSlotUI slot = CreateFromTemplate(parent, template);
            if (slot == null)
                slot = CreateFallback(parent);

            slot.name = "Character Slot";
            slot.ResetRuntimeState();
            slot.SetLayoutSize(DefaultSlotSize);
            slot.EnsureReferences();
            slot.ConfigureInputSurface();
            slot.ApplySelected(false, force: true);
            return slot;
        }

        public void Bind(CharacterRowUI row, GameObject model, int index, Sprite icon)
        {
            m_Row = row;
            m_Model = model;
            m_Index = index;

            EnsureReferences();
            ConfigureInputSurface();

            if (m_Icon != icon)
                SetIcon(icon);

            ApplySelected(false, force: true);
        }

        public void SetLayoutSize(float size)
        {
            float resolvedSize = size > 0f ? size : DefaultSlotSize;
            bool sizeChanged = !Mathf.Approximately(m_LayoutSize, resolvedSize);

            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null && sizeChanged)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, resolvedSize);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, resolvedSize);
            }

            if (m_LayoutElement == null)
                m_LayoutElement = GetComponent<LayoutElement>();
            if (m_LayoutElement == null)
                m_LayoutElement = gameObject.AddComponent<LayoutElement>();

            bool layoutMatchesSize = Mathf.Approximately(m_LayoutElement.minWidth, resolvedSize)
                && Mathf.Approximately(m_LayoutElement.minHeight, resolvedSize)
                && Mathf.Approximately(m_LayoutElement.preferredWidth, resolvedSize)
                && Mathf.Approximately(m_LayoutElement.preferredHeight, resolvedSize)
                && Mathf.Approximately(m_LayoutElement.flexibleWidth, 0f)
                && Mathf.Approximately(m_LayoutElement.flexibleHeight, 0f)
                && m_LayoutElement.layoutPriority == 1;

            if (!sizeChanged && layoutMatchesSize)
                return;

            m_LayoutElement.minWidth = resolvedSize;
            m_LayoutElement.minHeight = resolvedSize;
            m_LayoutElement.preferredWidth = resolvedSize;
            m_LayoutElement.preferredHeight = resolvedSize;
            m_LayoutElement.flexibleWidth = 0f;
            m_LayoutElement.flexibleHeight = 0f;
            m_LayoutElement.layoutPriority = 1;
            m_LayoutSize = resolvedSize;
        }

        public void SetSelected(bool selected)
        {
            EnsureReferences();
            ApplySelected(selected, force: false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_Row == null || m_Row.ShouldSuppressSlotClick)
                return;

            m_Row.SelectSlot(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_Row?.SetPointerInsideSlot(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_Row?.SetPointerInsideSlot(false);
        }

        private static CharacterSlotUI CreateFromTemplate(RectTransform parent, CharacterSlotUI template)
        {
            if (template == null)
                template = SlotTemplateResources.TemplateSlot;

            if (template == null)
                return null;

            return Object.Instantiate(template, parent, false);
        }

        private static CharacterSlotUI CreateFallback(RectTransform parent)
        {
            GameObject slotObject = new GameObject("Character Slot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            if (parent != null)
                slotObject.transform.SetParent(parent, false);

            return slotObject.AddComponent<CharacterSlotUI>();
        }

        private void ResetRuntimeState()
        {
            m_Row = null;
            m_Model = null;
            m_Icon = null;
            m_SelectedFrontAnimation = null;
            m_LayoutElement = null;
            m_InputSurface = null;
            m_Toggle = null;
            m_Button = null;
            m_EventTrigger = null;
            m_HasRarityRightUnselectedPosition = false;
            m_HasSelectedIconBackUnselectedColor = false;
            m_LayoutSize = -1f;
            m_InputSurfaceConfigured = false;
            m_HasReferences = false;
            m_Selected = false;
            m_Index = -1;
        }

        private void EnsureReferences()
        {
            if (m_HasReferences)
                return;

            ResolveReferences();
            CachePrefabState();
            m_HasReferences = true;
        }

        private void ResolveReferences()
        {
            rarityRight = rarityRight != null ? rarityRight : FindChildByName(transform, "Rarity Right") as RectTransform;
            selectedFront = selectedFront != null ? selectedFront : FindChildByName(transform, "Selected Front")?.gameObject;

            if (iconMaskImage == null)
            {
                Transform maskTransform = FindChildByName(transform, "Icon Mask");
                if (maskTransform == null)
                    maskTransform = FindChildByName(transform, "Selected Icon Back");

                iconMaskImage = maskTransform != null ? maskTransform.GetComponent<Image>() : null;
            }

            selectedIconBackImage = selectedIconBackImage != null
                ? selectedIconBackImage
                : FindChildByName(transform, "Selected Icon Back")?.GetComponent<Image>();

            Transform iconTransform = FindChildByName(transform, "Icon");
            if (iconTransform != null)
            {
                iconRawImage = iconRawImage != null ? iconRawImage : iconTransform.GetComponent<RawImage>();
                iconImage = iconImage != null ? iconImage : iconTransform.GetComponent<Image>();
            }

            m_SelectedFrontAnimation = selectedFront != null
                ? selectedFront.GetComponentInChildren<Animation>(true)
                : null;
        }

        private void CachePrefabState()
        {
            if (rarityRight != null && !m_HasRarityRightUnselectedPosition)
            {
                m_RarityRightUnselectedPosition = rarityRight.anchoredPosition;
                m_HasRarityRightUnselectedPosition = true;
            }

            if (selectedIconBackImage != null && !m_HasSelectedIconBackUnselectedColor)
            {
                m_SelectedIconBackUnselectedColor = selectedIconBackImage.color;
                m_HasSelectedIconBackUnselectedColor = true;
            }
        }

        private void ConfigureInputSurface()
        {
            if (m_InputSurfaceConfigured)
                return;

            if (m_InputSurface == null)
                m_InputSurface = GetComponent<Image>();
            if (m_InputSurface == null)
                m_InputSurface = gameObject.AddComponent<Image>();

            m_InputSurface.sprite = null;
            m_InputSurface.color = Color.clear;
            m_InputSurface.raycastTarget = true;

            if (m_Toggle == null)
                m_Toggle = GetComponent<Toggle>();
            if (m_Toggle != null)
            {
                m_Toggle.SetIsOnWithoutNotify(false);
                m_Toggle.enabled = false;
            }

            if (m_Button == null)
                m_Button = GetComponent<Button>();
            if (m_Button != null)
                m_Button.enabled = false;

            if (m_EventTrigger == null)
                m_EventTrigger = GetComponent<EventTrigger>();
            if (m_EventTrigger != null)
                m_EventTrigger.enabled = false;

            m_InputSurfaceConfigured = true;
        }

        private void SetIcon(Sprite icon)
        {
            m_Icon = icon;

            if (iconRawImage != null)
            {
                iconRawImage.texture = icon != null ? icon.texture : null;
                iconRawImage.uvRect = icon != null ? CalculateSpriteUvRect(icon) : new Rect(0f, 0f, 1f, 1f);
                iconRawImage.enabled = icon != null && icon.texture != null;
                iconRawImage.raycastTarget = false;
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
        }

        private void ApplySelected(bool selected, bool force)
        {
            if (!force && m_Selected == selected)
                return;

            m_Selected = selected;

            if (rarityRight != null && m_HasRarityRightUnselectedPosition)
            {
                Vector2 rarityPosition = m_RarityRightUnselectedPosition;
                if (selected)
                    rarityPosition.x = -rarityPosition.x;

                rarityRight.anchoredPosition = rarityPosition;
            }

            if (selectedFront != null)
                selectedFront.SetActive(selected);

            if (selectedIconBackImage != null && m_HasSelectedIconBackUnselectedColor)
                selectedIconBackImage.color = selected ? Color.white : m_SelectedIconBackUnselectedColor;

            if (m_SelectedFrontAnimation == null)
                return;

            if (selected)
            {
                m_SelectedFrontAnimation.enabled = true;
                if (m_SelectedFrontAnimation.clip != null)
                    m_SelectedFrontAnimation.Play();
            }
            else
            {
                m_SelectedFrontAnimation.Stop();
            }
        }

        private static Rect CalculateSpriteUvRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Rect(0f, 0f, 1f, 1f);

            Rect rect = sprite.textureRect;
            Texture texture = sprite.texture;
            return new Rect(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height);
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; ++i)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                    return child;

                Transform nested = FindChildByName(child, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static class SlotTemplateResources
        {
            private static GameObject s_TemplateObject;

            public static CharacterSlotUI TemplateSlot
            {
                get
                {
                    GameObject templateObject = LoadTemplateObject();
                    return templateObject != null ? templateObject.GetComponent<CharacterSlotUI>() : null;
                }
            }

            private static GameObject LoadTemplateObject()
            {
                if (s_TemplateObject != null)
                    return s_TemplateObject;

                s_TemplateObject = Resources.Load<GameObject>(TemplateResourcePath);
#if UNITY_EDITOR
                if (s_TemplateObject == null)
                    s_TemplateObject = AssetDatabase.LoadAssetAtPath<GameObject>(TemplateAssetPath);
#endif
                return s_TemplateObject;
            }
        }
    }
}

