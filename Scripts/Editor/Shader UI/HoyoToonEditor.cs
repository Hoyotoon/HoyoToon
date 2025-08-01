﻿// Material/Shader Inspector for Unity 2017/2018
// CopyRight (C) 2024 Thryrallo + HoyoToon
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using HoyoToon.HoyoToonEditor;
using System.Reflection;
using static HoyoToon.UnityHelper;
using HoyoToon.HoyoToonEditor.ShaderTranslations;
using JetBrains.Annotations;

namespace HoyoToon
{
    public class ShaderEditor : ShaderGUI
    {
        public const string EXTRA_OPTIONS_PREFIX = "--";
        public const float MATERIAL_NOT_RESET = 69.12f;

        public const string PROPERTY_NAME_MASTER_LABEL = "shader_master_label";
        public const string PROPERTY_NAME_LABEL_FILE = "shader_properties_label_file";
        public const string PROPERTY_NAME_LOCALE = "shader_locale";
        public const string PROPERTY_NAME_ON_SWAP_TO_ACTIONS = "shader_on_swap_to";
        public const string PROPERTY_NAME_SHADER_VERSION = "shader_version";
        public const string PROPERTY_NAME_EDITOR_DETECT = "shader_is_using_HoyoToon_editor";
        public const string PROPERTY_NAME_IN_SHADER_PRESETS = "_Mode";

        //Static
        private static string s_edtiorDirectoryPath;

        public static InputEvent Input = new InputEvent();
        public static ShaderEditor Active;

        // Stores the different shader properties
        public ShaderGroup MainGroup;
        private RenderQueueProperty _renderQueueProperty;
        private VRCFallbackProperty _vRCFallbackProperty;

        // UI Instance Variables

        private string _enteredSearchTerm = "";
        private string _appliedSearchTerm = "";

        // shader specified values
        private ShaderHeaderProperty _shaderHeader = null;
        private List<FooterButton> _footers;

        // sates
        private bool _isFirstOnGUICall = true;
        private bool _wasUsed = false;
        private bool _doReloadNextDraw = false;
        private bool _didSwapToShader = false;

        //EditorData
        public MaterialEditor Editor;
        public MaterialProperty[] Properties;
        public Material[] Materials;
        public Shader Shader;
        public Shader LastShader;
        public ShaderPart CurrentProperty;
        public Dictionary<string, ShaderProperty> PropertyDictionary;
        public List<ShaderPart> ShaderParts;
        public List<ShaderProperty> TextureArrayProperties;
        public bool IsFirstCall;
        public bool DoUseShaderOptimizer;
        public bool IsLockedMaterial;
        public bool IsInAnimationMode;
        public Renderer ActiveRenderer;
        public string RenamedPropertySuffix;
        public bool HasCustomRenameSuffix;
        public Localization Locale;
        public ShaderTranslator SuggestedTranslationDefinition;
        private string _duplicatePropertyNamesString = null;

        //Shader Versioning
        private Version _shaderVersionLocal;
        private Version _shaderVersionRemote;
        private bool _hasShaderUpdateUrl = false;
        private bool _isShaderUpToDate = true;
        private string _shaderUpdateUrl = null;

        //other
        string ShaderOptimizerPropertyName = null;
        ShaderProperty ShaderOptimizerProperty { get; set; }
        ShaderProperty LocaleProperty { get; set; }
        ShaderProperty InShaderPresetsProperty { get; set; }

        [PublicAPI]
        public float ShaderRenderingPreset { get => InShaderPresetsProperty.FloatValue; set => InShaderPresetsProperty.FloatValue = value; }

        private DefineableAction[] _onSwapToActions = null;

        public bool IsDrawing { get; private set; } = false;
        public bool IsPresetEditor { get; private set; } = false;
        public bool IsSectionedPresetEditor
        {
            get
            {
                return IsPresetEditor && Presets.IsMaterialSectionedPreset(Materials[0]);
            }
        }

        public bool HasMixedCustomPropertySuffix
        {
            get
            {
                if (Materials.Length == 1) return false;
                string suffix = ShaderOptimizer.GetRenamedPropertySuffix(Materials[0]);
                for (int i = 1; i < Materials.Length; i++)
                {
                    if (suffix != ShaderOptimizer.GetRenamedPropertySuffix(Materials[i])) return true;
                }
                return false;
            }
        }

        public bool DidSwapToNewShader
        {
            get
            {
                return _didSwapToShader;
            }
        }

        //-------------Init functions--------------------

        private Dictionary<string, string> LoadDisplayNamesFromFile()
        {
            //load display names from file if it exists
            MaterialProperty label_file_property = GetMaterialProperty(PROPERTY_NAME_LABEL_FILE);
            Dictionary<string, string> labels = new Dictionary<string, string>();
            if (label_file_property != null)
            {
                string[] guids = AssetDatabase.FindAssets(label_file_property.displayName);
                if (guids.Length == 0)
                {
                    HoyoToonLogs.WarningDebug("Label File could not be found");
                    return labels;
                }
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                string[] data = Regex.Split(HoyoToon.FileHelper.ReadFileIntoString(path), @"\r?\n");
                foreach (string d in data)
                {
                    string[] set = Regex.Split(d, ":=");
                    if (set.Length > 1) labels[set[0]] = set[1];
                }
            }
            return labels;
        }

        public static string SplitOptionsFromDisplayName(ref string displayName)
        {
            if (displayName.Contains(EXTRA_OPTIONS_PREFIX))
            {
                string[] parts = displayName.Split(new string[] { EXTRA_OPTIONS_PREFIX }, 2, System.StringSplitOptions.None);
                displayName = parts[0];
                return parts[1];
            }
            return null;
        }

