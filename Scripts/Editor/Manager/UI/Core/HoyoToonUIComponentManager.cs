using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.UI.Core
{
    /// <summary>
    /// Manager for HoyoToon UI components, handling component lifecycle and communication
    /// </summary>
    public class HoyoToonUIComponentManager
    {
        #region Singleton

        private static HoyoToonUIComponentManager _instance;
        public static HoyoToonUIComponentManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new HoyoToonUIComponentManager();
                return _instance;
            }
        }

        #endregion

        #region Fields

        private Dictionary<string, HoyoToonUIComponent> registeredComponents = new Dictionary<string, HoyoToonUIComponent>();
        private Dictionary<string, HoyoToonUITabComponent> registeredTabs = new Dictionary<string, HoyoToonUITabComponent>();
        private string activeTabId = null;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when a component is registered
        /// </summary>
        public event Action<string, HoyoToonUIComponent> ComponentRegistered;

        /// <summary>
        /// Event fired when a component is unregistered
        /// </summary>
        public event Action<string> ComponentUnregistered;

        /// <summary>
        /// Event fired when the active tab changes
        /// </summary>
        public event Action<string, string> ActiveTabChanged; // oldTabId, newTabId

        /// <summary>
        /// Event fired when a component triggers an event
        /// </summary>
        public event Action<ComponentEventArgs> ComponentEvent;

        #endregion

        #region Component Management

        /// <summary>
        /// Register a UI component
        /// </summary>
        public void RegisterComponent(HoyoToonUIComponent component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            var id = component.ComponentId;
            if (registeredComponents.ContainsKey(id))
            {
                Debug.LogWarning($"Component with ID '{id}' is already registered. Replacing existing component.");
                UnregisterComponent(id);
            }

            registeredComponents[id] = component;

            // If it's a tab component, also register it in the tabs collection
            if (component is HoyoToonUITabComponent tabComponent)
            {
                registeredTabs[id] = tabComponent;
            }

            ComponentRegistered?.Invoke(id, component);
        }

        /// <summary>
        /// Unregister a UI component
        /// </summary>
        public void UnregisterComponent(string componentId)
        {
            if (registeredComponents.TryGetValue(componentId, out var component))
            {
                component.Cleanup();
                registeredComponents.Remove(componentId);
                registeredTabs.Remove(componentId);
                ComponentUnregistered?.Invoke(componentId);

                // If this was the active tab, clear it
                if (activeTabId == componentId)
                {
                    activeTabId = null;
                }
            }
        }

        /// <summary>
        /// Get a registered component by ID
        /// </summary>
        public T GetComponent<T>(string componentId) where T : HoyoToonUIComponent
        {
            if (registeredComponents.TryGetValue(componentId, out var component) && component is T)
            {
                return (T)component;
            }
            return null;
        }

        /// <summary>
        /// Get all registered components of a specific type
        /// </summary>
        public List<T> GetComponents<T>() where T : HoyoToonUIComponent
        {
            return registeredComponents.Values.OfType<T>().ToList();
        }

        /// <summary>
        /// Check if a component is registered
        /// </summary>
        public bool IsComponentRegistered(string componentId)
        {
            return registeredComponents.ContainsKey(componentId);
        }

        /// <summary>
        /// Initialize all registered components
        /// </summary>
        public void InitializeAllComponents()
        {
            foreach (var component in registeredComponents.Values)
            {
                if (!component.IsInitialized)
                {
                    component.Initialize();
                }
            }
        }

        /// <summary>
        /// Update all registered components with new data
        /// </summary>
        public void UpdateAllComponents(Dictionary<string, object> globalData = null)
        {
            foreach (var component in registeredComponents.Values)
            {
                component.UpdateComponent(globalData);
            }
        }

        /// <summary>
        /// Cleanup all registered components
        /// </summary>
        public void CleanupAllComponents()
        {
            foreach (var component in registeredComponents.Values.ToList())
            {
                component.Cleanup();
            }
            registeredComponents.Clear();
            registeredTabs.Clear();
            activeTabId = null;
        }

        #endregion

        #region Tab Management

        /// <summary>
        /// Get all registered tab components
        /// </summary>
        public List<HoyoToonUITabComponent> GetAllTabs()
        {
            return registeredTabs.Values.ToList();
        }

        /// <summary>
        /// Get tab component by ID
        /// </summary>
        public HoyoToonUITabComponent GetTab(string tabId)
        {
            registeredTabs.TryGetValue(tabId, out var tab);
            return tab;
        }

        /// <summary>
        /// Set the active tab
        /// </summary>
        public void SetActiveTab(string tabId)
        {
            if (activeTabId == tabId)
                return; // Already active

            var oldTabId = activeTabId;

            // Deactivate current tab
            if (!string.IsNullOrEmpty(activeTabId) && registeredTabs.TryGetValue(activeTabId, out var currentTab))
            {
                currentTab.OnTabDeactivated();
            }

            // Activate new tab
            if (!string.IsNullOrEmpty(tabId) && registeredTabs.TryGetValue(tabId, out var newTab))
            {
                if (!newTab.IsInitialized)
                {
                    newTab.Initialize();
                }
                newTab.OnTabActivated();
                activeTabId = tabId;
            }
            else
            {
                activeTabId = null;
            }

            ActiveTabChanged?.Invoke(oldTabId, activeTabId);
        }

        /// <summary>
        /// Get the currently active tab
        /// </summary>
        public HoyoToonUITabComponent GetActiveTab()
        {
            if (!string.IsNullOrEmpty(activeTabId) && registeredTabs.TryGetValue(activeTabId, out var tab))
            {
                return tab;
            }
            return null;
        }

        /// <summary>
        /// Get quick actions from the active tab
        /// </summary>
        public List<QuickAction> GetActiveTabQuickActions()
        {
            var activeTab = GetActiveTab();
            return activeTab?.GetQuickActions() ?? new List<QuickAction>();
        }

        #endregion

        #region Event System

        /// <summary>
        /// Fire a component event
        /// </summary>
        public void FireComponentEvent(string componentId, string eventType, Dictionary<string, object> eventData = null)
        {
            var args = new ComponentEventArgs(componentId, eventType, eventData);
            ComponentEvent?.Invoke(args);
        }

        /// <summary>
        /// Subscribe to component events of a specific type
        /// </summary>
        public void SubscribeToComponentEvents(string eventType, Action<ComponentEventArgs> handler)
        {
            ComponentEvent += (args) =>
            {
                if (args.EventType == eventType)
                {
                    handler(args);
                }
            };
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get component statistics for debugging
        /// </summary>
        public ComponentManagerStats GetStats()
        {
            return new ComponentManagerStats
            {
                TotalComponents = registeredComponents.Count,
                TotalTabs = registeredTabs.Count,
                InitializedComponents = registeredComponents.Values.Count(c => c.IsInitialized),
                ActiveTabId = activeTabId
            };
        }

        /// <summary>
        /// Validate component integrity
        /// </summary>
        public List<string> ValidateComponents()
        {
            var issues = new List<string>();

            foreach (var kvp in registeredComponents)
            {
                var id = kvp.Key;
                var component = kvp.Value;

                if (component == null)
                {
                    issues.Add($"Component '{id}' is null");
                    continue;
                }

                if (component.ComponentId != id)
                {
                    issues.Add($"Component ID mismatch: registered as '{id}' but reports '{component.ComponentId}'");
                }

                if (component.RootElement == null && component.IsInitialized)
                {
                    issues.Add($"Component '{id}' is marked as initialized but has no root element");
                }
            }

            return issues;
        }

        #endregion
    }

    /// <summary>
    /// Statistics about the component manager state
    /// </summary>
    public class ComponentManagerStats
    {
        public int TotalComponents { get; set; }
        public int TotalTabs { get; set; }
        public int InitializedComponents { get; set; }
        public string ActiveTabId { get; set; }

        public override string ToString()
        {
            return $"Components: {TotalComponents} ({InitializedComponents} initialized), Tabs: {TotalTabs}, Active Tab: {ActiveTabId ?? "None"}";
        }
    }
}