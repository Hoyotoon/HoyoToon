#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using HoyoToon.Editor.Onboarding;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class LightingModule : ManagerModule
    {
        static LightingModule()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CleanupStaticTexture;
        }

        private static void CleanupStaticTexture()
        {
            if (_colorTemperatureTexture)
            {
                UnityEngine.Object.DestroyImmediate(_colorTemperatureTexture);
                _colorTemperatureTexture = null;
            }
        }

        public override string DisplayName => "Lighting";
        internal override string NavbarTourTarget => "tour.modules.lighting";

        private static class Styles
        {
            public static readonly GUIContent UseColorTemperatureLabel = new GUIContent("Use color temperature mode");
            public static readonly GUIContent ColorFilterLabel = new GUIContent("Filter");
            public static readonly GUIContent ColorTemperatureLabel = new GUIContent("Temperature");
            public static readonly GUIContent ModeLabel = new GUIContent("Mode");
            public static readonly GUIContent IntensityLabel = new GUIContent("Intensity");
            public static readonly GUIContent IndirectLabel = new GUIContent("Indirect Multiplier");
            public static readonly GUIContent ShadowTypeLabel = new GUIContent("Shadow Type");
            public static readonly GUIContent ShadowResolutionLabel = new GUIContent("Resolution");
            public static readonly GUIContent ShadowStrengthLabel = new GUIContent("Strength");
            public static readonly GUIContent ShadowBiasLabel = new GUIContent("Bias");
            public static readonly GUIContent ShadowNormalBiasLabel = new GUIContent("Normal Bias");
            public static readonly GUIContent ShadowNearPlaneLabel = new GUIContent("Near Plane");
            public static readonly GUIContent RotationLabel = new GUIContent("Rotation");
            public static readonly GUIContent AutoRotateLabel = new GUIContent("Auto Rotate");
            public static readonly GUIContent AutoRotateSpeedLabel = new GUIContent("Auto Rotate Speed");
            public static readonly GUIContent AutoRotateDirectionLabel = new GUIContent("Direction");
        }

        private const float MinColorTemperature = 1000f;
        private const float MaxColorTemperature = 20000f;
        private const float DefaultDirectionalYaw = 180f;
        private const float DefaultAutoRotateSpeed = 50f;
        private static Texture2D _colorTemperatureTexture;
        private const float AutoRotateUpdateInterval = 1f / 30f;
        private const float AutoRotateRepaintInterval = 1f / 10f;

        private enum AutoRotateDirection
        {
            Clockwise,
            CounterClockwise
        }

        private readonly List<Light> _sceneLights = new List<Light>();
        private readonly HashSet<Light> _selectedLights = new HashSet<Light>();
        private readonly AutoRotateDriver _autoRotateDriver = new AutoRotateDriver(AutoRotateUpdateInterval, AutoRotateRepaintInterval);
        private Vector2 _lightListScroll;
        private bool _lightCacheDirty = true;
        private bool _disposed;
        private bool _showSceneLights = true;
        private bool _showPrimaryControls = true;
        private LightType _pendingLightType = LightType.Directional;
        private bool _autoRotate;
        private float _autoRotateSpeed = DefaultAutoRotateSpeed;
        private AutoRotateDirection _autoRotateDirection = AutoRotateDirection.Clockwise;
        private float _manualYawCache = DefaultDirectionalYaw;
        private Light _tutorialLight;
        private HoyoToonManager _targetManager;
        private List<Light> _autoRotateTargets;
        private readonly List<TransformSnapshot> _autoRotateSnapshots = new List<TransformSnapshot>();

        private readonly struct TransformSnapshot
        {
            public TransformSnapshot(Transform transform)
            {
                Transform = transform;
                LocalPosition = transform != null ? transform.localPosition : Vector3.zero;
                LocalRotation = transform != null ? transform.localRotation : Quaternion.identity;
                LocalScale = transform != null ? transform.localScale : Vector3.one;
            }

            public Transform Transform { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
        }

        private sealed class AutoRotateDriver
        {
            private readonly double _updateInterval;
            private readonly double _repaintInterval;
            private double _lastAutoRotateTime;
            private double _nextAutoRotateTime;
            private double _nextAutoRotateRepaintTime;

            public AutoRotateDriver(double updateInterval, double repaintInterval)
            {
                _updateInterval = updateInterval;
                _repaintInterval = repaintInterval;
            }

            public void ResetTiming(double now)
            {
                _lastAutoRotateTime = now;
                _nextAutoRotateTime = now;
                _nextAutoRotateRepaintTime = now;
            }

            public void Update(bool enabled, float speed, AutoRotateDirection direction, Func<List<Light>> targetProvider)
            {
                if (!enabled)
                {
                    return;
                }

                double now = EditorApplication.timeSinceStartup;
                if (speed <= 0f)
                {
                    _lastAutoRotateTime = now;
                    return;
                }

                if (now < _nextAutoRotateTime)
                {
                    return;
                }

                var targets = targetProvider != null ? targetProvider() : null;
                if (targets == null || targets.Count == 0)
                {
                    _lastAutoRotateTime = now;
                    return;
                }

                _nextAutoRotateTime = now + _updateInterval;
                float delta = (float)(now - _lastAutoRotateTime);
                if (delta <= 0f)
                {
                    return;
                }

                _lastAutoRotateTime = now;
                float sign = direction == AutoRotateDirection.Clockwise ? 1f : -1f;
                float deltaYaw = speed * delta * sign;

                for (int i = 0; i < targets.Count; i++)
                {
                    var light = targets[i];
                    if (!light)
                    {
                        continue;
                    }

                    var t = light.transform;
                    var euler = t.localEulerAngles;
                    float newYaw = Mathf.Repeat(euler.y + deltaYaw, 360f);
                    t.localEulerAngles = new Vector3(euler.x, newYaw, euler.z);
                }

                if (!Application.isPlaying && now >= _nextAutoRotateRepaintTime)
                {
                    _nextAutoRotateRepaintTime = now + _repaintInterval;
                    SceneView.RepaintAll();
                }
            }
        }

        public LightingModule()
        {
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.update += HandleEditorUpdate;
        }

        public override void OnGUI(HoyoToonManager targetManager)
        {
            _targetManager = targetManager;
            EnsureSceneLights();

            _showSceneLights = DrawFoldoutSection("Scene Lights", _showSceneLights, () =>
            {
                DrawSceneLightsList();
                DrawCreateLightControls();
            });

            EditorGUILayout.Space(4f);

            _showPrimaryControls = DrawFoldoutSection("Primary Sun Controls", _showPrimaryControls, DrawPrimaryLightControls);
        }

        private void EnsureSceneLights()
        {
            if (!_lightCacheDirty)
            {
                return;
            }

            RefreshSceneLights();
            _lightCacheDirty = false;
        }

        private void MarkLightsDirty()
        {
            _lightCacheDirty = true;
        }

        private void RefreshSceneLights()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _sceneLights.Clear();
            foreach (var light in lights)
            {
                if (light != null)
                {
                    _sceneLights.Add(light);
                }
            }

            _sceneLights.Sort((a, b) => string.CompareOrdinal(a ? a.name : string.Empty, b ? b.name : string.Empty));
            _selectedLights.RemoveWhere(l => l == null || !_sceneLights.Contains(l));
        }

        private void DrawSceneLightsList()
        {
            if (_sceneLights.Count == 0)
            {
                EditorGUILayout.HelpBox("No lights were detected in the scene.", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Lights ({_sceneLights.Count})", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.Width(40f)))
                {
                    _selectedLights.Clear();
                    foreach (var light in _sceneLights)
                    {
                        if (light) _selectedLights.Add(light);
                    }
                }

                if (GUILayout.Button("None", EditorStyles.miniButton, GUILayout.Width(50f)))
                {
                    _selectedLights.Clear();
                }
            }

            float rowHeight = EditorGUIUtility.singleLineHeight + 4f;
            float targetHeight = Mathf.Clamp(rowHeight * _sceneLights.Count, 36f, 140f);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_lightListScroll, GUILayout.MinHeight(targetHeight), GUILayout.MaxHeight(targetHeight)))
            {
                _lightListScroll = scroll.scrollPosition;
                foreach (var light in _sceneLights)
                {
                    if (!light) continue;
                    bool isSun = light == RenderSettings.sun;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool wasSelected = _selectedLights.Contains(light);
                        bool toggle = GUILayout.Toggle(wasSelected, GUIContent.none, GUILayout.Width(18f));
                        if (toggle != wasSelected)
                        {
                            if (toggle) _selectedLights.Add(light);
                            else _selectedLights.Remove(light);
                        }

                        GUILayout.Label(light.name, isSun ? EditorStyles.boldLabel : EditorStyles.label);
                        GUILayout.FlexibleSpace();

                        GUILayout.Label($"Int {light.intensity:0.##}", EditorStyles.miniLabel, GUILayout.Width(60f));

                        if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40f)))
                        {
                            EditorGUIUtility.PingObject(light);
                        }
                    }
                }
            }

            var listRect = GUILayoutUtility.GetLastRect();
            TourOverlay.DrawHighlightIfActive("tour.lighting.remove", listRect, "Remove Tutorial Light", onClick: RemoveTutorialLight);
            DrawInlineCalloutIfNeeded(StepIds.LightingRemove,
                "Click the highlighted area to remove the tutorial light.\n\nThe base scene lighting stays intact.");
        }

        private void DrawCreateLightControls()
        {
            DrawInlineCalloutIfNeeded(StepIds.LightingLightType,
                "Click the Light Type dropdown and choose a light.\n\nDifferent light types help preview how the model reads.");
            DrawInlineCalloutIfNeeded(StepIds.LightingAddLight,
                "Click Add Light to create the light in the scene.\n\nThis is a preview light and can be removed after.");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _pendingLightType = (LightType)EditorGUILayout.EnumPopup("Create", _pendingLightType);
                if (EditorGUI.EndChangeCheck() && string.Equals(GuidedTourController.CurrentStep.id, StepIds.LightingLightType, StringComparison.OrdinalIgnoreCase))
                {
                    GuidedTourController.NotifyLightingLightTypePicked();
                }
                var typeRect = GUILayoutUtility.GetLastRect();
                TourOverlay.DrawHighlightIfActive("tour.lighting.create.dropdown", typeRect, "Light Type");
                using (new EditorGUI.DisabledScope(!Enum.IsDefined(typeof(LightType), _pendingLightType)))
                {
                    if (GUILayout.Button("Add Light", GUILayout.Width(90f)))
                    {
                        bool isTourAdd = GuidedTourController.IsActive
                            && string.Equals(GuidedTourController.CurrentStep.id, StepIds.LightingAddLight, StringComparison.OrdinalIgnoreCase);
                        CreateLightOfType(_pendingLightType, isTourAdd);
                        if (isTourAdd)
                        {
                            GuidedTourController.NotifyLightingLightAdded();
                        }
                    }
                    var addRect = GUILayoutUtility.GetLastRect();
                    TourOverlay.DrawHighlightIfActive("tour.lighting.create.add", addRect, "Add Light");
                }
            }
        }

        private void DrawPrimaryLightControls()
        {
            var targets = GetLightTargets(includeSceneLightsFallback: true);
            if (targets.Count == 0)
            {
                DrawEmptyPrimaryLightState();
                return;
            }

            Light referenceLight = targets[0];
            if (!referenceLight)
            {
                EditorGUILayout.HelpBox("Primary light reference is missing.", MessageType.Warning);
                return;
            }

            DrawColorControls(targets);
            EditorGUILayout.Space();
            DrawIntensityControls(targets);
            EditorGUILayout.Space();
            DrawShadowControls(targets);
            EditorGUILayout.Space();
            DrawTransformControls(targets);
            EditorGUILayout.Space();
            DrawPrimaryLightFooter(referenceLight);
        }

        private void DrawEmptyPrimaryLightState()
        {
            EditorGUILayout.HelpBox("Select or add a directional light to begin editing.", MessageType.Info);
            if (GUILayout.Button("Create Directional Light"))
            {
                CreateLightOfType(LightType.Directional, false);
            }
        }

        private List<Light> GetLightTargets(bool includeSceneLightsFallback)
        {
            var list = new List<Light>();
            foreach (var light in _selectedLights)
            {
                if (light)
                {
                    list.Add(light);
                }
            }

            if (list.Count > 0)
            {
                return list;
            }

            var sun = RenderSettings.sun;
            if (sun && sun.type == LightType.Directional)
            {
                list.Add(sun);
                return list;
            }

            if (includeSceneLightsFallback)
            {
                foreach (var light in _sceneLights)
                {
                    if (light)
                    {
                        list.Add(light);
                        break;
                    }
                }
            }

            return list;
        }

        private void DrawColorControls(List<Light> targets)
        {
            Light reference = targets[0];
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            bool useColorTemp = EditorGUILayout.Toggle(Styles.UseColorTemperatureLabel, reference.useColorTemperature);
            if (useColorTemp != reference.useColorTemperature)
            {
                ApplyToLights(targets, "Toggle Color Temperature", l => l.useColorTemperature = useColorTemp);
            }

            Color newColor = EditorGUILayout.ColorField(Styles.ColorFilterLabel, reference.color);
            if (newColor != reference.color)
            {
                ApplyToLights(targets, "Change Light Color", l => l.color = newColor);
            }

            float newTemp;
            using (new EditorGUI.DisabledScope(!useColorTemp))
            {
                newTemp = DrawColorTemperatureSlider(reference.colorTemperature);
            }
            if (useColorTemp && !Mathf.Approximately(newTemp, reference.colorTemperature))
            {
                ApplyToLights(targets, "Adjust Color Temperature", l => l.colorTemperature = Mathf.Clamp(newTemp, MinColorTemperature, MaxColorTemperature));
            }

            EditorGUI.indentLevel--;
        }

        private void DrawIntensityControls(List<Light> targets)
        {
            Light reference = targets[0];
            EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            LightmapBakeType newBakeType = (LightmapBakeType)EditorGUILayout.EnumPopup(Styles.ModeLabel, reference.lightmapBakeType);
            if (newBakeType != reference.lightmapBakeType)
            {
                ApplyToLights(targets, "Change Light Mode", l => l.lightmapBakeType = newBakeType);
            }

            float newIntensity = EditorGUILayout.FloatField(Styles.IntensityLabel, reference.intensity);
            if (!Mathf.Approximately(newIntensity, reference.intensity))
            {
                ApplyToLights(targets, "Change Light Intensity", l => l.intensity = Mathf.Max(0f, newIntensity));
            }

            float newIndirect = EditorGUILayout.FloatField(Styles.IndirectLabel, reference.bounceIntensity);
            if (!Mathf.Approximately(newIndirect, reference.bounceIntensity))
            {
                ApplyToLights(targets, "Change Indirect Multiplier", l => l.bounceIntensity = Mathf.Max(0f, newIndirect));
            }

            EditorGUI.indentLevel--;
        }

        private void DrawShadowControls(List<Light> targets)
        {
            Light reference = targets[0];
            EditorGUILayout.LabelField("Realtime Shadows", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            LightShadows shadowType = (LightShadows)EditorGUILayout.EnumPopup(Styles.ShadowTypeLabel, reference.shadows);
            if (shadowType != reference.shadows)
            {
                ApplyToLights(targets, "Change Shadow Type", l => l.shadows = shadowType);
            }

            bool shadowsEnabled = shadowType != LightShadows.None;
            using (new EditorGUI.DisabledScope(!shadowsEnabled))
            {

            LightShadowResolution resolution = (LightShadowResolution)EditorGUILayout.EnumPopup(Styles.ShadowResolutionLabel, reference.shadowResolution);
            if (resolution != reference.shadowResolution)
            {
                ApplyToLights(targets, "Change Shadow Resolution", l => l.shadowResolution = resolution);
            }

            float newStrength = EditorGUILayout.Slider(Styles.ShadowStrengthLabel, reference.shadowStrength, 0f, 2f);
            if (!Mathf.Approximately(newStrength, reference.shadowStrength))
            {
                ApplyToLights(targets, "Change Shadow Strength", l => l.shadowStrength = newStrength);
            }

            float newBias = EditorGUILayout.Slider(Styles.ShadowBiasLabel, reference.shadowBias, 0f, 2f);
            if (!Mathf.Approximately(newBias, reference.shadowBias))
            {
                ApplyToLights(targets, "Change Shadow Bias", l => l.shadowBias = newBias);
            }

            float newNormalBias = EditorGUILayout.Slider(Styles.ShadowNormalBiasLabel, reference.shadowNormalBias, 0f, 3f);
            if (!Mathf.Approximately(newNormalBias, reference.shadowNormalBias))
            {
                ApplyToLights(targets, "Change Normal Bias", l => l.shadowNormalBias = newNormalBias);
            }

            float newNearPlane = EditorGUILayout.Slider(Styles.ShadowNearPlaneLabel, reference.shadowNearPlane, 0.01f, 10f);
            if (!Mathf.Approximately(newNearPlane, reference.shadowNearPlane))
            {
                ApplyToLights(targets, "Change Shadow Near Plane", l => l.shadowNearPlane = newNearPlane);
            }

            }
            EditorGUI.indentLevel--;
        }

        private void DrawTransformControls(List<Light> targets)
        {
            Transform reference = targets[0].transform;
            EditorGUILayout.LabelField("Rotation", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            using (new EditorGUI.DisabledScope(_autoRotate))
            {
                float currentYaw = _autoRotate ? _manualYawCache : reference.localEulerAngles.y;
                if (!_autoRotate)
                {
                    _manualYawCache = currentYaw;
                }

                DrawInlineCalloutIfNeeded(StepIds.LightingRotation,
                    "Drag Rotation to aim the key light.\n\nThis changes shadow direction and highlights.");
                float newYaw = EditorGUILayout.Slider(Styles.RotationLabel, currentYaw, 0f, 360f);
                var rotationRect = GUILayoutUtility.GetLastRect();
                TourOverlay.DrawHighlightIfActive("tour.lighting.rotation", rotationRect, "Rotation");
                if (!Mathf.Approximately(newYaw, currentYaw))
                {
                    _manualYawCache = newYaw;
                    ApplyToLightTransforms(targets, "Adjust Light Rotation", t => t.localEulerAngles = new Vector3(0f, newYaw, 0f));
                    if (string.Equals(GuidedTourController.CurrentStep.id, StepIds.LightingRotation, StringComparison.OrdinalIgnoreCase))
                    {
                        GuidedTourController.NotifyLightingRotationAdjusted();
                    }
                }
            }

            DrawInlineCalloutIfNeeded(StepIds.LightingAutoRotate,
                "Toggle Auto Rotate on, then toggle it off.\n\nAuto Rotate is a quick preview for moving light.");
            bool newAutoRotate = EditorGUILayout.Toggle(Styles.AutoRotateLabel, _autoRotate);
            var autoRotateRect = GUILayoutUtility.GetLastRect();
            TourOverlay.DrawHighlightIfActive("tour.lighting.autorotate", autoRotateRect, "Auto Rotate");
            if (newAutoRotate != _autoRotate)
            {
                SetAutoRotateState(newAutoRotate, targets);
                _autoRotateDriver.ResetTiming(EditorApplication.timeSinceStartup);
                if (string.Equals(GuidedTourController.CurrentStep.id, StepIds.LightingAutoRotate, StringComparison.OrdinalIgnoreCase))
                {
                    GuidedTourController.NotifyLightingAutoRotateToggled(_autoRotate);
                }
            }

            using (new EditorGUI.DisabledScope(!_autoRotate))
            {
                _autoRotateDirection = (AutoRotateDirection)EditorGUILayout.EnumPopup(Styles.AutoRotateDirectionLabel, _autoRotateDirection);
                float newAutoRotateSpeed = EditorGUILayout.Slider(Styles.AutoRotateSpeedLabel, _autoRotateSpeed, 0f, 180f);
                if (!Mathf.Approximately(newAutoRotateSpeed, _autoRotateSpeed))
                {
                    _autoRotateSpeed = Mathf.Max(0f, newAutoRotateSpeed);
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawPrimaryLightFooter(Light primary)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select In Hierarchy"))
                {
                    Selection.activeObject = primary.gameObject;
                }
            }
        }

        private void RemoveTutorialLight()
        {
            if (!GuidedTourController.IsActive
                || !string.Equals(GuidedTourController.CurrentStep.id, StepIds.LightingRemove, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_tutorialLight != null)
            {
                Undo.DestroyObjectImmediate(_tutorialLight.gameObject);
                _tutorialLight = null;
                MarkLightsDirty();
            }

            GuidedTourController.NotifyLightingLightRemoved();
        }

        private Light CreateLightOfType(LightType type, bool isTutorial)
        {
            string lightName = $"HoyoToon {type} Light";
            var go = new GameObject(lightName, typeof(Light));
            Undo.RegisterCreatedObjectUndo(go, $"Create {type} Light");

            var parent = ResolveLightsParent();
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var light = go.GetComponent<Light>();
            light.type = type;
            light.color = Color.white;
            light.intensity = 1f;
            light.bounceIntensity = 1f;
            light.shadows = LightShadows.Soft;
            if (isTutorial)
            {
                _tutorialLight = light;
            }

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;

            switch (type)
            {
                case LightType.Directional:
                    RenderSettings.sun = light;
                    rotation = Quaternion.Euler(0f, DefaultDirectionalYaw, 0f);
                    break;
                case LightType.Point:
                    light.range = 10f;
                    light.shadows = LightShadows.Soft;
                    position = new Vector3(0f, 2f, 0f);
                    break;
                case LightType.Spot:
                    light.range = 15f;
                    light.spotAngle = 45f;
                    position = new Vector3(0f, 2f, 0f);
                    rotation = Quaternion.Euler(50f, -30f, 0f);
                    break;
                case LightType.Rectangle:
                    light.intensity = 2000f;
                    position = new Vector3(0f, 2f, 0f);
                    break;
#if UNITY_2023_1_OR_NEWER
                case LightType.Disc:
                    position = new Vector3(0f, 2f, 0f);
                    break;
#endif
                default:
                    break;
            }

            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = Vector3.one;

            _selectedLights.Clear();
            _selectedLights.Add(light);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            MarkLightsDirty();
            return light;
        }

        private Transform ResolveLightsParent()
        {
            var manager = _targetManager;
            if (manager == null)
            {
                return null;
            }

            var lightsParent = manager.transform.Find("Lights");
            if (lightsParent != null)
            {
                return lightsParent;
            }

            var lightsObject = new GameObject("Lights");
            Undo.RegisterCreatedObjectUndo(lightsObject, "Create Lights Group");
            lightsObject.transform.SetParent(manager.transform, false);
            return lightsObject.transform;
        }

        private float DrawColorTemperatureSlider(float currentValue)
        {
            const float numericFieldWidth = 60f;
            Rect controlRect = EditorGUILayout.GetControlRect();
            float labelWidth = EditorGUIUtility.labelWidth;

            Rect labelRect = new Rect(controlRect.x, controlRect.y, labelWidth, controlRect.height);
            Rect sliderRect = new Rect(labelRect.xMax, controlRect.y + 2f, controlRect.width - labelWidth - numericFieldWidth - 4f, controlRect.height - 4f);
            sliderRect.width = Mathf.Max(50f, sliderRect.width);
            Rect fieldRect = new Rect(sliderRect.xMax + 4f, controlRect.y, numericFieldWidth, controlRect.height);

            EditorGUI.LabelField(labelRect, Styles.ColorTemperatureLabel);
            DrawColorTemperatureGradient(sliderRect);

            float normalized = Mathf.InverseLerp(MinColorTemperature, MaxColorTemperature, currentValue);
            EditorGUI.BeginChangeCheck();
            float sliderNormalized = GUI.HorizontalSlider(sliderRect, normalized, 0f, 1f, GUIStyle.none, GUI.skin.horizontalSliderThumb);
            bool sliderChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            int numericValue = EditorGUI.IntField(fieldRect, Mathf.RoundToInt(currentValue));
            bool numericChanged = EditorGUI.EndChangeCheck();

            if (sliderChanged)
            {
                return Mathf.Lerp(MinColorTemperature, MaxColorTemperature, sliderNormalized);
            }

            if (numericChanged)
            {
                return Mathf.Clamp(numericValue, MinColorTemperature, MaxColorTemperature);
            }

            return currentValue;
        }

        private void DrawColorTemperatureGradient(Rect sliderRect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            GUI.DrawTexture(sliderRect, GetColorTemperatureTexture(), ScaleMode.StretchToFill, true);
            var borderColor = new Color(0f, 0f, 0f, 0.35f);
            EditorGUI.DrawRect(new Rect(sliderRect.x, sliderRect.y, sliderRect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(sliderRect.x, sliderRect.yMax - 1f, sliderRect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(sliderRect.x, sliderRect.y, 1f, sliderRect.height), borderColor);
            EditorGUI.DrawRect(new Rect(sliderRect.xMax - 1f, sliderRect.y, 1f, sliderRect.height), borderColor);
        }

        private static Texture2D GetColorTemperatureTexture()
        {
            if (_colorTemperatureTexture == null)
            {
                _colorTemperatureTexture = GenerateColorTemperatureTexture();
            }

            return _colorTemperatureTexture;
        }

        private static Texture2D GenerateColorTemperatureTexture()
        {
            const int width = 256;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                float kelvin = Mathf.Lerp(MinColorTemperature, MaxColorTemperature, t);
                texture.SetPixel(x, 0, TemperatureToColor(kelvin));
            }

            texture.Apply(false, true);
            return texture;
        }

        private static Color TemperatureToColor(float kelvin)
        {
            kelvin = Mathf.Clamp(kelvin, MinColorTemperature, MaxColorTemperature) / 100f;

            float r;
            float g;
            float b;

            if (kelvin <= 66f)
            {
                r = 255f;
                g = 99.4708f * Mathf.Log(Mathf.Max(kelvin, 1f)) - 161.1196f;
                g = Mathf.Clamp(g, 0f, 255f);

                if (kelvin <= 19f)
                {
                    b = 0f;
                }
                else
                {
                    b = 138.5177f * Mathf.Log(Mathf.Max(kelvin - 10f, 0.1f)) - 305.0448f;
                    b = Mathf.Clamp(b, 0f, 255f);
                }
            }
            else
            {
                r = 329.6987f * Mathf.Pow(kelvin - 60f, -0.13320476f);
                r = Mathf.Clamp(r, 0f, 255f);

                g = 288.1222f * Mathf.Pow(kelvin - 60f, -0.07551485f);
                g = Mathf.Clamp(g, 0f, 255f);

                b = 255f;
            }

            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private static void ApplyToLights(List<Light> targets, string undoLabel, Action<Light> mutate)
        {
            ApplyToTargets(targets, undoLabel, light => light, mutate);
        }

        private static void ApplyToLightTransforms(List<Light> targets, string undoLabel, Action<Transform> mutate)
        {
            ApplyToTargets(targets, undoLabel, light => light != null ? light.transform : null, mutate);
        }

        private static void ApplyToTargets<T>(List<Light> targets, string undoLabel, Func<Light, T> selector, Action<T> mutate)
            where T : UnityEngine.Object
        {
            foreach (var light in targets)
            {
                if (!light)
                {
                    continue;
                }

                var target = selector(light);
                if (target == null)
                {
                    continue;
                }

                Undo.RecordObject(target, undoLabel);
                mutate(target);
                EditorUtility.SetDirty(target);
            }
        }

        private void SetAutoRotateState(bool enabled, List<Light> targets)
        {
            if (enabled == _autoRotate)
            {
                return;
            }

            if (enabled)
            {
                BeginAutoRotateSession(targets);
            }
            else
            {
                EndAutoRotateSession();
            }

            _autoRotate = enabled;
        }

        private void BeginAutoRotateSession(List<Light> targets)
        {
            _autoRotateSnapshots.Clear();
            _autoRotateTargets = null;

            if (targets == null || targets.Count == 0)
            {
                return;
            }

            _autoRotateTargets = new List<Light>(targets.Count);
            for (int i = 0; i < targets.Count; i++)
            {
                var light = targets[i];
                if (!light)
                {
                    continue;
                }

                _autoRotateTargets.Add(light);
                _autoRotateSnapshots.Add(new TransformSnapshot(light.transform));
            }
        }

        private void EndAutoRotateSession()
        {
            RestoreAutoRotateTransforms();
            _autoRotateTargets = null;
            _autoRotateSnapshots.Clear();
        }

        private void RestoreAutoRotateTransforms()
        {
            if (_autoRotateSnapshots.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _autoRotateSnapshots.Count; i++)
            {
                var snapshot = _autoRotateSnapshots[i];
                var transform = snapshot.Transform;
                if (!transform)
                {
                    continue;
                }

                Undo.RecordObject(transform, "Restore Auto Rotate Transform");
                transform.localPosition = snapshot.LocalPosition;
                transform.localRotation = snapshot.LocalRotation;
                transform.localScale = snapshot.LocalScale;
                EditorUtility.SetDirty(transform);
            }

            if (_autoRotateSnapshots.Count > 0)
            {
                var transform = _autoRotateSnapshots[0].Transform;
                if (transform)
                {
                    _manualYawCache = transform.localEulerAngles.y;
                }
            }

            SceneView.RepaintAll();
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_autoRotate)
            {
                SetAutoRotateState(false, null);
            }

            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.update -= HandleEditorUpdate;
            _disposed = true;
        }

        private void HandleHierarchyChanged()
        {
            MarkLightsDirty();
        }

        private void HandleBeforeAssemblyReload()
        {
            Dispose();
        }

        private void HandleEditorUpdate()
        {
            if (!_autoRotate || _disposed)
            {
                return;
            }

            _autoRotateDriver.Update(_autoRotate, _autoRotateSpeed, _autoRotateDirection, () => _autoRotateTargets);
        }
    }
}
#endif
