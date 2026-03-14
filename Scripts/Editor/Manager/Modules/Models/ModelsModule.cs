#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class ModelsModule : ManagerModule
    {

        internal const string ShareUrl = "https://cdn.hoyotoon.com/s/pXIz";
        internal const string CharactersFolderName = "Characters";
        internal const string DefaultVariantName = "Default";
        internal const string DefaultDownloadRoot = "Assets/HoyoToon/Characters";
        internal const int MaxParallelDownloads = 8;

        private static readonly int MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;


        internal readonly Dictionary<string, GameFolderCache> _gameCache = new Dictionary<string, GameFolderCache>(StringComparer.OrdinalIgnoreCase);
        internal readonly List<GameOption> _availableGames = new List<GameOption>();

        internal int _selectedGameIndex = -1;
        internal int _selectedCharacterIndex = -1;
        internal int _selectedVariantIndex = -1;

        internal List<string> _characters = new List<string>();
        internal List<VariantOption> _variants = new List<VariantOption>();

        private bool _hasLoadedOnce;
        internal bool _isRefreshing;
        internal string _statusMessage;
        internal string _downloadRoot;
        private bool _hasLoadedAutoSetupPreference;
        internal bool _autoSetupAfterDownload = true;
        internal string _characterSearch;
        internal Vector2 _gameScroll;
        internal Vector2 _characterScroll;
        internal Vector2 _variantScroll;
        internal Vector2 _variantSectionScroll;
        internal readonly Dictionary<string, Vector2> _variantScrollByCharacter = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> _selectedCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> _selectedVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, HashSet<string>> _selectedVariantsByCharacter = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> _loadingVariantsForCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal HsrFbxChoice _hsrFbxChoice = HsrFbxChoice.WithAnims;

        private List<string> _filteredCharactersCache = new List<string>();
        private bool _filteredCharactersDirty = true;


        private readonly ModelsModuleDrawer _drawer;
        private readonly ModelsDownloadService _downloadService;

        public ModelsModule()
        {
            _downloadService = new ModelsDownloadService(this);
            _drawer = new ModelsModuleDrawer(this);
        }


        public override string DisplayName => "Models";
        internal override string NavbarTourTarget => "tour.modules.models";

        public override void OnGUI(HoyoToonManager targetManager)
        {
            if (targetManager == null)
            {
                EditorGUILayout.HelpBox("Assign a HoyoToon Manager to download and configure models.", MessageType.Info);
                return;
            }

            EnsureDownloadRoot();
            EnsureAutoSetupPreference();
            EnsureInitialLoad();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _drawer.DrawStatus();
                _drawer.DrawRefreshRow();
                EditorGUILayout.Space(4f);
                _drawer.DrawGameSelection();
                _drawer.DrawCharacterSelection();
                _drawer.DrawVariantSelection();
                EditorGUILayout.Space(4f);
                _drawer.DrawDownloadActions(targetManager);
            }
        }


        internal void StartRefreshGameList() => _downloadService.StartRefreshGameList();
        internal void EnsureCharactersLoaded() => _downloadService.EnsureCharactersLoaded();
        internal void EnsureVariantsLoaded() => _downloadService.EnsureVariantsLoaded();
        internal void EnsureVariantsLoadedForCharacter(GameOption game, string characterName) => _downloadService.EnsureVariantsLoadedForCharacter(game, characterName);
        internal List<VariantOption> GetVariantsForCharacter(GameOption game, string characterName) => _downloadService.GetVariantsForCharacter(game, characterName);
        internal void StartDownloadSelected(HoyoToonManager manager, System.Threading.CancellationToken cancellationToken = default) => _downloadService.StartDownloadSelected(manager, cancellationToken);


        internal void EnsureDownloadRoot()
        {
            if (!string.IsNullOrEmpty(_downloadRoot))
            {
                return;
            }

            _downloadRoot = EditorPrefs.GetString(PrefsKeys.ModelsDownloadRoot, DefaultDownloadRoot);
            if (string.IsNullOrWhiteSpace(_downloadRoot))
            {
                _downloadRoot = DefaultDownloadRoot;
            }
        }

        internal void EnsureAutoSetupPreference()
        {
            if (_hasLoadedAutoSetupPreference)
            {
                return;
            }

            _hasLoadedAutoSetupPreference = true;
            _autoSetupAfterDownload = EditorPrefs.GetBool(PrefsKeys.ModelsAutoSetup, true);
        }

        internal void EnsureInitialLoad()
        {
            if (_hasLoadedOnce)
            {
                return;
            }

            _hasLoadedOnce = true;
            StartRefreshGameList();
        }


        internal List<string> GetFilteredCharacters()
        {
            if (_characters == null)
            {
                return new List<string>();
            }

            if (!_filteredCharactersDirty)
            {
                return _filteredCharactersCache;
            }

            if (string.IsNullOrWhiteSpace(_characterSearch))
            {
                _filteredCharactersCache = new List<string>(_characters);
                _filteredCharactersDirty = false;
                return _filteredCharactersCache;
            }

            _filteredCharactersCache = _characters
                .Where(name => !string.IsNullOrEmpty(name) && name.IndexOf(_characterSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            _filteredCharactersDirty = false;
            return _filteredCharactersCache;
        }

        internal void SetCharacterSearch(string value)
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_characterSearch, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _characterSearch = normalized;
            _filteredCharactersDirty = true;
        }

        internal void MarkFilteredCharactersDirty()
        {
            _filteredCharactersDirty = true;
        }

        internal List<string> GetSelectedCharacterList()
        {
            return GetOrderedSelectedCharacters(includeFallbackSelection: true);
        }

        internal List<string> GetOrderedSelectedCharacters(bool includeFallbackSelection)
        {
            var ordered = new List<string>();
            if (_selectedCharacters.Count > 0)
            {
                for (int i = 0; i < _characters.Count; i++)
                {
                    var name = _characters[i];
                    if (_selectedCharacters.Contains(name))
                    {
                        ordered.Add(name);
                    }
                }

                return ordered;
            }

            if (includeFallbackSelection && _selectedCharacterIndex >= 0 && _selectedCharacterIndex < _characters.Count)
            {
                ordered.Add(_characters[_selectedCharacterIndex]);
            }

            return ordered;
        }

        internal Vector2 GetVariantScroll(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return Vector2.zero;
            }

            if (!_variantScrollByCharacter.TryGetValue(characterName, out var scroll))
            {
                scroll = Vector2.zero;
                _variantScrollByCharacter[characterName] = scroll;
            }

            return scroll;
        }

        internal void SetVariantScroll(string characterName, Vector2 scroll)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            _variantScrollByCharacter[characterName] = scroll;
        }

        internal HashSet<string> GetOrCreateVariantSelection(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!_selectedVariantsByCharacter.TryGetValue(characterName, out var selection))
            {
                selection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _selectedVariantsByCharacter[characterName] = selection;
            }

            return selection;
        }

        internal void ClearNonTourCharacterSelection()
        {
            if (string.IsNullOrEmpty(HoyoToon.Editor.Onboarding.GuidedTourController.DesiredCharacterName))
            {
                return;
            }

            if (_selectedCharacters.Count == 1 && _selectedCharacters.Contains(HoyoToon.Editor.Onboarding.GuidedTourController.DesiredCharacterName))
            {
                return;
            }

            _selectedCharacters.Clear();
            _selectedCharacterIndex = -1;
        }

        internal void ClearNonTourVariantSelection()
        {
            if (string.IsNullOrEmpty(HoyoToon.Editor.Onboarding.GuidedTourController.DesiredVariantName))
            {
                return;
            }

            if (_selectedVariants.Count == 1 && _selectedVariants.Contains(HoyoToon.Editor.Onboarding.GuidedTourController.DesiredVariantName))
            {
                return;
            }

            _selectedVariants.Clear();
            _selectedVariantIndex = -1;
        }

        internal GameFolderCache GetOrCreateCache(GameOption option)
        {
            if (option == null)
            {
                return null;
            }

            if (!_gameCache.TryGetValue(option.Key, out var cache))
            {
                cache = new GameFolderCache(option.FolderName);
                _gameCache[option.Key] = cache;
            }

            return cache;
        }

        internal bool TryGetBatchSelection(out GameOption game, out List<string> characters, out List<string> variants)
        {
            game = null;
            characters = new List<string>();
            variants = new List<string>();

            if (_selectedGameIndex < 0 || _selectedGameIndex >= _availableGames.Count)
            {
                return false;
            }

            game = _availableGames[_selectedGameIndex];

            if (_selectedCharacters.Count > 0)
            {
                characters = GetOrderedSelectedCharacters(includeFallbackSelection: false);
            }
            else
            {
                characters = GetOrderedSelectedCharacters(includeFallbackSelection: true);
            }

            if (_selectedVariants.Count > 0)
            {
                foreach (var option in _variants)
                {
                    if (_selectedVariants.Contains(option.Name))
                    {
                        variants.Add(option.Name);
                    }
                }
            }
            else if (_selectedVariantIndex >= 0 && _selectedVariantIndex < _variants.Count)
            {
                variants.Add(_variants[_selectedVariantIndex].Name);
            }
            else if (_selectedCharacters.Count > 1 && _selectedVariantsByCharacter.Count > 0)
            {
                var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var selection in _selectedVariantsByCharacter.Values)
                {
                    foreach (var name in selection)
                    {
                        distinct.Add(name);
                    }
                }

                variants.AddRange(distinct);
            }

            return characters.Count > 0 && variants.Count > 0;
        }

        internal bool IsSelectedGame(string gameKey)
        {
            return _selectedGameIndex >= 0
                   && _selectedGameIndex < _availableGames.Count
                   && string.Equals(_availableGames[_selectedGameIndex].Key, gameKey, StringComparison.OrdinalIgnoreCase);
        }

        internal bool IsSelectedCharacter(string characterName)
        {
            return _selectedCharacterIndex >= 0
                   && _selectedCharacterIndex < _characters.Count
                   && string.Equals(_characters[_selectedCharacterIndex], characterName, StringComparison.OrdinalIgnoreCase);
        }

        internal void ClearSelections()
        {
            _availableGames.Clear();
            _selectedGameIndex = -1;
            _gameScroll = Vector2.zero;
            ClearCharacterSelection();
        }

        internal void ClearCharacterSelection()
        {
            _characters = new List<string>();
            _filteredCharactersCache = new List<string>();
            _filteredCharactersDirty = true;
            _selectedCharacterIndex = -1;
            _characterScroll = Vector2.zero;
            _characterSearch = string.Empty;
            _selectedCharacters.Clear();
            ClearVariantSelection();
        }

        internal void ClearVariantSelection()
        {
            _variants = new List<VariantOption>();
            _selectedVariantIndex = -1;
            _variantScroll = Vector2.zero;
            _variantSectionScroll = Vector2.zero;
            _selectedVariants.Clear();
            _selectedVariantsByCharacter.Clear();
            _loadingVariantsForCharacters.Clear();
            _variantScrollByCharacter.Clear();
        }

        internal static void ScheduleOnMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (System.Threading.Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                action();
                return;
            }

            EditorApplication.delayCall += () => action();
        }
    }
}
#endif
