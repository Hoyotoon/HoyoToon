using System.Collections;
using System.Collections.Generic;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Input;
using HoyoToon.Runtime.Scene.Placement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoyoToon.Runtime.Simulator.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    [AddComponentMenu("HoyoToon/Simulator/UI/Character Row UI")]
    public sealed class CharacterRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler
    {
        private const int DefaultMaxVisibleSlots = 10;
        private const float DefaultSlotSize = 112f;
        private const float DefaultSlotSpacing = 14f;
        private const float DefaultOverflowEdgePadding = 24f;
        private const float DefaultScrollWheelPixels = 90f;
        private const float DefaultSelectedVisualMaskPadding = 64f;
        private const float DefaultSelectedVisualVerticalMaskPadding = 16f;
        private const float DefaultElasticReturnSmoothTime = 0.14f;
        private const float RosterPollInterval = 0.2f;

        [SerializeField] private CharacterPlacementController placementController;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private CharacterSlotUI slotTemplate;
        [SerializeField] private int maxVisibleSlots = DefaultMaxVisibleSlots;
        [SerializeField] private float slotSize = DefaultSlotSize;
        [SerializeField] private float slotSpacing = DefaultSlotSpacing;
        [SerializeField] private float overflowEdgePadding = DefaultOverflowEdgePadding;
        [SerializeField] private float scrollWheelPixels = DefaultScrollWheelPixels;
        [SerializeField] private float selectedVisualMaskPadding = DefaultSelectedVisualMaskPadding;
        [SerializeField] private float selectedVisualVerticalMaskPadding = DefaultSelectedVisualVerticalMaskPadding;
        [SerializeField] private float elasticReturnSmoothTime = DefaultElasticReturnSmoothTime;
        [SerializeField] private bool blockCameraInputWhilePointerOver = true;

        private readonly List<CharacterSlotUI> m_Slots = new List<CharacterSlotUI>();
        private readonly List<GameObject> m_Models = new List<GameObject>();
        private readonly List<HSRCharacterController> m_ControllerScratch = new List<HSRCharacterController>();

        private bool m_IsDragging;
        private bool m_PointerInsideRow;
        private bool m_PointerInsideSlot;
        private bool m_SuppressSlotClick;
        private bool m_LayoutOverflowsViewport;
        private float m_NextRosterPollTime;
        private float m_MinRestingScrollOffset;
        private float m_MaxRestingScrollOffset;
        private float m_MinElasticScrollOffset;
        private float m_MaxElasticScrollOffset;
        private float m_ElasticReturnVelocity;
        private int m_LastRosterHash = int.MinValue;
        private int m_LastActiveIndex = int.MinValue;

        public bool ShouldSuppressSlotClick => m_SuppressSlotClick || m_IsDragging;

        private void Awake()
        {
            ResolveReferences();
            ConfigureScrollRect();
            CacheExistingSlots();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ConfigureScrollRect();
            CacheExistingSlots();

            if (Application.isPlaying)
                SyncRoster(force: true);
        }

        private void OnDisable()
        {
            SetCameraInputBlocked(false);
            StopAllCoroutines();
            m_IsDragging = false;
            m_PointerInsideRow = false;
            m_PointerInsideSlot = false;
            m_SuppressSlotClick = false;
            m_ElasticReturnVelocity = 0f;
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            MaintainScrollRectSettings();
            ResolvePlacementController();
            int activeIndex = placementController != null ? placementController.ActiveModelIndex : -1;
            if (activeIndex != m_LastActiveIndex)
            {
                ApplySelection(activeIndex);
                m_LastActiveIndex = activeIndex;
            }

            if (Time.unscaledTime < m_NextRosterPollTime)
                return;

            m_NextRosterPollTime = Time.unscaledTime + RosterPollInterval;
            int rosterHash = BuildRosterHash();
            if (rosterHash != m_LastRosterHash)
                SyncRoster(force: false);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            ConstrainContentToScrollBounds();
        }

        public void SelectSlot(CharacterSlotUI slot)
        {
            if (slot == null || slot.Index < 0)
                return;

            ResolvePlacementController();
            if (placementController != null && slot.Index < placementController.ManagedModels.Count)
                placementController.ActiveModelIndex = slot.Index;

            ApplySelection(slot.Index);
            m_LastActiveIndex = slot.Index;
        }

        public void SetPointerInsideSlot(bool inside)
        {
            m_PointerInsideSlot = inside;
            RefreshCameraInputBlock();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_PointerInsideRow = true;
            RefreshCameraInputBlock();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_PointerInsideRow = false;
            RefreshCameraInputBlock();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            m_IsDragging = true;
            m_SuppressSlotClick = true;
            m_ElasticReturnVelocity = 0f;
            RefreshCameraInputBlock();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            m_IsDragging = false;
            RefreshCameraInputBlock();
            if (isActiveAndEnabled && gameObject.activeInHierarchy)
                StartCoroutine(ClearClickSuppressionAfterFrame());
            else
                m_SuppressSlotClick = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null)
                return;

            float scrollDelta = Mathf.Abs(eventData.scrollDelta.x) > Mathf.Abs(eventData.scrollDelta.y)
                ? eventData.scrollDelta.x
                : -eventData.scrollDelta.y;

            if (!Mathf.Approximately(scrollDelta, 0f))
                ScrollContentBy(scrollDelta * Mathf.Max(1f, scrollWheelPixels));

            eventData.Use();
            RefreshCameraInputBlock();
        }

        private IEnumerator ClearClickSuppressionAfterFrame()
        {
            yield return null;
            m_SuppressSlotClick = false;
        }

        private void SyncRoster(bool force)
        {
            ResolveReferences();
            ResolvePlacementController();
            ConfigureScrollRect();
            CacheExistingSlots();
            RefreshModelList();
            EnsureSlotCapacity(m_Models.Count);

            for (int i = 0; i < m_Slots.Count; ++i)
            {
                bool visible = i < m_Models.Count;
                if (m_Slots[i].gameObject.activeSelf != visible)
                    m_Slots[i].gameObject.SetActive(visible);

                if (!visible)
                    continue;

                GameObject model = m_Models[i];
                m_Slots[i].name = ResolveSlotName(model, i);
                m_Slots[i].Bind(this, model, i, CharacterIconResolver.ResolveIcon(model));
            }

            float normalizedPosition = GetRestingNormalizedScrollOffset();
            bool wasOverflowing = m_LayoutOverflowsViewport;

            ConfigureLayout(m_Models.Count);
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                if (!m_LayoutOverflowsViewport)
                {
                    scrollRect.horizontalNormalizedPosition = 0f;
                }
                else if (force || !wasOverflowing)
                {
                    SetScrollOffset(m_MinRestingScrollOffset);
                }
                else
                {
                    SetScrollOffset(Mathf.Lerp(m_MinRestingScrollOffset, m_MaxRestingScrollOffset, Mathf.Clamp01(normalizedPosition)));
                }
            }

            int activeIndex = placementController != null ? placementController.ActiveModelIndex : -1;
            ApplySelection(activeIndex);
            m_LastRosterHash = BuildRosterHash();
            m_LastActiveIndex = activeIndex;
        }

        private void ApplySelection(int selectedIndex)
        {
            for (int i = 0; i < m_Slots.Count; ++i)
                m_Slots[i].SetSelected(i == selectedIndex && m_Slots[i].gameObject.activeSelf);
        }

        private void RefreshModelList()
        {
            m_Models.Clear();

            if (placementController != null)
            {
                placementController.EnsureRosterConsistency();
                IReadOnlyList<GameObject> managedModels = placementController.ManagedModels;
                for (int i = 0; i < managedModels.Count; ++i)
                {
                    if (managedModels[i] != null)
                        m_Models.Add(managedModels[i]);
                }
            }

            if (m_Models.Count > 0)
                return;

            HSRCharacterController.GetActiveControllersInScene(gameObject.scene, m_ControllerScratch, forceRefresh: true);
            for (int i = 0; i < m_ControllerScratch.Count; ++i)
            {
                HSRCharacterController controller = m_ControllerScratch[i];
                if (controller != null && controller.gameObject != null)
                    m_Models.Add(controller.gameObject);
            }
        }

        private int BuildRosterHash()
        {
            ResolvePlacementController();
            if (placementController != null)
                placementController.EnsureRosterConsistency();

            unchecked
            {
                int hash = 17;
                IReadOnlyList<GameObject> models = placementController != null
                    ? placementController.ManagedModels
                    : ResolveControllerFallbackModels();

                hash = hash * 31 + models.Count;
                for (int i = 0; i < models.Count; ++i)
                    hash = hash * 31 + (models[i] != null ? models[i].GetInstanceID() : 0);

                return hash;
            }
        }

        private IReadOnlyList<GameObject> ResolveControllerFallbackModels()
        {
            m_Models.Clear();
            HSRCharacterController.GetActiveControllersInScene(gameObject.scene, m_ControllerScratch, forceRefresh: false);
            for (int i = 0; i < m_ControllerScratch.Count; ++i)
            {
                HSRCharacterController controller = m_ControllerScratch[i];
                if (controller != null && controller.gameObject != null)
                    m_Models.Add(controller.gameObject);
            }

            return m_Models;
        }

        private void EnsureSlotCapacity(int count)
        {
            if (content == null)
                return;

            while (m_Slots.Count < count)
                m_Slots.Add(CharacterSlotUI.Create(content, slotTemplate));
        }

        private void CacheExistingSlots()
        {
            if (content == null || m_Slots.Count > 0)
                return;

            for (int i = 0; i < content.childCount; ++i)
            {
                CharacterSlotUI slot = content.GetChild(i).GetComponent<CharacterSlotUI>();
                if (slot == null)
                    slot = content.GetChild(i).gameObject.AddComponent<CharacterSlotUI>();

                m_Slots.Add(slot);
            }
        }

        private void ConfigureLayout(int itemCount)
        {
            if (content == null)
                return;

            int visibleSlots = Mathf.Max(1, maxVisibleSlots);
            float viewportWidth = visibleSlots * slotSize + Mathf.Max(0, visibleSlots - 1) * slotSpacing;
            float itemWidth = itemCount * slotSize + Mathf.Max(0, itemCount - 1) * slotSpacing;
            bool overflowsViewport = itemWidth > viewportWidth;
            float requestedEdgePadding = Mathf.Max(0f, overflowEdgePadding);
            float centeredEndPadding = Mathf.Max(0f, (viewportWidth - slotSize) * 0.5f);
            float edgePadding = overflowsViewport ? requestedEdgePadding : 0f;
            float elasticOverscrollOffset = overflowsViewport ? Mathf.Max(0f, centeredEndPadding - requestedEdgePadding) : 0f;
            float contentWidth = overflowsViewport ? itemWidth + edgePadding * 2f : viewportWidth;
            m_LayoutOverflowsViewport = overflowsViewport;
            ConfigureScrollBounds(overflowsViewport, contentWidth, viewportWidth, elasticOverscrollOffset);

            if (viewport != null)
                viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportWidth);

            Vector2 previousContentPosition = content.anchoredPosition;
            content.anchorMin = new Vector2(0f, 0.5f);
            content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, slotSize);
            content.anchoredPosition = overflowsViewport
                ? ClampContentPosition(previousContentPosition, allowElastic: false)
                : Vector2.zero;

            HorizontalLayoutGroup layoutGroup = content.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.spacing = slotSpacing;
                int roundedPadding = Mathf.RoundToInt(edgePadding);
                layoutGroup.padding = new RectOffset(roundedPadding, roundedPadding, 0, 0);
                layoutGroup.childAlignment = overflowsViewport ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                layoutGroup.childControlWidth = true;
                layoutGroup.childControlHeight = true;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            if (scrollRect != null)
                scrollRect.horizontal = overflowsViewport;

            for (int i = 0; i < m_Slots.Count; ++i)
                m_Slots[i].SetLayoutSize(slotSize);
        }

        private void ConfigureScrollRect()
        {
            if (scrollRect == null)
                scrollRect = GetComponent<ScrollRect>();

            if (scrollRect == null)
                return;

            if (!scrollRect.horizontal)
                scrollRect.horizontal = true;
            if (scrollRect.vertical)
                scrollRect.vertical = false;
            if (!Mathf.Approximately(scrollRect.scrollSensitivity, 0f))
                scrollRect.scrollSensitivity = 0f;

            if (viewport != null)
                scrollRect.viewport = viewport;
            if (content != null)
                scrollRect.content = content;
        }

        private void MaintainScrollRectSettings()
        {
            if (scrollRect == null)
                return;

            if (scrollRect.vertical)
                scrollRect.vertical = false;
            if (!Mathf.Approximately(scrollRect.scrollSensitivity, 0f))
                scrollRect.scrollSensitivity = 0f;

            if (viewport != null && scrollRect.viewport != viewport)
                scrollRect.viewport = viewport;
            if (content != null && scrollRect.content != content)
                scrollRect.content = content;
        }

        private void ResolveReferences()
        {
            if (scrollRect == null)
                scrollRect = GetComponent<ScrollRect>();

            if (viewport == null && scrollRect != null)
                viewport = scrollRect.viewport;

            if (viewport == null)
                viewport = transform.Find("Viewport") as RectTransform;

            if (viewport == null)
                viewport = CreateViewport();

            if (content == null && scrollRect != null)
                content = scrollRect.content;

            if (content == null && viewport != null)
                content = viewport.Find("Content") as RectTransform;

            if (content == null && viewport != null)
                content = CreateContent(viewport);

            ConfigureViewportObject();
            ConfigureContentObject();
        }

        private RectTransform CreateViewport()
        {
            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasRenderer), typeof(Image));
            viewportObject.transform.SetParent(transform, false);

            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            return viewportRect;
        }

        private static RectTransform CreateContent(RectTransform viewport)
        {
            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);

            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0.5f);
            contentRect.anchorMax = new Vector2(0f, 0.5f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            return contentRect;
        }

        private void ConfigureViewportObject()
        {
            if (viewport == null)
                return;

            RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
            if (rectMask == null)
                rectMask = viewport.gameObject.AddComponent<RectMask2D>();
            float horizontalPadding = Mathf.Max(0f, selectedVisualMaskPadding);
            float verticalPadding = Mathf.Max(0f, selectedVisualVerticalMaskPadding);
            rectMask.padding = new Vector4(-horizontalPadding, -verticalPadding, -horizontalPadding, -verticalPadding);
            rectMask.softness = Vector2Int.zero;

            Image viewportImage = viewport.GetComponent<Image>();
            if (viewportImage == null)
                viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.sprite = null;
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
        }

        private void ConfigureContentObject()
        {
            if (content == null)
                return;

            if (content.GetComponent<HorizontalLayoutGroup>() == null)
                content.gameObject.AddComponent<HorizontalLayoutGroup>();

            if (content.GetComponent<ContentSizeFitter>() == null)
                content.gameObject.AddComponent<ContentSizeFitter>();
        }

        private void ScrollContentBy(float pixelDelta)
        {
            if (!m_LayoutOverflowsViewport || content == null)
                return;

            SetScrollOffset(GetScrollOffset() - pixelDelta);

            if (scrollRect != null)
                scrollRect.velocity = Vector2.zero;
        }

        private void SetScrollOffset(float offset)
        {
            if (content == null)
                return;

            Vector2 position = content.anchoredPosition;
            position.x = -Mathf.Max(0f, offset);
            content.anchoredPosition = ClampContentPosition(position, allowElastic: false);

            if (scrollRect != null)
                scrollRect.velocity = Vector2.zero;
        }

        private void ConfigureScrollBounds(
            bool overflowsViewport,
            float contentWidth,
            float viewportWidth,
            float elasticOverscrollOffset)
        {
            if (!overflowsViewport)
            {
                m_MinRestingScrollOffset = 0f;
                m_MaxRestingScrollOffset = 0f;
                m_MinElasticScrollOffset = 0f;
                m_MaxElasticScrollOffset = 0f;
                return;
            }

            float scrollableWidth = Mathf.Max(0f, contentWidth - viewportWidth);
            float elasticOffset = Mathf.Max(0f, elasticOverscrollOffset);
            m_MinRestingScrollOffset = 0f;
            m_MaxRestingScrollOffset = scrollableWidth;
            m_MinElasticScrollOffset = -elasticOffset;
            m_MaxElasticScrollOffset = scrollableWidth + elasticOffset;
        }

        private void ConstrainContentToScrollBounds()
        {
            if (content == null)
                return;

            if (!m_LayoutOverflowsViewport)
            {
                SetContentAnchoredPosition(Vector2.zero);

                m_ElasticReturnVelocity = 0f;
                return;
            }

            ScrollRect.MovementType movementType = scrollRect != null
                ? scrollRect.movementType
                : ScrollRect.MovementType.Clamped;

            if (movementType == ScrollRect.MovementType.Unrestricted)
            {
                Vector2 unrestrictedPosition = content.anchoredPosition;
                unrestrictedPosition.y = 0f;
                SetContentAnchoredPosition(unrestrictedPosition);
                m_ElasticReturnVelocity = 0f;
                return;
            }

            bool allowElastic = movementType == ScrollRect.MovementType.Elastic;
            float offset = GetScrollOffset();
            float constrainedOffset = ClampScrollOffset(offset, allowElastic);

            if (!Mathf.Approximately(offset, constrainedOffset) && scrollRect != null)
                scrollRect.velocity = Vector2.zero;

            if (allowElastic && !m_IsDragging)
            {
                float restingOffset = ClampScrollOffset(constrainedOffset, allowElastic: false);
                if (!Mathf.Approximately(constrainedOffset, restingOffset))
                {
                    float smoothTime = Mathf.Max(0.01f, elasticReturnSmoothTime);
                    constrainedOffset = Mathf.SmoothDamp(
                        constrainedOffset,
                        restingOffset,
                        ref m_ElasticReturnVelocity,
                        smoothTime,
                        Mathf.Infinity,
                        Time.unscaledDeltaTime);

                    if (Mathf.Abs(constrainedOffset - restingOffset) < 0.5f && Mathf.Abs(m_ElasticReturnVelocity) < 0.5f)
                        constrainedOffset = restingOffset;

                    if (scrollRect != null)
                        scrollRect.velocity = Vector2.zero;
                }
                else
                {
                    m_ElasticReturnVelocity = 0f;
                }
            }
            else
            {
                m_ElasticReturnVelocity = 0f;
            }

            Vector2 position = content.anchoredPosition;
            position.x = -constrainedOffset;
            position.y = 0f;
            SetContentAnchoredPosition(position);
        }

        private Vector2 ClampContentPosition(Vector2 position, bool allowElastic)
        {
            if (content == null)
                return position;

            float offset = -position.x;
            position.x = -ClampScrollOffset(offset, allowElastic);
            position.y = 0f;
            return position;
        }

        private float ClampScrollOffset(float offset, bool allowElastic)
        {
            if (!m_LayoutOverflowsViewport)
                return 0f;

            float minimumOffset = allowElastic ? m_MinElasticScrollOffset : m_MinRestingScrollOffset;
            float maximumOffset = allowElastic ? m_MaxElasticScrollOffset : m_MaxRestingScrollOffset;
            return Mathf.Clamp(offset, minimumOffset, maximumOffset);
        }

        private float GetScrollOffset()
        {
            return content != null ? -content.anchoredPosition.x : 0f;
        }

        private float GetRestingNormalizedScrollOffset()
        {
            if (!m_LayoutOverflowsViewport)
                return 0f;

            float range = m_MaxRestingScrollOffset - m_MinRestingScrollOffset;
            return range > 0f
                ? Mathf.InverseLerp(m_MinRestingScrollOffset, m_MaxRestingScrollOffset, ClampScrollOffset(GetScrollOffset(), allowElastic: false))
                : 0f;
        }

        private void SetContentAnchoredPosition(Vector2 position)
        {
            if (content == null)
                return;

            Vector2 currentPosition = content.anchoredPosition;
            if (Mathf.Approximately(currentPosition.x, position.x)
                && Mathf.Approximately(currentPosition.y, position.y))
            {
                return;
            }

            content.anchoredPosition = position;
        }

        private CharacterPlacementController ResolvePlacementController()
        {
            if (placementController == null)
                placementController = CharacterPlacementController.GetPrimaryCachedOrFind();

            return placementController;
        }

        private void RefreshCameraInputBlock()
        {
            SetCameraInputBlocked(m_IsDragging || m_PointerInsideRow || m_PointerInsideSlot);
        }

        private void SetCameraInputBlocked(bool blocked)
        {
            if (!blockCameraInputWhilePointerOver)
                blocked = false;

            InputManager.SetCameraInputBlocked(this, blocked);
        }

        private static string ResolveSlotName(GameObject model, int index)
        {
            string modelName = model != null ? model.name : "Character";
            return modelName + " Slot " + index;
        }
    }
}

