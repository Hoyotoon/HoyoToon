using System;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif

namespace HoyoToon.Runtime.Simulator.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Simulator/UI/Help Bar UI")]
    public sealed class HoyoToonHelpBarUI : MonoBehaviour
    {
        private const string VersionUnavailableLabel = "Version unavailable";

        [SerializeField] private Text versionText;
        [SerializeField] private Text fpsText;
        [SerializeField] private string versionPrefix = "HoyoToon ";
        [SerializeField, Min(0.05f)] private float fpsRefreshInterval = 0.25f;
        [SerializeField] private Color highFpsColor = new Color(0.1f, 1f, 0.24f, 1f);
        [SerializeField] private Color mediumFpsColor = new Color(1f, 0.82f, 0.24f, 1f);
        [SerializeField] private Color lowFpsColor = new Color(1f, 0.32f, 0.24f, 1f);

        private float m_FpsElapsed;
        private int m_FpsFrames;

        private void Awake()
        {
            ResolveReferences();
            RefreshVersionText();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshVersionText();
            m_FpsElapsed = 0f;
            m_FpsFrames = 0;
        }

        private void Update()
        {
            if (!Application.isPlaying || fpsText == null)
                return;

            m_FpsElapsed += Time.unscaledDeltaTime;
            m_FpsFrames++;

            if (m_FpsElapsed < Mathf.Max(0.05f, fpsRefreshInterval))
                return;

            int fps = Mathf.RoundToInt(m_FpsFrames / Mathf.Max(0.0001f, m_FpsElapsed));
            fpsText.text = fps.ToString() + " FPS";
            fpsText.color = ResolveFpsColor(fps);
            m_FpsElapsed = 0f;
            m_FpsFrames = 0;
        }

        private void OnValidate()
        {
            fpsRefreshInterval = Mathf.Max(0.05f, fpsRefreshInterval);
            ResolveReferences();
            RefreshVersionText();
        }

        public void RefreshVersionText()
        {
            if (versionText == null)
                return;

            versionText.text = versionPrefix + ResolvePackageVersionLabel();
        }

        private void ResolveReferences()
        {
            if (versionText == null)
            {
                Transform versionTransform = transform.Find("Version");
                if (versionTransform == null)
                    versionTransform = transform.Find("Left Info");

                versionText = versionTransform != null ? versionTransform.GetComponent<Text>() : null;
            }

            if (fpsText == null)
            {
                Transform fpsTransform = transform.Find("FPS");
                fpsText = fpsTransform != null ? fpsTransform.GetComponent<Text>() : null;
            }
        }

        private Color ResolveFpsColor(int fps)
        {
            if (fps >= 50)
                return highFpsColor;

            return fps >= 30 ? mediumFpsColor : lowFpsColor;
        }

        private static string ResolvePackageVersionLabel()
        {
#if UNITY_EDITOR
            try
            {
                PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(HoyoToonHelpBarUI).Assembly);
                if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.version))
                    return NormalizeVersionLabel(packageInfo.version);
            }
            catch
            {
            }
#endif

            return NormalizeVersionLabel(Application.version);
        }

        private static string NormalizeVersionLabel(string version)
        {
            string normalizedVersion = string.IsNullOrWhiteSpace(version) ? VersionUnavailableLabel : version.Trim();
            if (string.Equals(normalizedVersion, VersionUnavailableLabel, StringComparison.Ordinal))
                return normalizedVersion;

            return normalizedVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? normalizedVersion
                : "v" + normalizedVersion;
        }
    }
}