        private enum HoyoToonPropertyType
        {
            none, property, master_label, footer, header, header_end, header_start, group_start, group_end, section_start, section_end, instancing, dsgi, lightmap_flags, locale, on_swap_to, space, shader_version, optimizer, in_shader_presets
        }

        private HoyoToonPropertyType GetPropertyType(MaterialProperty p)
        {
            string name = p.name;
            MaterialProperty.PropFlags flags = p.flags;

            if (flags == MaterialProperty.PropFlags.HideInInspector)
            {
                if (name == PROPERTY_NAME_MASTER_LABEL)
                    return HoyoToonPropertyType.master_label;
                if (name == PROPERTY_NAME_ON_SWAP_TO_ACTIONS)
                    return HoyoToonPropertyType.on_swap_to;
                if (name == PROPERTY_NAME_SHADER_VERSION)
                    return HoyoToonPropertyType.shader_version;

                if (name.StartsWith("start", StringComparison.Ordinal))
                    return HoyoToonPropertyType.header_start;
                if (name.StartsWith("end", StringComparison.Ordinal))
                    return HoyoToonPropertyType.header_end;
                if (name.StartsWith("m_", StringComparison.Ordinal))
                    return HoyoToonPropertyType.header;
                if (name.StartsWith("g_start", StringComparison.Ordinal))
                    return HoyoToonPropertyType.group_start;
                if (name.StartsWith("g_end", StringComparison.Ordinal))
                    return HoyoToonPropertyType.group_end;
                if (name.StartsWith("s_start", StringComparison.Ordinal))
                    return HoyoToonPropertyType.section_start;
                if (name.StartsWith("s_end", StringComparison.Ordinal))
                    return HoyoToonPropertyType.section_end;
                if (name.StartsWith("footer_", StringComparison.Ordinal))
                    return HoyoToonPropertyType.footer;
                if (name == "Instancing")
                    return HoyoToonPropertyType.instancing;
                if (name == "DSGI")
                    return HoyoToonPropertyType.dsgi;
                if (name == "LightmapFlags")
                    return HoyoToonPropertyType.lightmap_flags;
                if (name == PROPERTY_NAME_LOCALE)
                    return HoyoToonPropertyType.locale;
                if (name.StartsWith("space"))
                    return HoyoToonPropertyType.space;
            }
            else if (name == ShaderOptimizerPropertyName)
            {
                return HoyoToonPropertyType.optimizer;
            }
            else if (name == PROPERTY_NAME_IN_SHADER_PRESETS)
            {
                return HoyoToonPropertyType.in_shader_presets;
            }
            else if (flags.HasFlag(MaterialProperty.PropFlags.HideInInspector) == false)
            {
                return HoyoToonPropertyType.property;
            }
            return HoyoToonPropertyType.none;
        }

        private void LoadLocales()
        {
            MaterialProperty locales_property = GetMaterialProperty(PROPERTY_NAME_LOCALE);
            Locale = null;
            if (locales_property != null)
            {
                string guid = locales_property.displayName;
                Locale = Localization.Load(guid);
            }
            else
            {
                Locale = Localization.Create();
            }
        }

        public void FakePartialInitilizationForLocaleGathering(Shader s)
        {
            Material material = new Material(s);
            Materials = new Material[] { material };
            Editor = MaterialEditor.CreateEditor(new UnityEngine.Object[] { material }) as MaterialEditor;
            Properties = MaterialEditor.GetMaterialProperties(Materials);
            RenamedPropertySuffix = ShaderOptimizer.GetRenamedPropertySuffix(Materials[0]);
            HasCustomRenameSuffix = ShaderOptimizer.HasCustomRenameSuffix(Materials[0]);
            ShaderEditor.Active = this;
            CollectAllProperties();
            UnityEngine.Object.DestroyImmediate(Editor);
            UnityEngine.Object.DestroyImmediate(material);
        }

