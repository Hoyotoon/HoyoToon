#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoyoToon.EditorTools.ManagerUI.Modules
{
    internal sealed class LightingModule : HoyoToonManagerModule
    {
        public override string DisplayName => "Lighting";

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
        private Vector2 _lightListScroll;
        private bool _lightCacheDirty = true;
        private bool _disposed;
        private bool _showSceneLights = true;
        private bool _showPrimaryControls = true;
        private LightType _pendingLightType = LightType.Directional;
        private bool _autoRotate;
        private float _autoRotateSpeed = DefaultAutoRotateSpeed;
        private AutoRotateDirection _autoRotateDirection = AutoRotateDirection.Clockwise;
        private double _lastAutoRotateTime;
        private double _nextAutoRotateTime;
        private double _nextAutoRotateRepaintTime;
        private float _manualYawCache = DefaultDirectionalYaw;

        public LightingModule()
        {
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.update += HandleEditorUpdate;
        }

        public override void OnGUI(HoyoToonManager targetManager)
        {
            EnsureSceneLights();

            _showSceneLights = DrawFoldoutSection("Scene Lights", _showSceneLights, () =>
            {
                DrawSceneLightsList();
                EditorGUILayout.Space(4f);
                DrawCreateLightControls();
            });

            EditorGUILayout.Space(8f);

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
#if UNITY_2020_1_OR_NEWER
            var lights = UnityEngine.Object.FindObjectsOfType<Light>(true);
#else
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
#endif
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

            using (var scroll = new EditorGUILayout.ScrollViewScope(_lightListScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(180)))
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

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All", EditorStyles.miniButton))
                {
                    _selectedLights.Clear();
                    foreach (var light in _sceneLights)
                    {
                        if (light) _selectedLights.Add(light);
                    }
                }

                if (GUILayout.Button("Clear Selection", EditorStyles.miniButton))
                {
                    _selectedLights.Clear();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawCreateLightControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _pendingLightType = (LightType)EditorGUILayout.EnumPopup("Create Light", _pendingLightType);
                using (new EditorGUI.DisabledScope(!Enum.IsDefined(typeof(LightType), _pendingLightType)))
                {
                    if (GUILayout.Button("Add Light", GUILayout.Width(90f)))
                    {
                        CreateLightOfType(_pendingLightType);
                    }
                }
            }
        }

        private void DrawPrimaryLightControls()
        {
            var targets = GetActiveTargets();
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
                CreateLightOfType(LightType.Directional);
            }
        }

        private List<Light> GetActiveTargets()
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

            foreach (var light in _sceneLights)
            {
                if (light)
                {
                    list.Add(light);
                    break;
                }
            }

            return list;
        }

        private List<Light> GetAutoRotateTargets()
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

            EditorGUI.BeginDisabledGroup(!useColorTemp);
            float newTemp = DrawColorTemperatureSlider(reference.colorTemperature);
            EditorGUI.EndDisabledGroup();
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
            EditorGUI.BeginDisabledGroup(!shadowsEnabled);

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

            EditorGUI.EndDisabledGroup();
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

                float newYaw = EditorGUILayout.Slider(Styles.RotationLabel, currentYaw, 0f, 360f);
                if (!Mathf.Approximately(newYaw, currentYaw))
                {
                    _manualYawCache = newYaw;
                    ApplyToLightTransforms(targets, "Adjust Light Rotation", t => t.localEulerAngles = new Vector3(0f, newYaw, 0f));
                }
            }

            bool newAutoRotate = EditorGUILayout.Toggle(Styles.AutoRotateLabel, _autoRotate);
            if (newAutoRotate != _autoRotate)
            {
                _autoRotate = newAutoRotate;
                _lastAutoRotateTime = EditorApplication.timeSinceStartup;
                _nextAutoRotateTime = _lastAutoRotateTime;
                _nextAutoRotateRepaintTime = _lastAutoRotateTime;
            }

            EditorGUI.BeginDisabledGroup(!_autoRotate);
            _autoRotateDirection = (AutoRotateDirection)EditorGUILayout.EnumPopup(Styles.AutoRotateDirectionLabel, _autoRotateDirection);
            float newAutoRotateSpeed = EditorGUILayout.Slider(Styles.AutoRotateSpeedLabel, _autoRotateSpeed, 0f, 180f);
            if (!Mathf.Approximately(newAutoRotateSpeed, _autoRotateSpeed))
            {
                _autoRotateSpeed = Mathf.Max(0f, newAutoRotateSpeed);
            }
            EditorGUI.EndDisabledGroup();

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

        private void CreateLightOfType(LightType type)
        {
            string lightName = $"HoyoToon {type} Light";
            var go = new GameObject(lightName, typeof(Light));
            Undo.RegisterCreatedObjectUndo(go, $"Create {type} Light");

            var light = go.GetComponent<Light>();
            light.type = type;
            light.color = Color.white;
            light.intensity = 1f;
            light.bounceIntensity = 1f;
            light.shadows = LightShadows.Soft;

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
                case LightType.Area:
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
            foreach (var light in targets)
            {
                if (!light) continue;
                Undo.RecordObject(light, undoLabel);
                mutate(light);
                EditorUtility.SetDirty(light);
            }
        }

        private static void ApplyToLightTransforms(List<Light> targets, string undoLabel, Action<Transform> mutate)
        {
            foreach (var light in targets)
            {
                if (!light) continue;
                Undo.RecordObject(light.transform, undoLabel);
                mutate(light.transform);
                EditorUtility.SetDirty(light.transform);
            }
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.update -= HandleEditorUpdate;
            if (_colorTemperatureTexture)
            {
                UnityEngine.Object.DestroyImmediate(_colorTemperatureTexture);
                _colorTemperatureTexture = null;
            }
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

            if (_autoRotateSpeed <= 0f)
            {
                _lastAutoRotateTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextAutoRotateTime)
            {
                return;
            }

            var targets = GetAutoRotateTargets();
            if (targets.Count == 0)
            {
                _lastAutoRotateTime = now;
                return;
            }

            _nextAutoRotateTime = now + AutoRotateUpdateInterval;
            float delta = (float)(now - _lastAutoRotateTime);
            if (delta <= 0f)
            {
                return;
            }

            _lastAutoRotateTime = now;
            float direction = _autoRotateDirection == AutoRotateDirection.Clockwise ? 1f : -1f;
            float deltaYaw = _autoRotateSpeed * delta * direction;

            foreach (var light in targets)
            {
                if (!light) continue;
                Transform t = light.transform;
                Vector3 euler = t.localEulerAngles;
                float newYaw = Mathf.Repeat(euler.y + deltaYaw, 360f);
                t.localEulerAngles = new Vector3(0f, newYaw, 0f);
            }

            if (!Application.isPlaying && now >= _nextAutoRotateRepaintTime)
            {
                _nextAutoRotateRepaintTime = now + AutoRotateRepaintInterval;
                SceneView.RepaintAll();
            }
        }
    }
}
#endif
