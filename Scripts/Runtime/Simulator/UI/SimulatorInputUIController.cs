using System;
using HoyoToon.Runtime.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HoyoToon.Runtime.Simulator.UI
{
    internal static class SimulatorInputUIController
    {
        private const string HelpBarName = "HelpBar";
        private const string LegacyHelpBarName = "Help Bar";
        private const string ControlsName = "Controls";
        private const string PreferredUiRootPath = "HoyoToon/UI";
        private const string LegacyUiRootName = "UI";

        private static bool s_RuntimeUiVisible = true;
        private static bool s_HelpBarVisible = true;
        private static GameObject s_RuntimeUiRoot;
        private static GameObject s_HelpBarObject;
        private static InputManager s_InputManager;
        private static bool s_IsInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindInputManager();

            s_RuntimeUiVisible = true;
            s_HelpBarVisible = true;
            s_RuntimeUiRoot = null;
            s_HelpBarObject = null;
            s_IsInitialized = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (s_IsInitialized)
                return;

            s_IsInitialized = true;
            if (!Application.isPlaying)
                return;

            BindInputManager();
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyRuntimeUiVisibility();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (!Application.isPlaying)
                return;

            ApplyRuntimeUiVisibility();
        }

        private static void BindInputManager()
        {
            if (s_InputManager != null)
                return;

            s_InputManager = InputManager.Instance;
            if (s_InputManager == null)
                return;

            s_InputManager.ToggleUiPressed += HandleToggleUiPressed;
            s_InputManager.ToggleHelpBarPressed += HandleToggleHelpBarPressed;
        }

        private static void UnbindInputManager()
        {
            if (s_InputManager == null)
                return;

            s_InputManager.ToggleUiPressed -= HandleToggleUiPressed;
            s_InputManager.ToggleHelpBarPressed -= HandleToggleHelpBarPressed;
            s_InputManager = null;
        }

        private static void HandleToggleUiPressed()
        {
            SetRuntimeUiVisible(!s_RuntimeUiVisible);
        }

        private static void HandleToggleHelpBarPressed()
        {
            SetHelpBarVisible(!s_HelpBarVisible);
        }

        private static void SetRuntimeUiVisible(bool visible)
        {
            if (s_RuntimeUiVisible == visible)
                return;

            s_RuntimeUiVisible = visible;
            ApplyRuntimeUiVisibility();
        }

        private static void SetHelpBarVisible(bool visible)
        {
            if (s_HelpBarVisible == visible)
                return;

            s_HelpBarVisible = visible;
            ApplyRuntimeUiVisibility();
        }

        private static void ApplyRuntimeUiVisibility()
        {
            GameObject uiRoot = ResolveRuntimeUiRoot();
            GameObject helpBarObject = ResolveHelpBarObject(uiRoot);
            bool helpBarVisible = s_RuntimeUiVisible && s_HelpBarVisible;

            if (uiRoot == null)
            {
                SetHelpBarActive(helpBarObject, helpBarVisible);
                return;
            }

            Transform uiTransform = uiRoot.transform;
            for (int index = 0; index < uiTransform.childCount; index++)
            {
                Transform child = uiTransform.GetChild(index);
                if (child == null)
                    continue;

                bool childVisible = child.gameObject == helpBarObject ? helpBarVisible : s_RuntimeUiVisible;
                if (child.gameObject.activeSelf != childVisible)
                    child.gameObject.SetActive(childVisible);
            }

            SetHelpBarActive(helpBarObject, helpBarVisible);
        }

        private static GameObject ResolveRuntimeUiRoot()
        {
            if (s_RuntimeUiRoot != null && IsLoadedSceneObject(s_RuntimeUiRoot.transform))
                return s_RuntimeUiRoot;

            s_RuntimeUiRoot = null;
            s_HelpBarObject = null;

            s_RuntimeUiRoot = GameObject.Find(PreferredUiRootPath);
            if (s_RuntimeUiRoot == null)
                s_RuntimeUiRoot = GameObject.Find(LegacyUiRootName);
            if (s_RuntimeUiRoot == null)
                s_RuntimeUiRoot = FindSceneTransform(IsPreferredRuntimeUiRoot)?.gameObject;
            if (s_RuntimeUiRoot == null)
                s_RuntimeUiRoot = FindSceneTransform(transform => string.Equals(transform.name, LegacyUiRootName, StringComparison.Ordinal))?.gameObject;
            if (s_RuntimeUiRoot == null)
            {
                GameObject helpBarObject = ResolveHelpBarObject(null);
                Transform helpBarParent = helpBarObject != null ? helpBarObject.transform.parent : null;
                s_RuntimeUiRoot = helpBarParent != null ? helpBarParent.gameObject : null;
            }

            return s_RuntimeUiRoot;
        }

        private static GameObject ResolveHelpBarObject(GameObject uiRoot)
        {
            if (s_HelpBarObject != null && IsLoadedSceneObject(s_HelpBarObject.transform))
                return s_HelpBarObject;

            s_HelpBarObject = null;

            s_HelpBarObject = FindHelpBarByComponent(uiRoot);
            if (s_HelpBarObject != null)
                return s_HelpBarObject;

            if (uiRoot == null)
            {
                s_HelpBarObject = FindHelpBarByName();
                if (s_HelpBarObject != null)
                    return s_HelpBarObject;

                s_HelpBarObject = FindSceneTransform(IsHelpBarTransform)?.gameObject;
                if (s_HelpBarObject != null)
                    return s_HelpBarObject;

                return null;
            }

            Transform directHelpBar = uiRoot.transform.Find(HelpBarName);
            if (directHelpBar != null)
            {
                s_HelpBarObject = directHelpBar.gameObject;
                return s_HelpBarObject;
            }

            Transform[] children = uiRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                Transform child = children[index];
                if (child != null && IsHelpBarTransform(child))
                {
                    s_HelpBarObject = child.gameObject;
                    return s_HelpBarObject;
                }
            }

            s_HelpBarObject = FindHelpBarByName();
            return s_HelpBarObject;
        }

        private static GameObject FindHelpBarByComponent(GameObject uiRoot)
        {
            if (uiRoot != null)
            {
                HelpBarUI scopedHelpBar = uiRoot.GetComponentInChildren<HelpBarUI>(true);
                if (scopedHelpBar != null && IsLoadedSceneObject(scopedHelpBar.transform))
                    return scopedHelpBar.gameObject;
            }

            HelpBarUI[] helpBars = Resources.FindObjectsOfTypeAll<HelpBarUI>();
            for (int index = 0; index < helpBars.Length; index++)
            {
                HelpBarUI helpBar = helpBars[index];
                if (helpBar != null && IsLoadedSceneObject(helpBar.transform))
                    return helpBar.gameObject;
            }

            return null;
        }

        private static GameObject FindHelpBarByName()
        {
            Transform helpBarTransform = FindSceneTransform(candidate =>
            {
                if (!IsHelpBarTransform(candidate))
                    return false;

                return FindChildByName(candidate, ControlsName) != null;
            });

            return helpBarTransform != null ? helpBarTransform.gameObject : null;
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private static void SetHelpBarActive(GameObject helpBarObject, bool active)
        {
            if (helpBarObject != null && helpBarObject.activeSelf != active)
                helpBarObject.SetActive(active);
        }

        private static bool IsHelpBarTransform(Transform transform)
        {
            return transform != null
                && (string.Equals(transform.name, HelpBarName, StringComparison.Ordinal)
                    || string.Equals(transform.name, LegacyHelpBarName, StringComparison.Ordinal));
        }

        private static Transform FindSceneTransform(Predicate<Transform> predicate)
        {
            if (predicate == null)
                return null;

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (!IsLoadedSceneObject(candidate) || !predicate(candidate))
                    continue;

                return candidate;
            }

            return null;
        }

        private static bool IsLoadedSceneObject(Transform transform)
        {
            if (transform == null)
                return false;

            UnityEngine.SceneManagement.Scene scene = transform.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static bool IsPreferredRuntimeUiRoot(Transform transform)
        {
            return transform != null
                && string.Equals(transform.name, LegacyUiRootName, StringComparison.Ordinal)
                && transform.parent != null
                && string.Equals(transform.parent.name, "HoyoToon", StringComparison.Ordinal);
        }
    }
}

