using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.UI.Core
{
    /// <summary>
    /// Base class for all HoyoToon UI components providing common functionality
    /// and standardized component lifecycle management
    /// </summary>
    public abstract class HoyoToonUIComponent
    {
        #region Protected Fields

        protected VisualElement rootElement;
        protected bool isInitialized = false;
        protected Dictionary<string, object> componentData = new Dictionary<string, object>();

        #endregion

        #region Public Properties

        /// <summary>
        /// The root visual element of this component
        /// </summary>
        public VisualElement RootElement => rootElement;

        /// <summary>
        /// Whether this component has been initialized
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Unique identifier for this component
        /// </summary>
        public abstract string ComponentId { get; }

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Initialize the component and create its UI structure
        /// </summary>
        public virtual void Initialize()
        {
            if (isInitialized)
                return;

            CreateRootElement();
            CreateComponentUI();
            RegisterEventHandlers();
            isInitialized = true;
            OnInitialized();
        }

        /// <summary>
        /// Update the component with new data
        /// </summary>
        public virtual void UpdateComponent(Dictionary<string, object> data = null)
        {
            if (!isInitialized)
                Initialize();

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    componentData[kvp.Key] = kvp.Value;
                }
            }

            RefreshComponentUI();
        }

        /// <summary>
        /// Cleanup the component and its resources
        /// </summary>
        public virtual void Cleanup()
        {
            UnregisterEventHandlers();
            OnCleanup();
            isInitialized = false;
            componentData.Clear();
            rootElement = null;
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Create the main UI structure for this component
        /// </summary>
        protected abstract void CreateComponentUI();

        /// <summary>
        /// Refresh the component UI when data changes
        /// </summary>
        protected abstract void RefreshComponentUI();

        #endregion

        #region Virtual Methods

        /// <summary>
        /// Create the root element for this component
        /// </summary>
        protected virtual void CreateRootElement()
        {
            rootElement = new VisualElement();
            rootElement.name = $"HoyoToonUI_{ComponentId}";
        }

        /// <summary>
        /// Register event handlers for this component
        /// </summary>
        protected virtual void RegisterEventHandlers()
        {
            // Override in derived classes to register specific event handlers
        }

        /// <summary>
        /// Unregister event handlers for this component
        /// </summary>
        protected virtual void UnregisterEventHandlers()
        {
            // Override in derived classes to clean up event handlers
        }

        /// <summary>
        /// Called after the component is initialized
        /// </summary>
        protected virtual void OnInitialized()
        {
            // Override in derived classes for post-initialization logic
        }

        /// <summary>
        /// Called during cleanup
        /// </summary>
        protected virtual void OnCleanup()
        {
            // Override in derived classes for cleanup logic
        }

        #endregion

        #region Data Management

        /// <summary>
        /// Get component data by key
        /// </summary>
        protected T GetData<T>(string key, T defaultValue = default(T))
        {
            if (componentData.TryGetValue(key, out var value) && value is T)
            {
                return (T)value;
            }
            return defaultValue;
        }

        /// <summary>
        /// Set component data by key
        /// </summary>
        protected void SetData<T>(string key, T value)
        {
            componentData[key] = value;
        }

        /// <summary>
        /// Check if component has data for the specified key
        /// </summary>
        protected bool HasData(string key)
        {
            return componentData.ContainsKey(key);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Find a child element by name
        /// </summary>
        protected T FindChild<T>(string name) where T : VisualElement
        {
            return rootElement?.Q<T>(name);
        }

        /// <summary>
        /// Find all child elements of type T
        /// </summary>
        protected List<T> FindChildren<T>() where T : VisualElement
        {
            var results = new List<T>();
            if (rootElement != null)
            {
                rootElement.Query<T>().ForEach(results.Add);
            }
            return results;
        }

        /// <summary>
        /// Show this component
        /// </summary>
        public virtual void Show()
        {
            if (rootElement != null)
            {
                rootElement.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>
        /// Hide this component
        /// </summary>
        public virtual void Hide()
        {
            if (rootElement != null)
            {
                rootElement.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Toggle component visibility
        /// </summary>
        public virtual void ToggleVisibility()
        {
            if (rootElement?.style.display == DisplayStyle.None)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        #endregion
    }

    /// <summary>
    /// Specialized base class for tab components
    /// </summary>
    public abstract class HoyoToonUITabComponent : HoyoToonUIComponent
    {
        #region Properties

        /// <summary>
        /// Display name for this tab
        /// </summary>
        public abstract string TabName { get; }

        /// <summary>
        /// Whether this tab is currently active
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Whether this tab requires a model to function
        /// </summary>
        public virtual bool RequiresModel => true;

        #endregion

        #region Tab Lifecycle

        /// <summary>
        /// Called when this tab becomes active
        /// </summary>
        public virtual void OnTabActivated()
        {
            IsActive = true;
            Show();
            OnTabActivatedInternal();
        }

        /// <summary>
        /// Called when this tab becomes inactive
        /// </summary>
        public virtual void OnTabDeactivated()
        {
            IsActive = false;
            OnTabDeactivatedInternal();
        }

        /// <summary>
        /// Internal method called when tab is activated
        /// </summary>
        protected virtual void OnTabActivatedInternal()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Internal method called when tab is deactivated
        /// </summary>
        protected virtual void OnTabDeactivatedInternal()
        {
            // Override in derived classes
        }

        #endregion

        #region Quick Actions

        /// <summary>
        /// Get quick actions available for this tab
        /// </summary>
        public virtual List<QuickAction> GetQuickActions()
        {
            return new List<QuickAction>();
        }

        #endregion
    }

    /// <summary>
    /// Quick action data structure
    /// </summary>
    public class QuickAction
    {
        public string Label { get; set; }
        public Action Action { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string Tooltip { get; set; }

        public QuickAction(string label, Action action, bool isEnabled = true, string tooltip = null)
        {
            Label = label;
            Action = action;
            IsEnabled = isEnabled;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Event data for component events
    /// </summary>
    public class ComponentEventArgs : EventArgs
    {
        public string ComponentId { get; }
        public string EventType { get; }
        public Dictionary<string, object> EventData { get; }

        public ComponentEventArgs(string componentId, string eventType, Dictionary<string, object> eventData = null)
        {
            ComponentId = componentId;
            EventType = eventType;
            EventData = eventData ?? new Dictionary<string, object>();
        }
    }
}