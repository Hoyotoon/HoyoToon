using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using HoyoToon.UI.Core;
using HoyoToon.UI.Components;

namespace HoyoToon
{
    /// <summary>
    /// HoyoToon Resources Tab Controller
    /// Handles local resource management and download from repository
    /// Updated to use the new modular UI system
    /// </summary>
    public class HoyoToonResourcesTabController : HoyoToonModularBaseTabController
    {
        public override string TabName => "Resources";
        public override bool RequiresModel => false; // Resources can work independently

        private string searchFilter = "";
        private string selectedGameFilter = "All";
        private ScrollView resourceListView;
        private List<LocalResourceInfo> localResources;
        private ResourceTreeNode resourceTree;
        private Dictionary<string, bool> expandedFolders = new Dictionary<string, bool>();

        #region Modular UI System Implementation

        protected override void InitializeTabComponents()
        {
            // Register components for Resources tab
            AddComponent<ValidationStatusComponent>();
            AddComponent<ProgressIndicatorComponent>();
        }

        public override List<QuickAction> GetQuickActions()
        {
            var actions = new List<QuickAction>();

            // Download All Resources action
            actions.Add(new QuickAction("Download All Resources", () =>
            {
                try
                {
                    HoyoToonResourceManager.DownloadAllResources();
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to download resources: {e.Message}", "OK");
                }
            }));

            // Check for Updates action
            actions.Add(new QuickAction("Check for Updates", () =>
            {
                try
                {
                    HoyoToonResourceManager.CheckResourceStatus();
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to check updates: {e.Message}", "OK");
                }
            }));

            // Validate Resources action
            actions.Add(new QuickAction("Validate Resources", () =>
            {
                try
                {
                    HoyoToonResourceManager.CheckResourceStatus();
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to validate resources: {e.Message}", "OK");
                }
            }));

            return actions;
        }

        #endregion

        protected override void OnInitialize()
        {
            localResources = new List<LocalResourceInfo>();
            expandedFolders = new Dictionary<string, bool>();
            ScanLocalResources();
        }

        protected override void CreateTabContent()
        {
            // Game Resource Status Section
            contentView.Add(CreateHoyoToonSectionHeader("Game Resource Status"));
            CreateDownloadManagementSection();

            // Search and Filter Section
            contentView.Add(CreateHoyoToonSectionHeader("Browse Local Resources"));
            CreateSearchAndFilterSection();

            // Local Resources List
            CreateResourceListSection();
        }

        private void CreateSearchAndFilterSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;
            container.style.marginBottom = 15;

            // Search and filter row
            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;