        //finds all properties and headers and stores them in correct order
        private void CollectAllProperties()
        {
            if (ShaderOptimizer.IsShaderUsingHoyoToonOptimizer(Shader))
            {
                ShaderOptimizerPropertyName = ShaderOptimizer.GetOptimizerPropertyName(Shader);
            }

            //load display names from file if it exists
            MaterialProperty[] props = Properties;
            Dictionary<string, string> labels = LoadDisplayNamesFromFile();
            LoadLocales();

            PropertyDictionary = new Dictionary<string, ShaderProperty>();
            ShaderParts = new List<ShaderPart>();
            MainGroup = new ShaderGroup(this); //init top object that all Shader Objects are childs of
            Stack<ShaderGroup> groupStack = new Stack<ShaderGroup>(); //header stack. used to keep track if editorData header to parent new objects to
            groupStack.Push(MainGroup); //add top object as top object to stack
            groupStack.Push(MainGroup); //add top object a second time, because it get's popped with first actual header item
            _footers = new List<FooterButton>(); //init footer list
            int offsetDepthCount = 0;
            DrawingData.IsCollectingProperties = true;

            HashSet<string> duplicatePropertiesSearch = new HashSet<string>(); // for debugging
            List<string> duplicateProperties = new List<string>(); // for debugging

            for (int i = 0; i < props.Length; i++)
            {
                string displayName = props[i].displayName;

                //Load from label file
                if (labels.ContainsKey(props[i].name)) displayName = labels[props[i].name];

                //extract json data from display name
                string optionsRaw = SplitOptionsFromDisplayName(ref displayName);

                displayName = Locale.Get(props[i], displayName);

                int offset = offsetDepthCount;

                // Duplicate property name check
                if (duplicatePropertiesSearch.Contains(props[i].name))
                    duplicateProperties.Add(props[i].name);
                else
                    duplicatePropertiesSearch.Add(props[i].name);

                DrawingData.ResetLastDrawerData();

                HoyoToonPropertyType type = GetPropertyType(props[i]);
                ShaderProperty NewProperty = null;
                ShaderPart newPart = null;
                // -- Group logic --
                // Change offset if needed
                if (type == HoyoToonPropertyType.header_start)
                    offset = ++offsetDepthCount;
                if (type == HoyoToonPropertyType.header_end)
                    offsetDepthCount--;
                // Create new group if needed
                switch (type)
                {
                    case HoyoToonPropertyType.group_start:
                        newPart = new ShaderGroup(this, props[i], Editor, displayName, offset, optionsRaw, i);
                        break;
                    case HoyoToonPropertyType.section_start:
                        newPart = new ShaderSection(this, props[i], Editor, displayName, offset, optionsRaw, i);
                        break;
                    case HoyoToonPropertyType.header:
                    case HoyoToonPropertyType.header_start:
                        newPart = new ShaderHeader(this, props[i], Editor, displayName, offset, optionsRaw, i);
                        break;
                }
                // pop if needed
                if (type == HoyoToonPropertyType.header || type == HoyoToonPropertyType.header_end || type == HoyoToonPropertyType.group_end || type == HoyoToonPropertyType.section_end)
                {
                    groupStack.Pop();
                }
                // push if needed
                if (newPart != null)
                {
                    groupStack.Peek().addPart(newPart);
                    groupStack.Push(newPart as ShaderGroup);
                }

                switch (type)
                {
                    case HoyoToonPropertyType.on_swap_to:
                        _onSwapToActions = PropertyOptions.Deserialize(optionsRaw).actions;
                        break;
                    case HoyoToonPropertyType.master_label:
                        _shaderHeader = new ShaderHeaderProperty(this, props[i], displayName, 0, optionsRaw, false, i);
                        break;
                    case HoyoToonPropertyType.footer:
                        _footers.Add(new FooterButton(Parser.Deserialize<ButtonData>(displayName)));
                        break;
                    case HoyoToonPropertyType.none:
                    case HoyoToonPropertyType.property:
                        if (props[i].type == MaterialProperty.PropType.Texture)
                            NewProperty = new ShaderTextureProperty(this, props[i], displayName, offset, optionsRaw, props[i].flags.HasFlag(MaterialProperty.PropFlags.NoScaleOffset) == false, false, i);
                        else
                            NewProperty = new ShaderProperty(this, props[i], displayName, offset, optionsRaw, false, i);
                        break;
                    case HoyoToonPropertyType.lightmap_flags:
                        NewProperty = new GIProperty(this, props[i], displayName, offset, optionsRaw, false, i);
                        break;
                    case HoyoToonPropertyType.dsgi:
                        NewProperty = new DSGIProperty(this, props[i], displayName, offset, optionsRaw, false, i);
                        break;
                    case HoyoToonPropertyType.instancing:
                        NewProperty = new InstancingProperty(this, props[i], displayName, offset, optionsRaw, false, i);
                        break;
                    case HoyoToonPropertyType.locale:
                        LocaleProperty = new LocaleProperty(this, props[i], displayName, offset, optionsRaw, false, i);
                        break;
                    case HoyoToonPropertyType.shader_version:
                        PropertyOptions options = PropertyOptions.Deserialize(optionsRaw);
                        _shaderVersionRemote = new Version(WebHelper.GetCachedString(options.remote_version_url));
                        _shaderVersionLocal = new Version(displayName);
                        _isShaderUpToDate = _shaderVersionLocal >= _shaderVersionRemote;
                        _shaderUpdateUrl = options.generic_string;
                        _hasShaderUpdateUrl = _shaderUpdateUrl != null;
                        break;
                    case HoyoToonPropertyType.optimizer:
                        ShaderOptimizerProperty = new ShaderProperty(this, props[i], displayName, offset, optionsRaw, false, i);
                        ShaderOptimizerProperty.SetIsExemptFromLockedDisabling(true);
                        break;
                    case HoyoToonPropertyType.in_shader_presets:
                        InShaderPresetsProperty = new ShaderProperty(this, props[i], displayName, offset, optionsRaw, false, i);
                        break;
                }
                if (NewProperty != null)
                {
                    newPart = NewProperty;
                    if (type != HoyoToonPropertyType.none)
                        groupStack.Peek().addPart(NewProperty);
                }
                if (newPart != null)
                {
                    if (!PropertyDictionary.ContainsKey(props[i].name))
                        PropertyDictionary.Add(props[i].name, NewProperty);
                    ShaderParts.Add(newPart);
                }
            }

            if (duplicateProperties.Count > 0 && Config.Singleton.enableDeveloperMode)
                _duplicatePropertyNamesString = string.Join("\n ", duplicateProperties.ToArray());

            DrawingData.IsCollectingProperties = false;
        }

        //-------------Draw Functions----------------

