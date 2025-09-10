// Copyright (c) Jason Ma

using System;
using UnityEngine;
using UnityEditor;
using LWGUI.Runtime.LwguiGradient;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace LWGUI.LwguiGradientEditor
{
    internal class PresetLibraryLwguiGradientEditor : PresetLibraryEditor<LwguiGradientPresetLibrary>
    {
        public PresetLibraryLwguiGradientEditor(ScriptableObjectSaveLoadHelper<LwguiGradientPresetLibrary> helper,
            PresetLibraryEditorState state,
            Action<int, object> itemClickedCallback
        ) : base(helper, state, itemClickedCallback)
        {}

        public ColorSpace colorSpace { get; set; }
        public LwguiGradient.ChannelMask viewChannelMask { get; set; }

        protected override void DrawPreset(PresetLibrary lib, Rect rect, object presetObject)
        {
            ((LwguiGradientPresetLibrary)lib).Draw(rect, presetObject as LwguiGradient, colorSpace, viewChannelMask);
        }
    }

    public class LwguiGradientWindow : EditorWindow
    {
        #region Fields
        
        private static LwguiGradientWindow _lwguiGradientWindow;
        public const string presetsEditorPrefID = "LwguiGradient";
        public delegate void ChangeGradientCallback(LwguiGradient gradient);

        private LwguiGradientEditor _lwguiGradientEditor;
        private PresetLibraryLwguiGradientEditor _lwguiGradientLibraryEditor;
        [SerializeField] private PresetLibraryEditorState _LwguiGradientLibraryEditorState;
        
        [NonSerialized] public LwguiGradient lwguiGradient;
        [NonSerialized] public ColorSpace colorSpace;
        [NonSerialized] public LwguiGradient.ChannelMask viewChannelMask;
        [NonSerialized] public LwguiGradient.GradientTimeRange gradientTimeRange;
        
        private GUIView _viewToUpdate;
        private ChangeGradientCallback _onChange;

        #endregion

        #region GUI Layout

        private static readonly Vector2 _minWindowSize = new (750, 500);
        private static readonly float _presetLibraryHeight = 100;
        private Rect _gradientEditorRect => new Rect(0, 0, position.width, position.height - _presetLibraryHeight);
        private Rect _presetLibraryRect => new Rect(0, position.height - _presetLibraryHeight, position.width, _presetLibraryHeight);

        #endregion
        
        public static LwguiGradientWindow instance
        {
            get
            {
                // if (!_lwguiGradientWindow)
                //     Debug.LogError("Lwgui Gradient Window not initalized, did you call Show first?");
                return _lwguiGradientWindow;
            }
        }
        
        public string currentPresetLibrary
        {
            get
            {
                Init(false);
                return _lwguiGradientLibraryEditor.currentLibraryWithoutExtension;
            }
            set
            {
                Init(false);
                _lwguiGradientLibraryEditor.currentLibraryWithoutExtension = value;
            }
        }
        
        public static bool visible => _lwguiGradientWindow != null;

        public void Init(bool force = true, bool forceRecreate = false)
        {
            if (_lwguiGradientEditor == null || force || forceRecreate)
            {
                if (_lwguiGradientEditor == null || forceRecreate)
                {
                    _lwguiGradientEditor = new LwguiGradientEditor();
                }
                _lwguiGradientEditor.Init(_gradientEditorRect, lwguiGradient, colorSpace, viewChannelMask, gradientTimeRange, _onChange);
            }
            
            if (_LwguiGradientLibraryEditorState == null || forceRecreate)
            {
                _LwguiGradientLibraryEditorState = new PresetLibraryEditorState(presetsEditorPrefID);
                _LwguiGradientLibraryEditorState.TransferEditorPrefsState(true);
            }
            
            if (_lwguiGradientLibraryEditor == null || force || forceRecreate)
            {
                if (_lwguiGradientLibraryEditor == null || forceRecreate)
                {
                    var saveLoadHelper = new ScriptableObjectSaveLoadHelper<LwguiGradientPresetLibrary>("lwguigradients", SaveType.Text);
                    _lwguiGradientLibraryEditor = new PresetLibraryLwguiGradientEditor(saveLoadHelper, _LwguiGradientLibraryEditorState, PresetClickedCallback);
                    UpdatePresetLibraryViewSettings();
                }
                _lwguiGradientLibraryEditor.showHeader = true;
                _lwguiGradientLibraryEditor.minMaxPreviewHeight = new Vector2(14f, 14f);
            }
        }

        void UpdatePresetLibraryViewSettings()
        {
            _lwguiGradientLibraryEditor.colorSpace = _lwguiGradientEditor.colorSpace;
            _lwguiGradientLibraryEditor.viewChannelMask = _lwguiGradientEditor.viewChannelMask;
        }

        /// Used to modify the LwguiGradient value externally, such as: Undo/Redo/Select Preset
        public static void UpdateCurrentGradient(LwguiGradient newGradient, bool doDeepCopy = false)
        {
            if (_lwguiGradientWindow == null)
                return;

            if (doDeepCopy)
            {
                _lwguiGradientWindow.lwguiGradient.DeepCopyFrom(newGradient);
            }
            else
            {
                _lwguiGradientWindow.lwguiGradient = newGradient;
            }
            // Debug.Log("Update");
            _lwguiGradientWindow.Init();
            _lwguiGradientWindow.Repaint();
            GUI.changed = true;
            LwguiGradientHelper.ClearRampPreviewCaches();
        }
        
        private static LwguiGradientWindow GetWindow(bool focus = true) => (LwguiGradientWindow)GetWindow(typeof(LwguiGradientWindow), true, "LWGUI Gradient Editor", focus);

        internal static void Show(LwguiGradient gradient, ColorSpace colorSpace = ColorSpace.Gamma, LwguiGradient.ChannelMask viewChannelMask = LwguiGradient.ChannelMask.All, LwguiGradient.GradientTimeRange timeRange = LwguiGradient.GradientTimeRange.One, GUIView viewToUpdate = null, ChangeGradientCallback onChange = null)
        {
            if (_lwguiGradientWindow == null)
            {
                _lwguiGradientWindow = GetWindow();
                _lwguiGradientWindow.minSize = _minWindowSize;
                _lwguiGradientWindow.RegisterEvents();
            }
            else
            {
                _lwguiGradientWindow = GetWindow();
            }
            
            _lwguiGradientWindow.lwguiGradient = gradient;
            _lwguiGradientWindow.colorSpace = colorSpace;
            _lwguiGradientWindow.viewChannelMask = viewChannelMask;
            _lwguiGradientWindow.gradientTimeRange = timeRange;
            _lwguiGradientWindow._viewToUpdate = viewToUpdate;
            _lwguiGradientWindow._onChange = onChange;

            _lwguiGradientWindow.Init();
            _lwguiGradientWindow.Show();
            // window.ShowAuxWindow();

            LwguiGradientHelper.ClearRampPreviewCaches();
        }

        public static void CloseWindow()
        {
            if (_lwguiGradientWindow == null)
                return;

            _lwguiGradientWindow.UnregisterEvents();
            _lwguiGradientWindow.Close();
            // GUIUtility.ExitGUI();
        }
        
        public static void RepaintWindow()
        {
            if (_lwguiGradientWindow == null)
                return;
            _lwguiGradientWindow.Repaint();
        }

        public static void RegisterSerializedObjectUndo(Object targetObject)
        {
            if (targetObject)
            {
                Undo.RegisterCompleteObjectUndo(targetObject, "Lwgui Gradient Editor");
                EditorUtility.SetDirty(targetObject);
            }
        }

        public static void RegisterRampMapUndo(Object texture, Object assetImporter)
        {
            Undo.RecordObjects(new Object[]{ texture, assetImporter }, "Set Lwgui Gradient To Texture");
            EditorUtility.SetDirty(texture);
            EditorUtility.SetDirty(assetImporter);
        }

        private void OnGUI()
        {
            if (lwguiGradient == null) 
                return;
            
            Init(false);
            
            // Separator
            EditorGUI.DrawRect(new Rect(_presetLibraryRect.x, _presetLibraryRect.y - 1, _presetLibraryRect.width, 1), new Color(0, 0, 0, 0.3f));
            EditorGUI.DrawRect(new Rect(_presetLibraryRect.x, _presetLibraryRect.y, _presetLibraryRect.width, 1), new Color(1, 1, 1, 0.1f));

            
            EditorGUI.BeginChangeCheck();
            _lwguiGradientEditor.OnGUI(_gradientEditorRect);
            _lwguiGradientLibraryEditor.OnGUI(_presetLibraryRect, lwguiGradient);
            if (EditorGUI.EndChangeCheck())
            {
                LwguiGradientHelper.ClearRampPreviewCaches();
                UpdatePresetLibraryViewSettings();
                SendChangedEvent(true);
            }
        }

        public const string LwguiGradientChangedCommand = "LwguiGradientChanged";

        void SendChangedEvent(bool exitGUI)
        {
            _onChange?.Invoke(lwguiGradient);
            if (_viewToUpdate)
            {
                Event e = EditorGUIUtility.CommandEvent(LwguiGradientChangedCommand);
                Repaint();
                _viewToUpdate.SendEvent(e);
                if (exitGUI)
                    GUIUtility.ExitGUI();
            }
        }

        private void OnEnable()
        {
            Application.logMessageReceived += LwguiGradientEditor.CheckAddGradientKeyFailureLog;
            hideFlags = HideFlags.DontSave;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= LwguiGradientEditor.CheckAddGradientKeyFailureLog;

            _LwguiGradientLibraryEditorState?.TransferEditorPrefsState(false);

            UnregisterEvents();
            Clear();
        }

        private void OnDestroy()
        {
            UnregisterEvents();
            _lwguiGradientLibraryEditor?.UnloadUsedLibraries();

            Clear();
        }

        private void Clear()
        {
            _lwguiGradientEditor = null;
            _lwguiGradientWindow = null;
            _lwguiGradientLibraryEditor = null;
        }

        private void RegisterEvents()
        {
#if UNITY_2022_2_OR_NEWER
            Undo.undoRedoEvent += OnUndoPerformed;
#endif
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void UnregisterEvents()
        {
#if UNITY_2022_2_OR_NEWER
            Undo.undoRedoEvent -= OnUndoPerformed;
#endif
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        #region Call Backs

#if UNITY_2022_2_OR_NEWER
        private void OnUndoPerformed(in UndoRedoInfo info)
        {
            // Debug.Log("Init");
            _lwguiGradientWindow.Init();
            _lwguiGradientWindow.Repaint();
        }
#endif

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Close();
        }
        
        void PresetClickedCallback(int clickCount, object presetObject)
        {
            LwguiGradient gradient = presetObject as LwguiGradient;
            if (gradient == null)
                Debug.LogError("Incorrect object passed " + presetObject);

            UpdateCurrentGradient(gradient, true);
            // UnityEditorInternal.GradientPreviewCache.ClearCache();
            // LwguiGradientHelper.ClearRampPreviewCaches();
        }

        #endregion
    }
}