            // Search field
            var searchField = CreateHoyoToonTextField("Search:");
            searchField.style.flexGrow = 1;
            searchField.style.marginRight = 10;
            searchField.value = searchFilter;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchFilter = evt.newValue;
                FilterAndRefreshResourceList();
            });

            // Game filter dropdown
            var gameFilterContainer = new VisualElement();
            gameFilterContainer.style.flexDirection = FlexDirection.Row;
            gameFilterContainer.style.alignItems = Align.Center;
            gameFilterContainer.style.marginRight = 10;

            var gameFilterLabel = new Label("Game:");
            gameFilterLabel.style.minWidth = 50;
            gameFilterLabel.style.marginRight = 5;

            var gameFilterDropdown = CreateHoyoToonDropdown();
            gameFilterDropdown.choices = new List<string> { "All", "Genshin Impact", "Honkai Impact 3rd", "Honkai Star Rail", "Wuthering Waves", "Zenless Zone Zero" };
            gameFilterDropdown.value = selectedGameFilter;
            gameFilterDropdown.style.minWidth = 150;
            gameFilterDropdown.RegisterValueChangedCallback(evt =>
            {
                selectedGameFilter = evt.newValue;
                FilterAndRefreshResourceList();
            });

            // Refresh button
            var refreshBtn = CreateHoyoToonStyledButton("Refresh", () =>
            {
                ScanLocalResources();
                FilterAndRefreshResourceList();
            }, new Color(0.3f, 0.7f, 0.4f));
            refreshBtn.style.minWidth = 80;

            gameFilterContainer.Add(gameFilterLabel);
            gameFilterContainer.Add(gameFilterDropdown);

            searchContainer.Add(searchField);
            searchContainer.Add(gameFilterContainer);
            searchContainer.Add(refreshBtn);

            container.Add(searchContainer);
            contentView.Add(container);
        }

        private void CreateResourceListSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;
            container.style.height = 200;

            // Header
            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            headerContainer.style.paddingTop = 5;
            headerContainer.style.paddingBottom = 5;
            headerContainer.style.paddingLeft = 10;
            headerContainer.style.paddingRight = 10;

            var nameHeader = new Label("Resource Structure");
            nameHeader.style.flexGrow = 1;
            nameHeader.style.unityFontStyleAndWeight = FontStyle.Bold;

            var typeHeader = new Label("Type");
            typeHeader.style.minWidth = 80;
            typeHeader.style.unityFontStyleAndWeight = FontStyle.Bold;

            var sizeHeader = new Label("Size");
            sizeHeader.style.minWidth = 80;
            sizeHeader.style.unityFontStyleAndWeight = FontStyle.Bold;

            var actionsHeader = new Label("Actions");
            actionsHeader.style.minWidth = 100;
            actionsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;

            headerContainer.Add(nameHeader);
            headerContainer.Add(typeHeader);
            headerContainer.Add(sizeHeader);
            headerContainer.Add(actionsHeader);

            container.Add(headerContainer);

            // Scrollable resource tree
            resourceListView = new ScrollView();
            resourceListView.style.flexGrow = 1;
            resourceListView.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            container.Add(resourceListView);

            FilterAndRefreshResourceList();

            contentView.Add(container);
        }

        private void CreateDownloadManagementSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Game download status
            var gameResourceStatus = HoyoToonResourceManager.GetResourceStatus();

            container.Add(CreateHoyoToonSubsectionHeader("Game Resource Status:"));

            foreach (var kvp in gameResourceStatus)
            {
                var status = kvp.Value;
                var statusContainer = new VisualElement();
                statusContainer.style.flexDirection = FlexDirection.Row;
                statusContainer.style.alignItems = Align.Center;
                statusContainer.style.marginBottom = 5;
                statusContainer.style.paddingTop = 5;
                statusContainer.style.paddingBottom = 5;
                statusContainer.style.paddingLeft = 10;
                statusContainer.style.paddingRight = 10;
                statusContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.5f);
                statusContainer.style.borderTopLeftRadius = 4;
                statusContainer.style.borderTopRightRadius = 4;
                statusContainer.style.borderBottomLeftRadius = 4;
                statusContainer.style.borderBottomRightRadius = 4;

                var gameInfo = new VisualElement();
                gameInfo.style.flexGrow = 1;

                var gameName = new Label(status.DisplayName);
                gameName.style.unityFontStyleAndWeight = FontStyle.Bold;
                gameName.style.color = Color.white;

                var gameDetails = new Label($"{status.FileCount} files • {FormatFileSize(status.TotalSize)} • Last sync: {status.LastSync:yyyy-MM-dd HH:mm}");
                gameDetails.style.fontSize = 11;
                gameDetails.style.color = new Color(0.8f, 0.8f, 0.8f);

                gameInfo.Add(gameName);
                gameInfo.Add(gameDetails);

                var statusLabel = new Label(status.HasResources ? "Downloaded" : "Missing");
                statusLabel.style.color = status.HasResources ? Color.green : Color.yellow;
                statusLabel.style.minWidth = 80;

                var downloadBtn = CreateHoyoToonStyledButton("Download", () =>
                {
                    DownloadGameResources(status.GameKey);
                }, new Color(0.2f, 0.6f, 0.8f));
                downloadBtn.style.minWidth = 80;

                var deleteBtn = CreateHoyoToonStyledButton("Delete", () =>
                {
                    DeleteGameResources(status.GameKey);
                }, new Color(0.7f, 0.3f, 0.3f));
                deleteBtn.style.minWidth = 80;
                deleteBtn.style.marginLeft = 5;

                statusContainer.Add(gameInfo);
                statusContainer.Add(statusLabel);
                statusContainer.Add(downloadBtn);
                if (status.HasResources)
                {
                    statusContainer.Add(deleteBtn);
                }

                container.Add(statusContainer);
            }

            contentView.Add(container);
        }

        #region Resource Management

        private void ScanLocalResources()
        {
            localResources.Clear();
            localResources.AddRange(HoyoToonAssetService.GetAllLocalResources());
            HoyoToonLogs.LogDebug($"Scanned {localResources.Count} resource files using HoyoToonResourceManager");
        }

        private void FilterAndRefreshResourceList()
        {
            if (resourceListView == null) return;

            resourceListView.Clear();

            var filteredResources = localResources.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                filteredResources = filteredResources.Where(r =>
                    r.FileName.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase) ||
                    r.RelativePath.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase));
            }

            // Apply game filter
            if (selectedGameFilter != "All")
            {
                var gameKey = GetGameKeyFromDisplayName(selectedGameFilter);
                if (!string.IsNullOrEmpty(gameKey))
                {
                    filteredResources = filteredResources.Where(r => r.GameKey == gameKey);
                }
            }

            // Build hierarchical tree structure
            resourceTree = BuildResourceTree(filteredResources);

            // Apply saved expansion states
            ApplyExpansionStates(resourceTree);

            // Create tree view
            CreateTreeView(resourceTree, 0);

            // Show count
            var countLabel = new Label($"Showing {filteredResources.Count()} of {localResources.Count} resources");
            countLabel.style.fontSize = 11;
            countLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            countLabel.style.marginTop = 5;
            countLabel.style.marginLeft = 10;
            resourceListView.Add(countLabel);
        }

        private ResourceTreeNode BuildResourceTree(IEnumerable<LocalResourceInfo> resources)
        {
            var root = new ResourceTreeNode { Name = "Resources", IsFolder = true };

            foreach (var resource in resources)
            {
                // Build path: GameName/subfolder/subfolder/file
                var pathParts = new List<string> { GetGameDisplayName(resource.GameKey) };

                // Add subfolders from relative path
                var relativeParts = resource.RelativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                pathParts.AddRange(relativeParts.Take(relativeParts.Length - 1)); // All parts except filename

                // Navigate/create folder structure
                var currentNode = root;
                foreach (var part in pathParts)
                {
                    var childNode = currentNode.Children.FirstOrDefault(c => c.Name == part && c.IsFolder);
                    if (childNode == null)
                    {
                        childNode = new ResourceTreeNode
                        {
                            Name = part,
                            IsFolder = true,
                            Parent = currentNode
                        };
                        currentNode.Children.Add(childNode);
                    }
                    currentNode = childNode;
                }

                // Add file node
                var fileNode = new ResourceTreeNode
                {
                    Name = resource.FileName,
                    IsFolder = false,
                    Resource = resource,
                    Parent = currentNode
                };
                currentNode.Children.Add(fileNode);
            }

            // Sort tree nodes
            SortTreeNode(root);
            return root;
        }

        private void SortTreeNode(ResourceTreeNode node)
        {
            // Sort children: folders first, then files, both alphabetically
            node.Children = node.Children.OrderBy(n => !n.IsFolder).ThenBy(n => n.Name).ToList();

            foreach (var child in node.Children)
            {
                SortTreeNode(child);
            }
        }

        private void ApplyExpansionStates(ResourceTreeNode node)
        {
            if (node.IsFolder)
            {
                var folderPath = GetNodePath(node);
                if (expandedFolders.TryGetValue(folderPath, out bool isExpanded))
                {
                    node.IsExpanded = isExpanded;
                }
            }

            foreach (var child in node.Children)
            {
                ApplyExpansionStates(child);
            }
        }

        private string GetNodePath(ResourceTreeNode node)
        {
            var path = new List<string>();
            var current = node;
            while (current != null && !string.IsNullOrEmpty(current.Name))
            {
                path.Insert(0, current.Name);
                current = current.Parent;
            }
            return string.Join("/", path);
        }

        private void CreateTreeView(ResourceTreeNode node, int depth)
        {
            // Skip root node display
            if (depth > 0)
            {
                var treeItem = CreateTreeItem(node, depth);
                resourceListView.Add(treeItem);
            }

            // Show children if folder is expanded or it's the root
            if (node.IsExpanded || depth == 0)
            {
                foreach (var child in node.Children)
                {
                    CreateTreeView(child, depth + 1);
                }
            }
        }

        private VisualElement CreateTreeItem(ResourceTreeNode node, int depth)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingTop = 2;
            item.style.paddingBottom = 2;
            item.style.paddingLeft = 10 + (depth - 1) * 20; // Indent based on depth
            item.style.paddingRight = 10;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);

            // Folder toggle or file icon + name
            var nameContainer = new VisualElement();
            nameContainer.style.flexDirection = FlexDirection.Row;
            nameContainer.style.alignItems = Align.Center;
            nameContainer.style.flexGrow = 1;

            if (node.IsFolder)
            {
                // Folder toggle button - capture node reference properly
                var currentNode = node; // Explicit capture
                var toggleButton = new Button(() =>
                {
                    currentNode.IsExpanded = !currentNode.IsExpanded;

                    // Save expansion state
                    var folderPath = GetNodePath(currentNode);
                    expandedFolders[folderPath] = currentNode.IsExpanded;

                    FilterAndRefreshResourceList(); // Refresh to show/hide children
                });
                toggleButton.text = node.IsExpanded ? "▼" : "▶";
                toggleButton.style.fontSize = 10;
                toggleButton.style.width = 20;
                toggleButton.style.height = 16;
                toggleButton.style.marginRight = 5;
                toggleButton.style.paddingTop = 0;
                toggleButton.style.paddingBottom = 0;
                toggleButton.style.paddingLeft = 0;
                toggleButton.style.paddingRight = 0;

                // Folder icon and name
                var folderLabel = new Label($"[Folder] {node.Name}");
                folderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                folderLabel.style.color = new Color(0.9f, 0.9f, 0.6f); // Slight yellow tint for folders

                nameContainer.Add(toggleButton);
                nameContainer.Add(folderLabel);

                // Folder info
                var fileCount = CountFilesInNode(node);
                var folderSize = CalculateNodeSize(node);

                var typeLabel = new Label($"{fileCount} items");
                typeLabel.style.minWidth = 80;
                typeLabel.style.fontSize = 11;
                typeLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

                var sizeLabel = new Label(FormatFileSize(folderSize));
                sizeLabel.style.minWidth = 80;
                sizeLabel.style.fontSize = 11;
                sizeLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

                var actionsContainer = new VisualElement();
                actionsContainer.style.minWidth = 100;

                item.Add(nameContainer);
                item.Add(typeLabel);
                item.Add(sizeLabel);
                item.Add(actionsContainer);
            }
            else
            {
                // File icon and name
                var fileIcon = GetFileIcon(node.Resource.FileType);
                var fileLabel = new Label($"{fileIcon} {node.Name}");
                fileLabel.style.color = Color.white;

                nameContainer.Add(fileLabel);

                // File info
                var typeLabel = new Label(node.Resource.FileType);
                typeLabel.style.minWidth = 80;
                typeLabel.style.fontSize = 11;
                typeLabel.style.color = new Color(0.8f, 0.8f, 0.8f);

                var sizeLabel = new Label(FormatFileSize(node.Resource.Size));
                sizeLabel.style.minWidth = 80;
                sizeLabel.style.fontSize = 11;
                sizeLabel.style.color = new Color(0.8f, 0.8f, 0.8f);

                // Actions
                var actionsContainer = new VisualElement();
                actionsContainer.style.flexDirection = FlexDirection.Row;
                actionsContainer.style.minWidth = 100;

                var selectBtn = CreateHoyoToonStyledButton("Select", () =>
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        Path.GetRelativePath(Application.dataPath.Replace("/Assets", ""), node.Resource.FullPath));
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }, new Color(0.3f, 0.7f, 0.4f));
                selectBtn.style.fontSize = 10;
                selectBtn.style.minWidth = 50;
                selectBtn.style.height = 18;

                actionsContainer.Add(selectBtn);

                item.Add(nameContainer);
                item.Add(typeLabel);
                item.Add(sizeLabel);
                item.Add(actionsContainer);
            }

            return item;
        }

        private int CountFilesInNode(ResourceTreeNode node)
        {
            int count = 0;
            foreach (var child in node.Children)
            {
                if (child.IsFolder)
                {
                    count += CountFilesInNode(child);
                }
                else
                {
                    count++;
                }
            }
            return count;
        }

        private long CalculateNodeSize(ResourceTreeNode node)
        {
            long size = 0;
            foreach (var child in node.Children)
            {
                if (child.IsFolder)
                {
                    size += CalculateNodeSize(child);
                }
                else
                {
                    size += child.Resource.Size;
                }
            }
            return size;
        }

        private string GetFileIcon(string fileType)
        {
            return fileType switch
            {
                "Texture" => "[Texture]",
                "Material" => "[Material]",
                "Prefab" => "[Prefab]",
                "Data" => "[Data]",
                "Shader" => "[Shader]",
                "Script" => "[Script]",
                _ => "[File]"
            };
        }

        private string GetGameDisplayName(string gameKey)
        {
            return gameKey switch
            {
                "GI" => "Genshin Impact",
                "Hi3" => "Honkai Impact 3rd",
                "HSR" => "Honkai Star Rail",
                "Wuwa" => "Wuthering Waves",
                "ZZZ" => "Zenless Zone Zero",
                _ => gameKey
            };
        }

        private string GetGameKeyFromDisplayName(string displayName)
        {
            return displayName switch
            {
                "Genshin Impact" => "GI",
                "Honkai Impact 3rd" => "Hi3",
                "Honkai Star Rail" => "HSR",
                "Wuthering Waves" => "Wuwa",
                "Zenless Zone Zero" => "ZZZ",
                _ => null
            };
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        #endregion

        #region Resource Operations

        private void ValidateLocalResources()
        {
            var issues = new List<string>();
            var packagePath = HoyoToonParseManager.GetPackagePath("com.meliverse.hoyotoon");
            var resourcesPath = Path.Combine(packagePath, "Resources");

            if (!HoyoToonAssetService.AreResourcesAvailable())
            {
                issues.Add("Resources directory not found");
            }
            else
            {
                // Check each game directory
                var gameKeys = new[] { "GI", "Hi3", "HSR", "Wuwa", "ZZZ" };
                foreach (var gameKey in gameKeys)
                {
                    if (!HoyoToonAssetService.HasResourcesForGame(gameKey))
                    {
                        issues.Add($"Missing {GetGameDisplayName(gameKey)} resources");
                    }
                    else
                    {
                        var resources = HoyoToonAssetService.GetLocalResourcesForGame(gameKey);
                        if (resources.Count == 0)
                        {
                            issues.Add($"{GetGameDisplayName(gameKey)} directory is empty");
                        }
                    }
                }
            }

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation Complete", "All resources are valid!", "OK");
            }
            else
            {
                var message = "Resource validation found issues:\n\n• " + string.Join("\n• ", issues);
                EditorUtility.DisplayDialog("Validation Issues", message, "OK");
            }
        }

        private void DownloadGameResources(string gameKey)
        {
            // Map internal game keys to HoyoToonResourceManager keys
            var resourceManagerKey = gameKey switch
            {
                "GI" => "Genshin",
                "Hi3" => "Hi3",
                "HSR" => "StarRail",
                "Wuwa" => "Wuwa",
                "ZZZ" => "ZZZ",
                _ => gameKey
            };

            switch (resourceManagerKey)
            {
                case "Genshin":
                    HoyoToonResourceManager.DownloadGenshinResources();
                    break;
                case "Hi3":
                    HoyoToonResourceManager.DownloadHi3Resources();
                    break;
                case "StarRail":
                    HoyoToonResourceManager.DownloadStarRailResources();
                    break;
                case "Wuwa":
                    HoyoToonResourceManager.DownloadWuwaResources();
                    break;
                case "ZZZ":
                    HoyoToonResourceManager.DownloadZZZResources();
                    break;
            }

            // Refresh the resource list after download
            EditorApplication.delayCall += () =>
            {
                ScanLocalResources();
                FilterAndRefreshResourceList();
            };
        }

        private void DeleteGameResources(string gameKey)
        {
            var gameDisplayName = GetGameDisplayName(gameKey);

            if (EditorUtility.DisplayDialog("Confirm Deletion",
                $"Delete all {gameDisplayName} resources?\n\nThis action cannot be undone.",
                "Delete", "Cancel"))
            {
                // Map internal game keys to HoyoToonResourceManager keys
                var resourceManagerKey = gameKey switch
                {
                    "GI" => "Genshin",
                    "Hi3" => "Hi3",
                    "HSR" => "StarRail",
                    "Wuwa" => "Wuwa",
                    "ZZZ" => "ZZZ",
                    _ => gameKey
                };

                switch (resourceManagerKey)
                {
                    case "Genshin":
                        HoyoToonResourceManager.DeleteGenshinResources();
                        break;
                    case "Hi3":
                        HoyoToonResourceManager.DeleteHi3Resources();
                        break;
                    case "StarRail":
                        HoyoToonResourceManager.DeleteStarRailResources();
                        break;
                    case "Wuwa":
                        HoyoToonResourceManager.DeleteWuwaResources();
                        break;
                    case "ZZZ":
                        HoyoToonResourceManager.DeleteZZZResources();
                        break;
                }

                // Refresh the resource list after deletion
                EditorApplication.delayCall += () =>
                {
                    ScanLocalResources();
                    FilterAndRefreshResourceList();
                };
            }
        }

        #endregion

        protected override void OnCleanup()
        {
            localResources?.Clear();
            expandedFolders?.Clear();
        }
    }

    /// <summary>
    /// Node in the resource tree structure
    /// </summary>
    public class ResourceTreeNode
    {
        public string Name;
        public bool IsFolder;
        public bool IsExpanded = false; // Start collapsed by default
        public LocalResourceInfo Resource; // Null for folders
        public ResourceTreeNode Parent;
        public List<ResourceTreeNode> Children = new List<ResourceTreeNode>();
    }
}