        public void InitlizeHoyoToonUI()
        {
            Config config = Config.Singleton;
            Active = this;

            //get material targets
            Materials = Editor.targets.Select(o => o as Material).ToArray();

            Shader = Materials[0].shader;

            RenamedPropertySuffix = ShaderOptimizer.GetRenamedPropertySuffix(Materials[0]);
            HasCustomRenameSuffix = ShaderOptimizer.HasCustomRenameSuffix(Materials[0]);

            IsPresetEditor = Materials.Length == 1 && Presets.ArePreset(Materials);

            //collect shader properties
            CollectAllProperties();

            _renderQueueProperty = new RenderQueueProperty(this);
            _vRCFallbackProperty = new VRCFallbackProperty(this);
            ShaderParts.Add(_renderQueueProperty);
            ShaderParts.Add(_vRCFallbackProperty);

            AddResetProperty();

            if (Config.Singleton.forceAsyncCompilationPreview)
            {
                ShaderUtil.allowAsyncCompilation = true;
            }

            _isFirstOnGUICall = false;
        }

        private Dictionary<string, MaterialProperty> materialPropertyDictionary;
        public MaterialProperty GetMaterialProperty(string name)
        {
            if (materialPropertyDictionary == null)
            {
                materialPropertyDictionary = new Dictionary<string, MaterialProperty>();
                foreach (MaterialProperty p in Properties)
                    if (materialPropertyDictionary.ContainsKey(p.name) == false) materialPropertyDictionary.Add(p.name, p);
            }
            if (materialPropertyDictionary.ContainsKey(name))
                return materialPropertyDictionary[name];
            return null;
        }

        private void AddResetProperty()
        {
            if (Materials[0].HasProperty(PROPERTY_NAME_EDITOR_DETECT) == false)
            {
                string path = AssetDatabase.GetAssetPath(Materials[0].shader);
                UnityHelper.AddShaderPropertyToSourceCode(path, "[HideInInspector] shader_is_using_HoyoToon_editor(\"\", Float)", "0");
            }
            Materials[0].SetFloat(PROPERTY_NAME_EDITOR_DETECT, 69);
        }



        public override void OnClosed(Material material)
        {
            base.OnClosed(material);
            _isFirstOnGUICall = true;
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            this.ShaderOptimizerProperty = null;
            this.LocaleProperty = null;
            this.InShaderPresetsProperty = null;
            //Unity sets the render queue to the shader defult when changing shader
            //This seems to be some deeper process that cant be disabled so i just set it again after the swap
            //Even material.shader = newShader resets the queue. (this is actually the only thing the base function does)
            int previousQueue = material.renderQueue;
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            material.renderQueue = previousQueue;
            SuggestedTranslationDefinition = ShaderTranslator.CheckForExistingTranslationFile(oldShader, newShader);
            FixKeywords(new Material[] { material });
            _doReloadNextDraw = true;
            _didSwapToShader = true;
            LastShader = oldShader;
            Shader = newShader;
        }

        void InitEditorData(MaterialEditor materialEditor)
        {
            Editor = materialEditor;
            TextureArrayProperties = new List<ShaderProperty>();
            IsFirstCall = true;
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
#if UNITY_2022_1_OR_NEWER
            EditorGUI.indentLevel -= 2;
#endif

            IsDrawing = true;
            //Init
            bool reloadUI = _isFirstOnGUICall || (_doReloadNextDraw && Event.current.type == EventType.Layout) || (materialEditor.target as Material).shader != Shader;
            if (reloadUI)
            {
                InitEditorData(materialEditor);
                Properties = props;
                InitlizeHoyoToonUI();
            }

            //Update Data
            Properties = props;
            Shader = Materials[0].shader;
            Input.Update(IsLockedMaterial);
            ActiveRenderer = Selection.activeTransform?.GetComponent<Renderer>();
            IsInAnimationMode = AnimationMode.InAnimationMode();

            Active = this;

            DoVariantWarning();
            //GUIManualReloadButton();
            GUIDevloperMode();
            GUIShaderVersioning();
            GUILogo();

            GUILayout.Space(5);
            GUITopBar();
            GUILayout.Space(5);
            GUISearchBar();
            GUILockinButton();
            //GUIPresetsBar();

            Presets.PresetEditorGUI(this);
            ShaderTranslator.SuggestedTranslationButtonGUI(this);

#if UNITY_2022_1_OR_NEWER
            EditorGUI.indentLevel += 2;
#endif

            //PROPERTIES
            using (new DetourMaterialPropertyVariantIcon())
            {
                foreach (ShaderPart part in MainGroup.parts)
                {
                    part.Draw();
                }
            }

            //Render Queue selection
            if (VRCInterface.IsVRCSDKInstalled()) _vRCFallbackProperty.Draw();
            if (Config.Singleton.showRenderQueue) _renderQueueProperty.Draw();

            BetterTooltips.DrawActive();

            GUIFooters();

            HandleEvents();

            IsDrawing = false;
            _didSwapToShader = false;
        }

        private void GUIManualReloadButton()
        {
            if (Config.Singleton.showManualReloadButton)
            {
                if (GUILayout.Button("Manual Reload"))
                {
                    this.Reload();
                }
            }
        }

        private void GUIDevloperMode()
        {
            if (Config.Singleton.enableDeveloperMode)
            {
                // Show duplicate property names
                if (_duplicatePropertyNamesString != null)
                {
                    EditorGUILayout.HelpBox("Duplicate Property Names:\n" + _duplicatePropertyNamesString, MessageType.Warning);
                }
            }
        }

        private void GUIShaderVersioning()
        {
            if (!_isShaderUpToDate)
            {
                Rect r = EditorGUILayout.GetControlRect(false, _hasShaderUpdateUrl ? 30 : 15);
                EditorGUI.LabelField(r, $"[New Shader Version available] {_shaderVersionLocal} -> {_shaderVersionRemote}" + (_hasShaderUpdateUrl ? "\n    Click here to download." : ""), Styles.redStyle);
                if (Input.HadMouseDownRepaint && _hasShaderUpdateUrl && GUILayoutUtility.GetLastRect().Contains(Input.mouse_position)) Application.OpenURL(_shaderUpdateUrl);
            }
        }

