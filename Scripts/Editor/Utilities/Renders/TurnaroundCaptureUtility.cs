#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Character.HSR;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Editor.Utilities.Renders
{
    internal static class TurnaroundCaptureUtility
    {
        public struct CameraState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float FieldOfView;
            public float NearClipPlane;
            public float FarClipPlane;
            public bool Orthographic;
            public float OrthographicSize;
        }

        public struct TurnaroundView
        {
            public Vector3 CameraOffsetDirection;
            public float LightYaw;

            public TurnaroundView(Vector3 cameraOffsetDirection, float lightYaw)
            {
                CameraOffsetDirection = cameraOffsetDirection;
                LightYaw = lightYaw;
            }
        }

        public struct TurnaroundLayout
        {
            public int CompositeWidth;
            public int CompositeHeight;
            public int PanelWidth;
            public int PanelHeight;
            public int StartX;
            public int StartY;
            public int Gap;
        }

        public sealed class TurnaroundCaptureScope : IDisposable
        {
            private readonly List<Renderer> m_DisabledRenderers = new List<Renderer>();
            private readonly List<Light> m_Lights = new List<Light>();
            private readonly List<Vector3> m_OriginalEulerAngles = new List<Vector3>();
            private readonly HSRCharacterController m_Controller;
            private readonly float m_OriginalSelfShadowFollow;
            private readonly bool m_HasOverriddenSelfShadow;

            public TurnaroundCaptureScope(GameObject activeModel, IEnumerable<Light> lights, float selfShadowOverride)
            {
                CaptureSceneRenderers(activeModel);
                CaptureDirectionalLights(lights);

                m_Controller = ResolveCharacterController(activeModel);
                if (m_Controller == null)
                {
                    return;
                }

                m_OriginalSelfShadowFollow = m_Controller.CharacterSelfShadowLightFollow;
                if (Mathf.Approximately(m_OriginalSelfShadowFollow, selfShadowOverride))
                {
                    return;
                }

                m_Controller.CharacterSelfShadowLightFollow = selfShadowOverride;
                m_Controller.SyncToRenderer();
                m_HasOverriddenSelfShadow = true;
            }

            public void ApplyView(TurnaroundView view)
            {
                float normalizedYaw = Mathf.Repeat(view.LightYaw, 360f);
                for (int index = 0; index < m_Lights.Count; index++)
                {
                    Light light = m_Lights[index];
                    if (light == null)
                    {
                        continue;
                    }

                    Vector3 originalEulerAngles = m_OriginalEulerAngles[index];
                    light.transform.localEulerAngles = new Vector3(
                        originalEulerAngles.x,
                        normalizedYaw,
                        originalEulerAngles.z);
                }
            }

            public void Dispose()
            {
                for (int index = 0; index < m_DisabledRenderers.Count; index++)
                {
                    Renderer renderer = m_DisabledRenderers[index];
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }

                for (int index = 0; index < m_Lights.Count; index++)
                {
                    Light light = m_Lights[index];
                    if (light != null)
                    {
                        light.transform.localEulerAngles = m_OriginalEulerAngles[index];
                    }
                }

                if (m_HasOverriddenSelfShadow && m_Controller != null)
                {
                    m_Controller.CharacterSelfShadowLightFollow = m_OriginalSelfShadowFollow;
                    m_Controller.SyncToRenderer();
                }

                m_DisabledRenderers.Clear();
                m_Lights.Clear();
                m_OriginalEulerAngles.Clear();
            }

            private void CaptureSceneRenderers(GameObject activeModel)
            {
                if (activeModel == null)
                {
                    return;
                }

                Transform activeRoot = activeModel.transform;
                UnityScene activeScene = activeModel.scene;
                Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                for (int index = 0; index < renderers.Length; index++)
                {
                    Renderer renderer = renderers[index];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (renderer.gameObject.scene != activeScene || renderer.transform.IsChildOf(activeRoot))
                    {
                        continue;
                    }

                    renderer.enabled = false;
                    m_DisabledRenderers.Add(renderer);
                }
            }

            private void CaptureDirectionalLights(IEnumerable<Light> lights)
            {
                if (lights == null)
                {
                    return;
                }

                foreach (Light light in lights)
                {
                    if (light == null || light.type != LightType.Directional)
                    {
                        continue;
                    }

                    m_Lights.Add(light);
                    m_OriginalEulerAngles.Add(light.transform.localEulerAngles);
                }
            }
        }

        public static bool TryBuildLayout(
            int outputWidth,
            int outputHeight,
            int panelCount,
            int gap,
            float outerPaddingPercent,
            int minOuterPadding,
            out TurnaroundLayout layout,
            out string validationMessage,
            string textureLimitMessage = null,
            string layoutMessage = null,
            string panelWidthMessage = null)
        {
            layout = default(TurnaroundLayout);
            validationMessage = null;

            int maxTextureSize = Mathf.Max(1, SystemInfo.maxTextureSize);
            if (outputWidth > maxTextureSize || outputHeight > maxTextureSize)
            {
                validationMessage = string.IsNullOrWhiteSpace(textureLimitMessage)
                    ? "Turnaround output exceeds the current texture size limit."
                    : string.Format(textureLimitMessage, maxTextureSize);
                return false;
            }

            panelCount = Mathf.Max(1, panelCount);
            gap = Mathf.Max(0, gap);

            int outerPadding = Mathf.Max(
                minOuterPadding,
                Mathf.RoundToInt(Mathf.Min(outputWidth, outputHeight) * Mathf.Max(0f, outerPaddingPercent)));
            int availableWidth = outputWidth - (outerPadding * 2) - (gap * (panelCount - 1));
            int availableHeight = outputHeight - (outerPadding * 2);
            if (availableWidth < panelCount || availableHeight < 1)
            {
                validationMessage = string.IsNullOrWhiteSpace(layoutMessage)
                    ? "Turnaround layout does not fit in the selected resolution."
                    : layoutMessage;
                return false;
            }

            int panelWidth = availableWidth / panelCount;
            if (panelWidth < 1)
            {
                validationMessage = string.IsNullOrWhiteSpace(panelWidthMessage)
                    ? "Turnaround panel width is too small."
                    : panelWidthMessage;
                return false;
            }

            int usedWidth = (panelWidth * panelCount) + (gap * (panelCount - 1));
            layout = new TurnaroundLayout
            {
                CompositeWidth = outputWidth,
                CompositeHeight = outputHeight,
                PanelWidth = panelWidth,
                PanelHeight = availableHeight,
                StartX = Mathf.Max(0, (outputWidth - usedWidth) / 2),
                StartY = outerPadding,
                Gap = gap
            };
            return true;
        }

        public static bool TryGetModelBounds(GameObject activeModel, out Bounds bounds)
        {
            bounds = default(Bounds);
            if (activeModel == null)
            {
                return false;
            }

            Renderer[] renderers = activeModel.GetComponentsInChildren<Renderer>(true);
            if (TryCollectBounds(renderers, requireVisibleRenderers: true, out bounds))
            {
                return true;
            }

            return TryCollectBounds(renderers, requireVisibleRenderers: false, out bounds);
        }

        public static HSRCharacterController ResolveCharacterController(GameObject activeModel)
        {
            return activeModel != null
                ? activeModel.GetComponentInChildren<HSRCharacterController>(true)
                : null;
        }

        public static TurnaroundView[] BuildViews(Transform modelTransform)
        {
            Vector3 up = Vector3.up;
            Vector3 forward = modelTransform != null
                ? Vector3.ProjectOnPlane(modelTransform.forward, up)
                : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }
            else
            {
                right.Normalize();
            }

            return new[]
            {
                new TurnaroundView(forward, 180f),
                new TurnaroundView(-right, 90f),
                new TurnaroundView(right, -90f),
                new TurnaroundView(-forward, 0f)
            };
        }

        public static float CalculateOrthographicSize(
            Bounds bounds,
            IReadOnlyList<TurnaroundView> views,
            int panelWidth,
            int panelHeight,
            float paddingMultiplier,
            float minPaddingMultiplier,
            float autoFitScale)
        {
            float aspect = panelWidth / (float)Mathf.Max(1, panelHeight);
            float requiredSize = 0.01f;

            if (views != null)
            {
                for (int index = 0; index < views.Count; index++)
                {
                    Vector3 right = Vector3.Cross(Vector3.up, views[index].CameraOffsetDirection);
                    if (right.sqrMagnitude < 0.0001f)
                    {
                        right = Vector3.right;
                    }
                    else
                    {
                        right.Normalize();
                    }

                    float horizontalExtent = CalculateProjectedExtent(bounds, right);
                    float verticalExtent = CalculateProjectedExtent(bounds, Vector3.up);
                    requiredSize = Mathf.Max(requiredSize, Mathf.Max(verticalExtent, horizontalExtent / aspect));
                }
            }

            return requiredSize
                * Mathf.Max(minPaddingMultiplier, paddingMultiplier)
                * autoFitScale;
        }

        public static void PositionCamera(
            Camera camera,
            CameraState originalState,
            Bounds bounds,
            TurnaroundView view,
            float orthographicSize)
        {
            if (camera == null)
            {
                return;
            }

            Vector3 offsetDirection = view.CameraOffsetDirection.sqrMagnitude < 0.0001f
                ? Vector3.forward
                : view.CameraOffsetDirection.normalized;
            Vector3 cameraForward = -offsetDirection;
            float depthExtent = CalculateProjectedExtent(bounds, cameraForward);
            float distancePadding = Mathf.Max(bounds.extents.magnitude * 1.5f, 1f);
            float distance = depthExtent + distancePadding;

            camera.transform.SetPositionAndRotation(
                bounds.center + (offsetDirection * distance),
                Quaternion.LookRotation(cameraForward, Vector3.up));
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(originalState.FarClipPlane, distance + depthExtent + distancePadding);
        }

        public static List<Light> GetDirectionalLights(GameObject activeModel)
        {
            var lights = new List<Light>();
            var seenLightIds = new HashSet<int>();

            void AddLightIfValid(Light light)
            {
                if (light == null || light.type != LightType.Directional)
                {
                    return;
                }

                if (!seenLightIds.Add(light.GetInstanceID()))
                {
                    return;
                }

                lights.Add(light);
            }

            HSRCharacterController controller = ResolveCharacterController(activeModel);
            if (controller != null)
            {
                AddLightIfValid(controller.SceneLight);
                AddLightIfValid(controller.CharacterLight);
                AddLightIfValid(controller.CharacterShadowLight);
            }

            AddLightIfValid(RenderSettings.sun);

            if (activeModel != null)
            {
                Light[] sceneLights = UnityEngine.Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int index = 0; index < sceneLights.Length; index++)
                {
                    Light light = sceneLights[index];
                    if (light == null || light.gameObject.scene != activeModel.scene)
                    {
                        continue;
                    }

                    AddLightIfValid(light);
                }
            }

            return lights;
        }

        public static Texture2D CreateComposite(
            IReadOnlyList<Texture2D> panels,
            Texture2D backgroundTexture,
            TurnaroundLayout layout)
        {
            Texture2D composite = ResizeTexture(backgroundTexture, layout.CompositeWidth, layout.CompositeHeight);
            if (composite == null)
            {
                composite = new Texture2D(layout.CompositeWidth, layout.CompositeHeight, TextureFormat.RGBA32, false);
                composite.SetPixels32(new Color32[layout.CompositeWidth * layout.CompositeHeight]);
                composite.Apply(false);
            }

            Color32[] compositePixels = composite.GetPixels32();
            int panelCount = panels != null ? panels.Count : 0;
            for (int panelIndex = 0; panelIndex < panelCount; panelIndex++)
            {
                Texture2D panel = panels[panelIndex];
                if (panel == null)
                {
                    continue;
                }

                int startX = layout.StartX + (panelIndex * (layout.PanelWidth + layout.Gap));
                AlphaBlendTexture(
                    compositePixels,
                    layout.CompositeWidth,
                    layout.CompositeHeight,
                    panel.GetPixels32(),
                    panel.width,
                    panel.height,
                    startX,
                    layout.StartY);
            }

            composite.SetPixels32(compositePixels);
            composite.Apply(false);
            return composite;
        }

        public static CameraState CaptureState(Camera camera)
        {
            if (camera == null)
            {
                return default(CameraState);
            }

            return new CameraState
            {
                Position = camera.transform.position,
                Rotation = camera.transform.rotation,
                FieldOfView = camera.fieldOfView,
                NearClipPlane = camera.nearClipPlane,
                FarClipPlane = camera.farClipPlane,
                Orthographic = camera.orthographic,
                OrthographicSize = camera.orthographicSize
            };
        }

        public static void ApplyCameraState(Camera camera, CameraState state)
        {
            if (camera == null)
            {
                return;
            }

            camera.transform.SetPositionAndRotation(state.Position, state.Rotation);
            camera.fieldOfView = state.FieldOfView;
            camera.nearClipPlane = state.NearClipPlane;
            camera.farClipPlane = state.FarClipPlane;
            camera.orthographic = state.Orthographic;
            camera.orthographicSize = state.OrthographicSize;
        }

        public static void RestoreState(Camera camera, CameraState state)
        {
            ApplyCameraState(camera, state);
        }

        private static bool TryCollectBounds(Renderer[] renderers, bool requireVisibleRenderers, out Bounds bounds)
        {
            bounds = default(Bounds);
            bool hasBounds = false;
            if (renderers == null)
            {
                return false;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (requireVisibleRenderers && (!renderer.enabled || !renderer.gameObject.activeInHierarchy))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static float CalculateProjectedExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 normalizedAxis = axis.sqrMagnitude < 0.0001f
                ? Vector3.up
                : axis.normalized;
            normalizedAxis = new Vector3(
                Mathf.Abs(normalizedAxis.x),
                Mathf.Abs(normalizedAxis.y),
                Mathf.Abs(normalizedAxis.z));
            return Vector3.Dot(bounds.extents, normalizedAxis);
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            if (source == null)
            {
                return null;
            }

            RenderTexture renderTexture = null;
            RenderTexture previousActive = null;
            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, renderTexture);
                previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;

                var resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                resizedTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                resizedTexture.Apply(false);
                return resizedTexture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        private static void AlphaBlendTexture(
            Color32[] destinationPixels,
            int destinationWidth,
            int destinationHeight,
            Color32[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            int startX,
            int startY)
        {
            const float InverseByte = 1f / 255f;

            if (destinationPixels == null || sourcePixels == null)
            {
                return;
            }

            for (int y = 0; y < sourceHeight; y++)
            {
                int destinationY = startY + y;
                if (destinationY < 0 || destinationY >= destinationHeight)
                {
                    continue;
                }

                int sourceRow = y * sourceWidth;
                int destinationRow = destinationY * destinationWidth;

                for (int x = 0; x < sourceWidth; x++)
                {
                    int destinationX = startX + x;
                    if (destinationX < 0 || destinationX >= destinationWidth)
                    {
                        continue;
                    }

                    Color32 source = sourcePixels[sourceRow + x];
                    if (source.a == 0)
                    {
                        continue;
                    }

                    int destinationIndex = destinationRow + destinationX;
                    if (source.a == byte.MaxValue)
                    {
                        destinationPixels[destinationIndex] = source;
                        continue;
                    }

                    Color32 destination = destinationPixels[destinationIndex];
                    float alpha = source.a * InverseByte;
                    float inverseAlpha = 1f - alpha;
                    destinationPixels[destinationIndex] = new Color32(
                        (byte)(source.r * alpha + destination.r * inverseAlpha),
                        (byte)(source.g * alpha + destination.g * inverseAlpha),
                        (byte)(source.b * alpha + destination.b * inverseAlpha),
                        (byte)Mathf.Min(source.a + destination.a * inverseAlpha, 255f));
                }
            }
        }
    }
}
#endif