        private void GUILogo()
        {

        }

        // Layout constants for the top bar
        private struct TopBarLayout
        {
            public const float BANNER_HEIGHT = 150f;
            public const float LOGO_WIDTH = 348f;
            public const float LOGO_HEIGHT = 114f;
            public const float CHARACTER_MAX_WIDTH = 256f;
            public const float CHARACTER_MAX_HEIGHT = 180f;
            public const float MIN_LOGO_DISTANCE = 5f;
        }

        private void GUITopBar()
        {
            var layout = CreateTopBarLayout();
            
            DrawBannerBackground(layout);
            DrawCharacterImages(layout);
            DrawLogo(layout);
            DrawHeaderElements(layout);
        }

        private TopBarLayoutData CreateTopBarLayout()
        {
            var data = new TopBarLayoutData();
            
            // Get shader properties
            data.logoPathProperty = GetMaterialProperty("ShaderLogo");
            data.bgPathProperty = GetMaterialProperty("ShaderBG");
            data.characterLeftProperty = GetMaterialProperty("CharacterLeft");
            data.characterRightProperty = GetMaterialProperty("CharacterRight");
            
            // Calculate layout rectangles
            data.contentRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(TopBarLayout.BANNER_HEIGHT));
            data.originalY = data.contentRect.y;
            
            // Extended background rect
            data.bgRect = new Rect(0, 0, EditorGUIUtility.currentViewWidth, data.contentRect.height + data.originalY);
            
            // Logo rect centered in original content area
            float logoY = data.originalY + (TopBarLayout.BANNER_HEIGHT - TopBarLayout.LOGO_HEIGHT) / 2;
            data.logoRect = new Rect((data.bgRect.width - TopBarLayout.LOGO_WIDTH) / 2, logoY, TopBarLayout.LOGO_WIDTH, TopBarLayout.LOGO_HEIGHT);
            
            return data;
        }

        private void DrawBannerBackground(TopBarLayoutData layout)
        {
            if (layout.bgPathProperty?.displayName != null)
            {
                Texture2D bg = Resources.Load<Texture2D>(layout.bgPathProperty.displayName);
                if (bg != null)
                {
                    GUI.DrawTexture(layout.bgRect, bg, ScaleMode.StretchToFill);
                }
            }
        }

        private void DrawCharacterImages(TopBarLayoutData layout)
        {
            DrawCharacterImage(layout.characterLeftProperty, layout, true);
            DrawCharacterImage(layout.characterRightProperty, layout, false);
        }

        private void DrawLogo(TopBarLayoutData layout)
        {
            if (layout.logoPathProperty?.displayName != null)
            {
                Texture2D logo = Resources.Load<Texture2D>(layout.logoPathProperty.displayName);
                if (logo != null)
                {
                    GUI.DrawTexture(layout.logoRect, logo, ScaleMode.ScaleToFit);
                }
            }
        }



        private void DrawHeaderElements(TopBarLayoutData layout)
        {
            // Draw the header if it exists
            if (_shaderHeader?.Options.texture != null) 
                _shaderHeader.Draw();

            bool drawAboveToolbar = !EditorGUIUtility.wideMode;
            if (_shaderHeader != null && drawAboveToolbar) 
                _shaderHeader.Draw(EditorGUILayout.GetControlRect());

            // Header rect for master label
            Rect headerRect = new Rect(0, 0, EditorGUIUtility.currentViewWidth, 25);

            if (LocaleProperty != null)
            {
                Rect localeRect = new Rect(headerRect.width - 100, 0, 100, 25);
                LocaleProperty.Draw(localeRect);
                headerRect.width -= 100;
            }

            // Draw master label text
            if (_shaderHeader != null && !drawAboveToolbar) 
                _shaderHeader.Draw(headerRect);
        }

        // Data structure to hold layout information
        private struct TopBarLayoutData
        {
            public MaterialProperty logoPathProperty;
            public MaterialProperty bgPathProperty; 
            public MaterialProperty characterLeftProperty;
            public MaterialProperty characterRightProperty;
            public Rect contentRect;
            public Rect bgRect;
            public Rect logoRect;
            public float originalY;
        }

        /// <summary>
        /// Draws character images at the absolute left or right edges of the UI
        /// </summary>
        /// <param name="characterProperty">The shader property containing the character texture path</param>
        /// <param name="layout">Layout data containing all positioning information</param>
        /// <param name="isLeftSide">True for left character, false for right character</param>
        private void DrawCharacterImage(MaterialProperty characterProperty, TopBarLayoutData layout, bool isLeftSide)
        {
            if (characterProperty?.displayName == null)
                return;

            Texture2D characterTexture = Resources.Load<Texture2D>(characterProperty.displayName);
            if (characterTexture == null)
                return;

            var characterRect = CalculateCharacterRect(characterTexture, layout, isLeftSide);
            GUI.DrawTexture(characterRect, characterTexture, ScaleMode.ScaleToFit);
        }

        /// <summary>
        /// Calculates the optimal rectangle for a character image
        /// </summary>
        private Rect CalculateCharacterRect(Texture2D texture, TopBarLayoutData layout, bool isLeftSide)
        {
            // Calculate character dimensions while maintaining aspect ratio
            float aspectRatio = (float)texture.width / texture.height;
            float characterWidth = Mathf.Min(TopBarLayout.CHARACTER_MAX_WIDTH, TopBarLayout.CHARACTER_MAX_HEIGHT * aspectRatio);
            float characterHeight = Mathf.Min(TopBarLayout.CHARACTER_MAX_HEIGHT, TopBarLayout.CHARACTER_MAX_WIDTH / aspectRatio);

            // Calculate Y position (bottom of banner)
            float characterY = layout.originalY + TopBarLayout.BANNER_HEIGHT - characterHeight;

            // Calculate X position with logo boundary constraints
            float characterX;
            if (isLeftSide)
            {
                float maxAllowedX = layout.logoRect.x - characterWidth - TopBarLayout.MIN_LOGO_DISTANCE;
                characterX = Mathf.Min(layout.bgRect.x, maxAllowedX);
            }
            else
            {
                float minAllowedX = layout.logoRect.xMax + TopBarLayout.MIN_LOGO_DISTANCE;
                characterX = Mathf.Max(layout.bgRect.xMax - characterWidth, minAllowedX);
            }

            return new Rect(characterX, characterY, characterWidth, characterHeight);
        }

        private void GUILockinButton()
        {
            if (ShaderOptimizerProperty != null)
                ShaderOptimizerProperty.Draw();
        }

        private void GUIPresetsBar()
        {
            Rect barRect = RectifiedLayout.GetRect(25);

            Rect inShaderRect = new Rect(barRect);
            inShaderRect.width /= 3;
            inShaderRect.x = barRect.width - inShaderRect.width;

            Rect presetsRect = new Rect(barRect);
            presetsRect.width = inShaderRect.width;
            presetsRect.height = 18;

            Rect presetsIcon = new Rect(presetsRect);
            presetsIcon.width = 18;
            presetsIcon.height = 18;
            presetsIcon.x = presetsRect.width - 20;

            if (GUI.Button(presetsRect, "Presets") | GUILib.Button(presetsIcon, Styles.icon_style_presets))
                Presets.OpenPresetsMenu(barRect, this, false);
            HoyoToonWideEnumDrawer.RenderLabel = false;
            HoyoToonWideEnumMultiDrawer.RenderLabel = false;
            if (InShaderPresetsProperty != null)
                InShaderPresetsProperty.Draw(inShaderRect);
            HoyoToonWideEnumDrawer.RenderLabel = true;
            HoyoToonWideEnumMultiDrawer.RenderLabel = true;
        }

        private void GUISearchBar()
        {
            EditorGUI.BeginChangeCheck();
            _enteredSearchTerm = EditorGUILayout.TextField(_enteredSearchTerm, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                _appliedSearchTerm = _enteredSearchTerm.ToLower();
                UpdateSearch(MainGroup);
            }
        }

        private void GUIFooters()
        {
            try
            {
                FooterButton.DrawList(_footers);
            }
            catch (Exception ex)
            {
                HoyoToonLogs.WarningDebug(ex.Message);
            }
            if (GUILayout.Button("@UI Modified by Meliodas", Styles.made_by_style))
                Application.OpenURL("https://www.twitter.com/thryrallo");
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
        }

        private void DoVariantWarning()
        {
#if UNITY_2022_1_OR_NEWER
            if (Materials[0].isVariant)
            {
                EditorGUILayout.HelpBox("This material is a variant. It cannot be locked or uploaded to VRChat.", MessageType.Warning);
            }
#endif
        }

        private void PopupTools(Rect position)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Fix Keywords"), false, delegate ()
            {
                FixKeywords(Materials);
            });
            menu.AddSeparator("");

            int unboundTextures = MaterialCleaner.CountUnusedProperties(MaterialCleaner.CleanPropertyType.Texture, Materials);
            int unboundProperties = MaterialCleaner.CountAllUnusedProperties(Materials);
            List<string> unusedTextures = new List<string>();
            MainGroup.FindUnusedTextures(unusedTextures, true);
            if (unboundTextures > 0 && !IsLockedMaterial)
            {
                menu.AddItem(new GUIContent($"Unbound Textures: {unboundTextures}/List in console"), false, delegate ()
                {
                    MaterialCleaner.ListUnusedProperties(MaterialCleaner.CleanPropertyType.Texture, Materials);
                });
                menu.AddItem(new GUIContent($"Unbound Textures: {unboundTextures}/Remove"), false, delegate ()
                {
                    MaterialCleaner.RemoveUnusedProperties(MaterialCleaner.CleanPropertyType.Texture, Materials);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent($"Unbound textures: 0"));
            }
            if (unusedTextures.Count > 0 && !IsLockedMaterial)
            {
                menu.AddItem(new GUIContent($"Unused Textures: {unusedTextures.Count}/List in console"), false, delegate ()
                {
                    Out("Unused textures", unusedTextures.Select(s => $"↳{s}"));
                });
                menu.AddItem(new GUIContent($"Unused Textures: {unusedTextures.Count}/Remove"), false, delegate ()
                {
                    foreach (string t in unusedTextures) if (PropertyDictionary.ContainsKey(t)) PropertyDictionary[t].MaterialProperty.textureValue = null;
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent($"Unused textures: 0"));
            }
            if (unboundProperties > 0 && !IsLockedMaterial)
            {
                menu.AddItem(new GUIContent($"Unbound properties: {unboundProperties}/List in console"), false, delegate ()
                {
                    MaterialCleaner.ListUnusedProperties(MaterialCleaner.CleanPropertyType.Texture, Materials);
                    MaterialCleaner.ListUnusedProperties(MaterialCleaner.CleanPropertyType.Float, Materials);
                    MaterialCleaner.ListUnusedProperties(MaterialCleaner.CleanPropertyType.Color, Materials);
                });
                menu.AddItem(new GUIContent($"Unbound properties: {unboundProperties}/Remove"), false, delegate ()
                {
                    MaterialCleaner.RemoveAllUnusedProperties(MaterialCleaner.CleanPropertyType.Texture, Materials);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent($"Unbound properties: 0"));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Is Preset"), Presets.IsPreset(Materials[0]), delegate ()
            {
                Presets.SetPreset(Materials, !Presets.IsPreset(Materials[0]));
                this.Reload();
            });
            menu.DropDown(position);
        }

        public static void Out(string s)
        {
            HoyoToonLogs.LogDebug($"<color=#ff80ff></color> {s}");
        }
        public static void Out(string header, params string[] lines)
        {
            HoyoToonLogs.LogDebug($"<color=#ff80ff></color> <b>{header}</b>\n{lines.Aggregate((s1, s2) => s1 + "\n" + s2)}");
        }
        public static void Out(string header, IEnumerable<string> lines)
        {
            if (lines.Count() == 0) Out(header);
            else HoyoToonLogs.LogDebug($"<color=#ff80ff></color> <b>{header}</b>\n{lines.Aggregate((s1, s2) => s1 + "\n" + s2)}");
        }
        public static void Out(string header, Color c, IEnumerable<string> lines)
        {
            if (lines.Count() == 0) Out(header);
            else HoyoToonLogs.LogDebug($"<color=#ff80ff></color> <b><color={ColorUtility.ToHtmlStringRGB(c)}>{header}</b></color> \n{lines.Aggregate((s1, s2) => s1 + "\n" + s2)}");
        }

        private void HandleEvents()
        {
            Event e = Event.current;
            //if reloaded, set reload to false
            if (_doReloadNextDraw && Event.current.type == EventType.Layout) _doReloadNextDraw = false;

            //if was undo, reload
            bool isUndo = (e.type == EventType.ExecuteCommand || e.type == EventType.ValidateCommand) && e.commandName == "UndoRedoPerformed";
            if (isUndo) _doReloadNextDraw = true;


            //on swap
            if (_onSwapToActions != null && _didSwapToShader)
            {
                foreach (DefineableAction a in _onSwapToActions)
                    a.Perform(Materials);
                _onSwapToActions = null;
            }

            //test if material has been reset
            if (_wasUsed && e.type == EventType.Repaint)
            {
                if (Materials[0].HasProperty("shader_is_using_HoyoToon_editor") && Materials[0].GetFloat("shader_is_using_HoyoToon_editor") != 69)
                {
                    _doReloadNextDraw = true;
                    HandleReset();
                    _wasUsed = true;
                }
            }

            if (e.type == EventType.Used) _wasUsed = true;
            if (Input.HadMouseDownRepaint) Input.HadMouseDown = false;
            Input.HadMouseDownRepaint = false;
            IsFirstCall = false;
            materialPropertyDictionary = null;
        }

        //iterate the same way drawing would iterate
        //if display part, display all parents parts
        private void UpdateSearch(ShaderPart part, bool parentIsSearchResult = false)
        {
            bool includesSearchTerm = part.Content.text.ToLower().Contains(_appliedSearchTerm);
            part.has_not_searchedFor = includesSearchTerm || parentIsSearchResult;
            if (part is ShaderGroup)
            {
                foreach (ShaderPart p in (part as ShaderGroup).parts)
                {
                    UpdateSearch(p, includesSearchTerm);
                    part.has_not_searchedFor |= !p.has_not_searchedFor;
                }
            }

            part.has_not_searchedFor = !part.has_not_searchedFor;
        }

        private void ClearSearch()
        {
            _appliedSearchTerm = "";
            UpdateSearch(MainGroup);
        }

        private void HandleReset()
        {
            MaterialLinker.UnlinkAll(Materials[0]);
            ShaderOptimizer.DeleteTags(Materials);
        }

        public void Repaint()
        {
            if (Materials.Length > 0)
                EditorUtility.SetDirty(Materials[0]);
        }

        public static void RepaintActive()
        {
            if (ShaderEditor.Active != null)
                Active.Repaint();
        }

        public void Reload()
        {
            this._isFirstOnGUICall = true;
            this._doReloadNextDraw = true;
            // this.Repaint();
            HoyoToonWideEnumMultiDrawer.Reload();
            HoyoToonWideEnumDrawer.Reload();
            HoyoToonRGBAPackerDrawer.Reload();
        }

        public static void ReloadActive()
        {
            if (ShaderEditor.Active != null)
                Active.Reload();
        }

        public void ApplyDrawers()
        {
            foreach (Material target in Materials)
                MaterialEditor.ApplyMaterialPropertyDrawers(target);
        }

        public static string GetShaderEditorDirectoryPath()
        {
            if (s_edtiorDirectoryPath == null)
            {
                IEnumerable<string> paths = AssetDatabase.FindAssets("HoyoToonEditor").Select(g => AssetDatabase.GUIDToAssetPath(g));
                foreach (string p in paths)
                {
                    if (p.EndsWith("/HoyoToonEditor.cs"))
                        s_edtiorDirectoryPath = Directory.GetParent(Path.GetDirectoryName(p)).FullName;
                }
            }
            return s_edtiorDirectoryPath;
        }

        // Cache property->keyword lookup for performance
        static Dictionary<Shader, List<(string prop, List<string> keywords)>> PropertyKeywordsByShader = new Dictionary<Shader, List<(string prop, List<string> keywords)>>();

        /// <summary> Iterate through all materials to ensure keywords list matches properties. </summary>
        public static void FixKeywords(IEnumerable<Material> materialsToFix)
        {
            // Process Shaders
            IEnumerable<Material> uniqueShadersMaterials = materialsToFix.GroupBy(m => m.shader).Select(g => g.First());
            IEnumerable<Shader> shadersWithHoyoToonEditor = uniqueShadersMaterials.Where(m => ShaderHelper.IsShaderUsingHoyoToonEditor(m)).Select(m => m.shader);

            // Clear cache every time if in developer mode, so that changes aren't missed
            if (Config.Singleton.enableDeveloperMode)
                PropertyKeywordsByShader.Clear();

            float f = 0;
            int count = shadersWithHoyoToonEditor.Count();

            if (count > 1) EditorUtility.DisplayProgressBar("Validating Keywords", "Processing Shaders", 0);

            foreach (Shader s in shadersWithHoyoToonEditor)
            {
                if (count > 1) EditorUtility.DisplayProgressBar("Validating Keywords", $"Processing Shader: {s.name}", f++ / count);
                if (!PropertyKeywordsByShader.ContainsKey(s))
                    PropertyKeywordsByShader[s] = ShaderHelper.GetPropertyKeywordsForShader(s);
            }
            // Find Materials
            IEnumerable<Material> materials = materialsToFix.Where(m => PropertyKeywordsByShader.ContainsKey(m.shader));
            f = 0;
            count = materials.Count();

            // Set Keywords
            foreach (Material m in materials)
            {
                if (count > 1) EditorUtility.DisplayProgressBar("Validating Keywords", $"Validating Material: {m.name}", f++ / count);

                List<string> keywordsInMaterial = m.shaderKeywords.ToList();

                foreach ((string prop, List<string> keywords) in PropertyKeywordsByShader[m.shader])
                {
                    switch (keywords.Count)
                    {
                        case 0:
                            break;
                        case 1:
                            string keyword = keywords[0];
                            keywordsInMaterial.Remove(keyword);

                            if (m.GetFloat(prop) == 1)
                                m.EnableKeyword(keyword);
                            else
                                m.DisableKeyword(keyword);
                            break;
                        default: // KeywordEnum
                            for (int i = 0; i < keywords.Count; i++)
                            {
                                keywordsInMaterial.Remove(keywords[i]);
                                if (m.GetFloat(prop) == i)
                                    m.EnableKeyword(keywords[i]);
                                else
                                    m.DisableKeyword(keywords[i]);
                            }
                            break;
                    }
                }

                // Disable any remaining keywords
                foreach (string keyword in keywordsInMaterial)
                    m.DisableKeyword(keyword);
            }
            if (count > 1) EditorUtility.ClearProgressBar();
        }

        /// <summary> Iterate through all materials with FixKeywords. </summary>
        //[MenuItem("HoyoToon/Shader Tools/Fix Keywords for All Materials (Slow)", priority = -20)]
        static void FixAllKeywords()
        {
            IEnumerable<Material> materialsToFix = AssetDatabase.FindAssets("t:material")
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => string.IsNullOrEmpty(p) == false)
                .Select(p => AssetDatabase.LoadAssetAtPath<Material>(p))
                .Where(m => m != null && m.shader != null)
                .Where(m => ShaderOptimizer.IsMaterialLocked(m) == false);

            FixKeywords(materialsToFix);
        }



        [MenuItem("HoyoToon/Settings/Shader Settings", priority = 100)]
        static void MenuShaderUISettings()
        {
            EditorWindow.GetWindow<Settings>(false, "HoyoToon Settings", true);
        }

        //[MenuItem("HoyoToon/Shader Optimizer/Upgraded Animated Properties", priority = -20)]
        static void MenuUpgradeAnimatedPropertiesToTagsOnAllMaterials()
        {
            ShaderOptimizer.UpgradeAnimatedPropertiesToTagsOnAllMaterials();
        }

        [MenuItem("Assets/HoyoToon/Materials/Optimizer/Materials List", priority = 40)]
        static void MenuShaderOptUnlockedMaterials()
        {
            EditorWindow.GetWindow<UnlockedMaterialsList>(false, "Materials", true);
        }

        [MenuItem("Assets/HoyoToon/Materials/Cleaner/List Unbound Properties", priority = 30)]
        static void AssetsCleanMaterials_ListUnboundProperties()
        {
            IEnumerable<Material> materials = Selection.objects.Where(o => o is Material).Select(o => o as Material);
            foreach (Material m in materials)
            {
                HoyoToonLogs.LogDebug("_______Unbound Properties for " + m.name + "_______");
                MaterialCleaner.ListUnusedProperties(MaterialCleaner.CleanPropertyType.Texture, m);
                MaterialCleaner.ListUnusedProperties(MaterialCleaner.CleanPropertyType.Color, m);
                MaterialCleaner.ListUnusedProperties(MaterialCleaner.CleanPropertyType.Float, m);
            }
        }

        [MenuItem("Assets/HoyoToon/Materials/Cleaner/Remove Unbound Textures", priority = 31)]
        static void AssetsCleanMaterials_CleanUnboundTextures()
        {
            IEnumerable<Material> materials = Selection.objects.Where(o => o is Material).Select(o => o as Material);
            foreach (Material m in materials)
            {
                HoyoToonLogs.LogDebug("_______Removing Unbound Textures for " + m.name + "_______");
                MaterialCleaner.RemoveAllUnusedProperties(MaterialCleaner.CleanPropertyType.Texture, m);
            }
        }
    }
}
#endif