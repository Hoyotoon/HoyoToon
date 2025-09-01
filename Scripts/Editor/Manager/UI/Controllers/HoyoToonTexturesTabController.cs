using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HoyoToon.UI.Core;
using HoyoToon.UI.Components;

namespace HoyoToon
{
    /// <summary>
    /// HoyoToon Textures Tab Controller
    /// API-driven texture analysis, validation, and optimization based on HoyoToon standards
    /// </summary>
    public class HoyoToonTexturesTabController : HoyoToonModularBaseTabController
    {
        public override string TabName => "Textures";

        #region Modular UI System Implementation

        protected override void InitializeTabComponents()
        {
            // Register components for Textures tab
            AddComponent<ValidationStatusComponent>();
            AddComponent<ModelInfoComponent>();
            AddComponent<ProgressIndicatorComponent>();
        }

        public override List<QuickAction> GetQuickActions()
        {
            return GetTexturesQuickActions();
        }

        #endregion

        private List<HoyoToonTextureAnalysis> textureAnalyses;
        private string currentShaderKey;
        private int currentPage = 0;
        private int texturesPerPage = 20;
        private bool showOnlyOptimizable = false;
        private string searchFilter = "";

        // UI Elements for pagination and filtering
        private VisualElement textureGalleryContainer;
        private Label paginationLabel;
        private Button prevPageBtn;
        private Button nextPageBtn;

        private List<QuickAction> GetTexturesQuickActions()
        {
            var actions = new List<QuickAction>();

            if (!IsModelAvailable())
                return actions;

            // Update analysis data
            UpdateTextureAnalysis();

            // Only show actions if there are textures to work with
            if (textureAnalyses?.Count > 0)
            {
                var needsOptimization = textureAnalyses.Where(t => t.IsValid && t.NeedsOptimization).ToList();

                if (needsOptimization.Count > 0)
                {
                    actions.Add(new QuickAction($"Optimize {needsOptimization.Count} Textures", () =>
                    {
                        OptimizeRecommendedTextures();
                    }));
                }

                // Always provide option to apply HoyoToon standards
                actions.Add(new QuickAction("Apply Standards", () =>
                {
                    ApplyHoyoToonStandards();
                }));

                // Validation action
                actions.Add(new QuickAction("Validate All Textures", () =>
                {
                    ValidateAllTextures();
                }));
            }

            return actions;
        }

        protected override void CreateTabContent()
        {
            if (!IsModelAvailable())
            {
                ShowNoModelMessage();
                return;
            }

            // Update analysis data
            UpdateTextureAnalysis();

            // Texture Analysis Overview Section
            contentView.Add(CreateHoyoToonSectionHeader("Texture Analysis Overview"));
            CreateTextureAnalysisOverviewSection();

            // Optimization Recommendations Section
            if (textureAnalyses?.Count > 0)
            {
                contentView.Add(CreateHoyoToonSectionHeader("Optimization Recommendations"));
                CreateOptimizationRecommendationsSection();
            }

            // Detailed Texture Analysis
            if (textureAnalyses?.Count > 0)
            {
                contentView.Add(CreateHoyoToonSectionHeader("Detailed Texture Analysis"));
                CreateDetailedAnalysisSection();
            }
        }

        private void UpdateTextureAnalysis()
        {
            if (analysisData?.textures == null || analysisData.textures.Count == 0)
                return;

            // Determine shader context for analysis
            currentShaderKey = DetermineShaderContext();

            // Get texture paths from analysis data
            var texturePaths = analysisData.textures.Select(t => t.path).ToList();

            // Perform comprehensive analysis using the API
            textureAnalyses = HoyoToonTextureManager.AnalyzeTextures(texturePaths, currentShaderKey);

            HoyoToonLogs.LogDebug($"Analyzed {textureAnalyses.Count} textures for shader context: {currentShaderKey ?? "Global"}");
        }

        private string DetermineShaderContext()
        {
            // Try to determine shader context from the model's materials
            if (analysisData?.materials != null && analysisData.materials.Count > 0)
            {
                // Get the most common shader type from materials
                var shaderPaths = analysisData.materials
                    .Where(m => !string.IsNullOrEmpty(m.currentShader))
                    .Select(m => m.currentShader)
                    .ToList();

                if (shaderPaths.Count > 0)
                {
                    var mostCommonShader = shaderPaths
                        .GroupBy(s => s)
                        .OrderByDescending(g => g.Count())
                        .First().Key;

                    return HoyoToonDataManager.GetShaderKeyFromPath(mostCommonShader);
                }
            }

            return null; // Use global settings
        }

        private void CreateTextureAnalysisOverviewSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            if (textureAnalyses == null || textureAnalyses.Count == 0)
            {
                container.Add(CreateHoyoToonWarningBox("No textures found for analysis"));
                contentView.Add(container);
                return;
            }

            // HoyoToon Texture Analysis Statistics - Prominent section at the top
            var statsContainer = new VisualElement();
            statsContainer.style.marginBottom = 15;
            statsContainer.style.paddingTop = 15;
            statsContainer.style.paddingBottom = 15;
            statsContainer.style.paddingLeft = 20;
            statsContainer.style.paddingRight = 20;
            statsContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            statsContainer.style.borderTopLeftRadius = 8;
            statsContainer.style.borderTopRightRadius = 8;
            statsContainer.style.borderBottomLeftRadius = 8;
            statsContainer.style.borderBottomRightRadius = 8;

            var statsTitle = new Label("HoyoToon Texture Analysis");
            statsTitle.style.fontSize = 16;
            statsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            statsTitle.style.color = new Color(0.8f, 0.9f, 1f);
            statsTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            statsTitle.style.marginBottom = 12;

            var validTextures = textureAnalyses.Where(t => t.IsValid).ToList();
            var compliantCount = validTextures.Where(t => !t.NeedsOptimization).Count();
            var highPriorityCount = validTextures.Where(t => t.OptimizationPriority >= 3).Count();
            var totalSavings = validTextures.Sum(t => ExtractMemorySavingsFromString(t.EstimatedSavings));

            var statsInfo = new Label($"Standards Compliant: {compliantCount}/{validTextures.Count} | " +
                                    $"High Priority Issues: {highPriorityCount}");
            statsInfo.style.fontSize = 13;
            statsInfo.style.color = new Color(0.9f, 0.9f, 0.9f);
            statsInfo.style.unityTextAlign = TextAnchor.MiddleCenter;

            statsContainer.Add(statsTitle);
            statsContainer.Add(statsInfo);
            container.Add(statsContainer);

            // Detailed analysis summary
            var needsOptimization = validTextures.Where(t => t.NeedsOptimization).ToList();
            var compliant = validTextures.Where(t => !t.NeedsOptimization).ToList();

            container.Add(CreateHoyoToonInfoRow("Total Textures:", textureAnalyses.Count.ToString()));
            container.Add(CreateHoyoToonInfoRow("Valid for Analysis:", validTextures.Count.ToString()));
            container.Add(CreateHoyoToonInfoRow("Shader Context:", currentShaderKey ?? "Global (No specific shader detected)"));

            // Compliance status
            container.Add(CreateHoyoToonInfoRow("Compliant with HoyoToon Standards:", compliant.Count.ToString(),
                compliant.Count > 0 ? Color.green : Color.gray));
            container.Add(CreateHoyoToonInfoRow("Need Optimization:", needsOptimization.Count.ToString(),
                needsOptimization.Count > 0 ? Color.yellow : Color.green));

            // Memory optimization potential
            if (needsOptimization.Count > 0)
            {
                if (totalSavings > 0)
                {
                    container.Add(CreateHoyoToonInfoRow("Potential Memory Savings:",
                        $"~{totalSavings}MB", Color.cyan));
                }
            }

            // Show warnings if needed
            if (needsOptimization.Count > 0)
            {
                container.Add(CreateHoyoToonWarningBox($"{needsOptimization.Count} textures don't meet HoyoToon standards and should be optimized"));
            }
            else if (compliant.Count > 0)
            {
                container.Add(CreateHoyoToonSuccessBox("All textures meet HoyoToon optimization standards!"));
            }

            contentView.Add(container);
        }

        private void CreateOptimizationRecommendationsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            var needsOptimization = textureAnalyses.Where(t => t.IsValid && t.NeedsOptimization).ToList();

            if (needsOptimization.Count == 0)
            {
                container.Add(CreateHoyoToonSuccessBox("No optimization recommendations - all textures are properly configured!"));
                contentView.Add(container);
                return;
            }

            // Categorize recommendations
            var compressionIssues = needsOptimization.Where(t => t.Recommendations.Any(r => r.Contains("compression"))).ToList();
            var sizeIssues = needsOptimization.Where(t => t.Recommendations.Any(r => r.Contains("size") || r.Contains("reduce"))).ToList();
            var mipmapIssues = needsOptimization.Where(t => t.Recommendations.Any(r => r.Contains("mipmap"))).ToList();
            var formatIssues = needsOptimization.Where(t => t.Recommendations.Any(r => r.Contains("sRGB") || r.Contains("type"))).ToList();

            if (compressionIssues.Count > 0)
            {
                container.Add(CreateOptimizationCategorySection("Compression Issues", compressionIssues,
                    "These textures should be compressed for better performance and smaller file size."));
            }

            if (sizeIssues.Count > 0)
            {
                var totalSavings = sizeIssues.Sum(t => ExtractMemorySavingsFromString(t.EstimatedSavings));
                container.Add(CreateOptimizationCategorySection("Size Optimization", sizeIssues,
                    $"These textures can be reduced in size. Potential memory savings: ~{totalSavings}MB"));
            }

            if (mipmapIssues.Count > 0)
            {
                container.Add(CreateOptimizationCategorySection("Mipmap Issues", mipmapIssues,
                    "These textures should have mipmaps enabled for better performance at distance."));
            }

            if (formatIssues.Count > 0)
            {
                container.Add(CreateOptimizationCategorySection("Format Issues", formatIssues,
                    "These textures have incorrect format settings for their intended use."));
            }

            contentView.Add(container);
        }

        private VisualElement CreateOptimizationCategorySection(string title, List<HoyoToonTextureAnalysis> textures, string description)
        {
            var categoryContainer = new VisualElement();
            categoryContainer.style.marginBottom = 10;

            var categoryFoldout = CreateHoyoToonFoldout($"{title} ({textures.Count})", false);
            var categoryContent = new VisualElement();
            categoryContent.style.marginLeft = 15;

            // Description
            var descLabel = new Label(description);
            descLabel.style.fontSize = 11;
            descLabel.style.color = Color.cyan;
            descLabel.style.marginBottom = 8;
            categoryContent.Add(descLabel);

            // Action button for this category
            var actionBtn = CreateHoyoToonStyledButton($"Fix All {title}", () =>
            {
                OptimizeSpecificTextures(textures);
            }, new Color(0.2f, 0.6f, 0.8f));
            actionBtn.style.marginBottom = 10;
            categoryContent.Add(actionBtn);

            // List affected textures (show all with pagination in detailed section)
            foreach (var texture in textures.Take(3)) // Show top 3 in category
            {
                var textureRow = new VisualElement();
                textureRow.style.flexDirection = FlexDirection.Row;
                textureRow.style.justifyContent = Justify.SpaceBetween;
                textureRow.style.marginBottom = 3;

                var nameLabel = new Label(texture.TextureName);
                nameLabel.style.fontSize = 11;

                var savingsLabel = new Label(texture.EstimatedSavings);
                savingsLabel.style.fontSize = 10;
                savingsLabel.style.color = Color.green;

                textureRow.Add(nameLabel);
                textureRow.Add(savingsLabel);
                categoryContent.Add(textureRow);
            }

            if (textures.Count > 3)
            {
                var moreLabel = new Label($"... and {textures.Count - 3} more (see detailed view below)");
                moreLabel.style.fontSize = 10;
                moreLabel.style.color = Color.cyan;
                moreLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                categoryContent.Add(moreLabel);
            }

            categoryFoldout.Add(categoryContent);
            categoryContainer.Add(categoryFoldout);
            return categoryContainer;
        }

        private void CreateDetailedAnalysisSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Create controls section
            CreateTextureControlsSection(container);

            // Create texture gallery container
            textureGalleryContainer = new VisualElement();
            container.Add(textureGalleryContainer);

            // Create pagination controls
            CreatePaginationControls(container);

            // Initial population
            RefreshTextureGallery();

            contentView.Add(container);
        }

        private void CreateTextureControlsSection(VisualElement container)
        {
            var controlsContainer = new VisualElement();
            controlsContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            controlsContainer.style.borderTopLeftRadius = 5;
            controlsContainer.style.borderTopRightRadius = 5;
            controlsContainer.style.borderBottomLeftRadius = 5;
            controlsContainer.style.borderBottomRightRadius = 5;
            controlsContainer.style.paddingTop = 10;
            controlsContainer.style.paddingBottom = 10;
            controlsContainer.style.paddingLeft = 10;
            controlsContainer.style.paddingRight = 10;
            controlsContainer.style.marginBottom = 10;

            // First row: Search and filters
            var firstRow = new VisualElement();
            firstRow.style.flexDirection = FlexDirection.Row;
            firstRow.style.marginBottom = 8;

            // Search field
            var searchField = CreateHoyoToonTextField("Search:");
            searchField.style.width = 200;
            searchField.value = searchFilter;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchFilter = evt.newValue;
                currentPage = 0;
                RefreshTextureGallery();
            });
            firstRow.Add(searchField);

            // Show only optimizable toggle
            var optimizableToggle = new Toggle("Show Only Textures Needing Optimization");
            optimizableToggle.style.marginLeft = 15;
            optimizableToggle.value = showOnlyOptimizable;
            optimizableToggle.RegisterValueChangedCallback(evt =>
            {
                showOnlyOptimizable = evt.newValue;
                currentPage = 0;
                RefreshTextureGallery();
            });
            firstRow.Add(optimizableToggle);

            controlsContainer.Add(firstRow);

            // Second row: Items per page and summary
            var secondRow = new VisualElement();
            secondRow.style.flexDirection = FlexDirection.Row;
            secondRow.style.justifyContent = Justify.SpaceBetween;

            // Items per page selector
            var itemsPerPageContainer = new VisualElement();
            itemsPerPageContainer.style.flexDirection = FlexDirection.Row;
            itemsPerPageContainer.style.alignItems = Align.Center;

            var itemsLabel = new Label("Items per page:");
            itemsLabel.style.marginRight = 5;
            itemsPerPageContainer.Add(itemsLabel);

            var itemsDropdown = CreateHoyoToonDropdown(new List<string> { "10", "20", "50", "100" }, 1);
            itemsDropdown.style.width = 60;
            itemsDropdown.RegisterValueChangedCallback(evt =>
            {
                texturesPerPage = int.Parse(evt.newValue);
                currentPage = 0;
                RefreshTextureGallery();
            });
            itemsPerPageContainer.Add(itemsDropdown);

            secondRow.Add(itemsPerPageContainer);

            // Summary info
            var summaryLabel = new Label();
            summaryLabel.style.color = Color.cyan;
            summaryLabel.style.fontSize = 11;
            UpdateSummaryLabel(summaryLabel);
            secondRow.Add(summaryLabel);

            controlsContainer.Add(secondRow);
            container.Add(controlsContainer);
        }

        private void CreatePaginationControls(VisualElement container)
        {
            var paginationContainer = new VisualElement();
            paginationContainer.style.flexDirection = FlexDirection.Row;
            paginationContainer.style.justifyContent = Justify.Center;
            paginationContainer.style.marginTop = 15;
            paginationContainer.style.marginBottom = 10;

            prevPageBtn = CreateHoyoToonStyledButton("Previous", () =>
            {
                if (currentPage > 0)
                {
                    currentPage--;
                    RefreshTextureGallery();
                }
            }, new Color(0.4f, 0.4f, 0.4f));
            prevPageBtn.style.marginRight = 10;
            paginationContainer.Add(prevPageBtn);

            paginationLabel = new Label("Page 1 of 1");
            paginationLabel.style.alignSelf = Align.Center;
            paginationLabel.style.marginLeft = 10;
            paginationLabel.style.marginRight = 10;
            paginationContainer.Add(paginationLabel);

            nextPageBtn = CreateHoyoToonStyledButton("Next", () =>
            {
                var filteredTextures = GetFilteredTextures();
                var totalPages = Mathf.CeilToInt((float)filteredTextures.Count / texturesPerPage);
                if (currentPage < totalPages - 1)
                {
                    currentPage++;
                    RefreshTextureGallery();
                }
            }, new Color(0.4f, 0.4f, 0.4f));
            nextPageBtn.style.marginLeft = 10;
            paginationContainer.Add(nextPageBtn);

            container.Add(paginationContainer);
        }

        private List<HoyoToonTextureAnalysis> GetFilteredTextures()
        {
            var textures = textureAnalyses?.Where(t => t.IsValid) ?? new List<HoyoToonTextureAnalysis>();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                textures = textures.Where(t =>
                    t.TextureName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Apply optimization filter
            if (showOnlyOptimizable)
            {
                textures = textures.Where(t => t.NeedsOptimization);
            }

            return textures.ToList();
        }

        private void RefreshTextureGallery()
        {
            if (textureGalleryContainer == null) return;

            textureGalleryContainer.Clear();

            var filteredTextures = GetFilteredTextures();
            var totalPages = Mathf.CeilToInt((float)filteredTextures.Count / texturesPerPage);

            // Update pagination controls
            if (paginationLabel != null)
            {
                paginationLabel.text = $"Page {currentPage + 1} of {Math.Max(totalPages, 1)}";
            }

            if (prevPageBtn != null)
            {
                prevPageBtn.SetEnabled(currentPage > 0);
            }

            if (nextPageBtn != null)
            {
                nextPageBtn.SetEnabled(currentPage < totalPages - 1);
            }

            // Get textures for current page
            var pageTextures = filteredTextures
                .Skip(currentPage * texturesPerPage)
                .Take(texturesPerPage)
                .ToList();

            if (pageTextures.Count == 0)
            {
                var noTexturesLabel = new Label("No textures found matching the current filters.");
                noTexturesLabel.style.fontSize = 12;
                noTexturesLabel.style.color = Color.gray;
                noTexturesLabel.style.alignSelf = Align.Center;
                noTexturesLabel.style.marginTop = 20;
                textureGalleryContainer.Add(noTexturesLabel);
                return;
            }

            // Create texture grid
            CreateTextureGrid(pageTextures);
        }

        private void CreateTextureGrid(List<HoyoToonTextureAnalysis> textures)
        {
            const int itemsPerRow = 2; // Two columns for better preview display

            for (int i = 0; i < textures.Count; i += itemsPerRow)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 15;

                for (int j = 0; j < itemsPerRow && i + j < textures.Count; j++)
                {
                    var texture = textures[i + j];
                    var textureCard = CreateTextureCard(texture);
                    textureCard.style.flexGrow = 1;
                    textureCard.style.marginRight = j < itemsPerRow - 1 ? 10 : 0;
                    row.Add(textureCard);
                }

                textureGalleryContainer.Add(row);
            }
        }

        private VisualElement CreateTextureCard(HoyoToonTextureAnalysis analysis)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.minHeight = 200;

            // Header with texture name and status
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 8;

            var nameLabel = new Label(analysis.TextureName);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = Color.white;
            header.Add(nameLabel);

            var statusLabel = new Label(GetAnalysisStatusText(analysis));
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = analysis.NeedsOptimization ? Color.yellow : Color.green;
            header.Add(statusLabel);

            card.Add(header);

            // Texture preview
            var preview = CreateTexturePreview(analysis.TexturePath);
            if (preview != null)
            {
                preview.style.alignSelf = Align.Center;
                preview.style.marginBottom = 8;
                card.Add(preview);
            }

            // Texture info
            var infoContainer = new VisualElement();
            infoContainer.style.marginBottom = 8;

            var texture = HoyoToonAssetService.LoadTexture(analysis.TexturePath);
            if (texture != null)
            {
                var sizeLabel = new Label($"Size: {texture.width}x{texture.height}");
                sizeLabel.style.fontSize = 10;
                sizeLabel.style.color = Color.gray;
                infoContainer.Add(sizeLabel);

                var formatLabel = new Label($"Format: {texture.format}");
                formatLabel.style.fontSize = 10;
                formatLabel.style.color = Color.gray;
                infoContainer.Add(formatLabel);

                if (analysis.RecommendedSettings?.MaxTextureSize.HasValue == true)
                {
                    var recSizeLabel = new Label($"Recommended: {analysis.RecommendedSettings.MaxTextureSize}x{analysis.RecommendedSettings.MaxTextureSize}");
                    recSizeLabel.style.fontSize = 10;
                    recSizeLabel.style.color = Color.cyan;
                    infoContainer.Add(recSizeLabel);
                }
            }

            card.Add(infoContainer);

            // Optimization info
            if (analysis.NeedsOptimization)
            {
                var optimizationContainer = new VisualElement();
                optimizationContainer.style.backgroundColor = new Color(0.8f, 0.6f, 0.0f, 0.2f);
                optimizationContainer.style.borderTopLeftRadius = 4;
                optimizationContainer.style.borderTopRightRadius = 4;
                optimizationContainer.style.borderBottomLeftRadius = 4;
                optimizationContainer.style.borderBottomRightRadius = 4;
                optimizationContainer.style.paddingTop = 5;
                optimizationContainer.style.paddingBottom = 5;
                optimizationContainer.style.paddingLeft = 5;
                optimizationContainer.style.paddingRight = 5;
                optimizationContainer.style.marginBottom = 8;

                var optimizationTitle = new Label($"Issues: {analysis.Recommendations.Count}");
                optimizationTitle.style.fontSize = 10;
                optimizationTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                optimizationTitle.style.color = Color.yellow;
                optimizationContainer.Add(optimizationTitle);

                // Show first 2 recommendations
                foreach (var rec in analysis.Recommendations.Take(2))
                {
                    var recLabel = new Label($"• {rec}");
                    recLabel.style.fontSize = 9;
                    recLabel.style.color = Color.white;
                    recLabel.style.whiteSpace = WhiteSpace.Normal;
                    optimizationContainer.Add(recLabel);
                }

                if (analysis.Recommendations.Count > 2)
                {
                    var moreLabel = new Label($"... and {analysis.Recommendations.Count - 2} more");
                    moreLabel.style.fontSize = 9;
                    moreLabel.style.color = Color.gray;
                    optimizationContainer.Add(moreLabel);
                }

                card.Add(optimizationContainer);
            }
            else
            {
                var goodContainer = new VisualElement();
                goodContainer.style.backgroundColor = new Color(0.0f, 0.8f, 0.0f, 0.2f);
                goodContainer.style.borderTopLeftRadius = 4;
                goodContainer.style.borderTopRightRadius = 4;
                goodContainer.style.borderBottomLeftRadius = 4;
                goodContainer.style.borderBottomRightRadius = 4;
                goodContainer.style.paddingTop = 5;
                goodContainer.style.paddingBottom = 5;
                goodContainer.style.paddingLeft = 5;
                goodContainer.style.paddingRight = 5;
                goodContainer.style.marginBottom = 8;

                var goodLabel = new Label("✓ Meets HoyoToon Standards");
                goodLabel.style.fontSize = 10;
                goodLabel.style.color = Color.green;
                goodLabel.style.alignSelf = Align.Center;
                goodContainer.Add(goodLabel);

                card.Add(goodContainer);
            }

            // Action buttons
            var actionsContainer = new VisualElement();
            actionsContainer.style.flexDirection = FlexDirection.Row;
            actionsContainer.style.flexWrap = Wrap.Wrap;

            if (analysis.NeedsOptimization)
            {
                var optimizeBtn = CreateHoyoToonStyledButton("Optimize", () =>
                {
                    OptimizeSpecificTexture(analysis);
                }, new Color(0.2f, 0.6f, 0.8f));
                optimizeBtn.style.marginRight = 5;
                optimizeBtn.style.marginBottom = 3;
                optimizeBtn.style.flexGrow = 1;
                actionsContainer.Add(optimizeBtn);
            }

            var selectBtn = CreateHoyoToonStyledButton("Select", () =>
            {
                SelectTexture(analysis.TexturePath);
            }, new Color(0.4f, 0.4f, 0.4f));
            selectBtn.style.marginRight = 5;
            selectBtn.style.marginBottom = 3;
            selectBtn.style.flexGrow = 1;
            actionsContainer.Add(selectBtn);

            var detailsBtn = CreateHoyoToonStyledButton("Details", () =>
            {
                ShowTextureDetails(analysis);
            }, new Color(0.3f, 0.7f, 0.4f));
            detailsBtn.style.flexGrow = 1;
            detailsBtn.style.marginBottom = 3;
            actionsContainer.Add(detailsBtn);

            card.Add(actionsContainer);

            return card;
        }

        private VisualElement CreateTexturePreview(string texturePath)
        {
            try
            {
                var texture = HoyoToonAssetService.LoadTexture(texturePath);
                if (texture == null) return null;

                var previewContainer = new VisualElement();
                previewContainer.style.width = 80;
                previewContainer.style.height = 80;
                previewContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
                previewContainer.style.borderTopLeftRadius = 4;
                previewContainer.style.borderTopRightRadius = 4;
                previewContainer.style.borderBottomLeftRadius = 4;
                previewContainer.style.borderBottomRightRadius = 4;

                var preview = new VisualElement();
                preview.style.width = 76;
                preview.style.height = 76;
                preview.style.marginTop = 2;
                preview.style.marginLeft = 2;
                preview.style.backgroundImage = new StyleBackground(texture);
                preview.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

                previewContainer.Add(preview);
                return previewContainer;
            }
            catch
            {
                // Return placeholder if preview fails
                var placeholder = new VisualElement();
                placeholder.style.width = 80;
                placeholder.style.height = 80;
                placeholder.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

                var placeholderLabel = new Label("?");
                placeholderLabel.style.alignSelf = Align.Center;
                placeholderLabel.style.fontSize = 24;
                placeholderLabel.style.color = Color.gray;
                placeholder.Add(placeholderLabel);

                return placeholder;
            }
        }

        private void ShowTextureDetails(HoyoToonTextureAnalysis analysis)
        {
            // Create a detailed popup or foldout with all texture information
            var detailWindow = UnityEditor.EditorWindow.GetWindow<TextureDetailWindow>();
            detailWindow.SetTextureAnalysis(analysis);
            detailWindow.Show();
        }

        private void UpdateSummaryLabel(Label label)
        {
            if (textureAnalyses == null || label == null) return;

            var totalTextures = textureAnalyses.Count(t => t.IsValid);
            var needsOptimization = textureAnalyses.Count(t => t.IsValid && t.NeedsOptimization);
            var filteredCount = GetFilteredTextures().Count;

            label.text = $"Showing {filteredCount} of {totalTextures} textures ({needsOptimization} need optimization)";
        }

        // Texture Detail Window class for showing comprehensive texture information
        public class TextureDetailWindow : UnityEditor.EditorWindow
        {
            private HoyoToonTextureAnalysis analysis;

            public void SetTextureAnalysis(HoyoToonTextureAnalysis textureAnalysis)
            {
                analysis = textureAnalysis;
                titleContent = new GUIContent($"Texture Details: {analysis.TextureName}");
            }

            private void OnGUI()
            {
                if (analysis == null) return;

                UnityEditor.EditorGUILayout.LabelField("Texture Analysis Details", UnityEditor.EditorStyles.boldLabel);
                UnityEditor.EditorGUILayout.Space();

                UnityEditor.EditorGUILayout.LabelField("Path:", analysis.TexturePath);
                UnityEditor.EditorGUILayout.LabelField("Status:", analysis.NeedsOptimization ? "Needs Optimization" : "Optimized");

                if (analysis.CurrentSettings != null)
                {
                    UnityEditor.EditorGUILayout.Space();
                    UnityEditor.EditorGUILayout.LabelField("Current Settings:", UnityEditor.EditorStyles.boldLabel);
                    UnityEditor.EditorGUILayout.LabelField("Type:", analysis.CurrentSettings.TextureType);
                    UnityEditor.EditorGUILayout.LabelField("Compression:", analysis.CurrentSettings.TextureCompression);
                    UnityEditor.EditorGUILayout.LabelField("Max Size:", analysis.CurrentSettings.MaxTextureSize.ToString());
                    UnityEditor.EditorGUILayout.LabelField("Mipmaps:", analysis.CurrentSettings.MipmapEnabled.ToString());
                    UnityEditor.EditorGUILayout.LabelField("sRGB:", analysis.CurrentSettings.SRGBTexture.ToString());
                }

                if (analysis.RecommendedSettings != null)
                {
                    UnityEditor.EditorGUILayout.Space();
                    UnityEditor.EditorGUILayout.LabelField("Recommended Settings:", UnityEditor.EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(analysis.RecommendedSettings.TextureType))
                        UnityEditor.EditorGUILayout.LabelField("Type:", analysis.RecommendedSettings.TextureType);
                    if (!string.IsNullOrEmpty(analysis.RecommendedSettings.TextureCompression))
                        UnityEditor.EditorGUILayout.LabelField("Compression:", analysis.RecommendedSettings.TextureCompression);
                    if (analysis.RecommendedSettings.MaxTextureSize.HasValue)
                        UnityEditor.EditorGUILayout.LabelField("Max Size:", analysis.RecommendedSettings.MaxTextureSize.ToString());
                    if (analysis.RecommendedSettings.MipmapEnabled.HasValue)
                        UnityEditor.EditorGUILayout.LabelField("Mipmaps:", analysis.RecommendedSettings.MipmapEnabled.ToString());
                    if (analysis.RecommendedSettings.SRGBTexture.HasValue)
                        UnityEditor.EditorGUILayout.LabelField("sRGB:", analysis.RecommendedSettings.SRGBTexture.ToString());
                }

                if (analysis.Recommendations.Count > 0)
                {
                    UnityEditor.EditorGUILayout.Space();
                    UnityEditor.EditorGUILayout.LabelField("Recommendations:", UnityEditor.EditorStyles.boldLabel);
                    foreach (var rec in analysis.Recommendations)
                    {
                        UnityEditor.EditorGUILayout.LabelField($"• {rec}");
                    }
                }
            }
        }

        private VisualElement CreateSettingsComparisonView(HoyoToonTextureAnalysis analysis)
        {
            var container = new VisualElement();
            container.style.marginTop = 10;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;

            var title = new Label("Settings Comparison:");
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 5;
            container.Add(title);

            var current = analysis.CurrentSettings;
            var recommended = analysis.RecommendedSettings;

            // Compare key settings
            AddSettingComparisonRow(container, "Compression", current.TextureCompression, recommended.TextureCompression);
            AddSettingComparisonRow(container, "Max Size", current.MaxTextureSize.ToString(), recommended.MaxTextureSize?.ToString());
            AddSettingComparisonRow(container, "Mipmaps", current.MipmapEnabled.ToString(), recommended.MipmapEnabled?.ToString());
            AddSettingComparisonRow(container, "sRGB", current.SRGBTexture.ToString(), recommended.SRGBTexture?.ToString());
            AddSettingComparisonRow(container, "Filter Mode", current.FilterMode, recommended.FilterMode);

            return container;
        }

        private void AddSettingComparisonRow(VisualElement container, string settingName, string currentValue, string recommendedValue)
        {
            if (string.IsNullOrEmpty(recommendedValue)) return;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 2;

            var nameLabel = new Label($"{settingName}:");
            nameLabel.style.fontSize = 10;
            nameLabel.style.color = Color.gray;
            nameLabel.style.minWidth = 80;

            var comparisonLabel = new Label($"{currentValue} → {recommendedValue}");
            comparisonLabel.style.fontSize = 10;

            // Color code based on whether change is needed
            bool needsChange = currentValue != recommendedValue;
            comparisonLabel.style.color = needsChange ? Color.yellow : Color.green;

            row.Add(nameLabel);
            row.Add(comparisonLabel);
            container.Add(row);
        }

        #region Helper Methods

        private string GetAnalysisStatusText(HoyoToonTextureAnalysis analysis)
        {
            if (!analysis.IsValid) return "Invalid";
            if (!analysis.NeedsOptimization) return "Compliant";

            string priority = analysis.OptimizationPriority >= 3 ? "High Priority" :
                            analysis.OptimizationPriority >= 2 ? "Medium Priority" : "Low Priority";
            return $"Needs Optimization - {priority}";
        }

        private long ExtractMemorySavingsFromString(string estimatedSavings)
        {
            try
            {
                if (estimatedSavings.Contains("MB"))
                {
                    var mbString = estimatedSavings.Replace("~", "").Replace("MB", "").Trim();
                    if (long.TryParse(mbString, out long mb))
                    {
                        return mb;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private void OptimizeSpecificTexture(HoyoToonTextureAnalysis analysis)
        {
            try
            {
                bool success = HoyoToonTextureManager.OptimizeTextureWithAnalysis(analysis.TexturePath, currentShaderKey);

                if (success)
                {
                    EditorUtility.DisplayDialog("Success",
                        $"Optimized texture: {analysis.TextureName}", "OK");

                    // Refresh analysis
                    UpdateTextureAnalysis();
                    CreateTabContent();
                }
                else
                {
                    EditorUtility.DisplayDialog("Info",
                        $"Texture {analysis.TextureName} did not need optimization", "OK");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to optimize texture: {e.Message}", "OK");
            }
        }

        private void OptimizeSpecificTextures(List<HoyoToonTextureAnalysis> analyses)
        {
            var texturePaths = analyses.Select(a => a.TexturePath).ToList();
            var result = HoyoToonTextureManager.BatchOptimizeTextures(texturePaths, currentShaderKey);

            EditorUtility.DisplayDialog("Batch Optimization Complete", result.Summary, "OK");

            // Refresh analysis
            UpdateTextureAnalysis();
            CreateTabContent();
        }

        private void OptimizeRecommendedTextures()
        {
            if (textureAnalyses == null || textureAnalyses.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No textures to optimize", "OK");
                return;
            }

            var needsOptimization = textureAnalyses.Where(t => t.IsValid && t.NeedsOptimization).ToList();
            if (needsOptimization.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "All textures are already optimized!", "OK");
                return;
            }

            OptimizeSpecificTextures(needsOptimization);
        }

        private void ApplyHoyoToonStandards()
        {
            if (textureAnalyses == null || textureAnalyses.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No textures to process", "OK");
                return;
            }

            var validTextures = textureAnalyses.Where(t => t.IsValid).Select(t => t.TexturePath).ToList();
            var result = HoyoToonTextureManager.BatchOptimizeTextures(validTextures, currentShaderKey);

            EditorUtility.DisplayDialog("HoyoToon Standards Applied", result.Summary, "OK");

            // Refresh analysis
            UpdateTextureAnalysis();
            CreateTabContent();
        }

        private void ValidateAllTextures()
        {
            if (textureAnalyses == null || textureAnalyses.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No textures to validate", "OK");
                return;
            }

            var validTextures = textureAnalyses.Where(t => t.IsValid).ToList();
            var compliantCount = validTextures.Where(t => !t.NeedsOptimization).Count();
            var nonCompliantCount = validTextures.Count - compliantCount;

            string message = $"Validation Complete!\n\n" +
                           $"Total Textures: {validTextures.Count}\n" +
                           $"Compliant: {compliantCount}\n" +
                           $"Need Optimization: {nonCompliantCount}\n" +
                           $"Shader Context: {currentShaderKey ?? "Global"}";

            EditorUtility.DisplayDialog("Texture Validation", message, "OK");
        }

        private void ValidateSpecificTexture(HoyoToonTextureAnalysis analysis)
        {
            var result = HoyoToonTextureManager.ValidateTextureForShader(analysis.TexturePath, currentShaderKey ?? "Global");
            EditorUtility.DisplayDialog("Texture Validation", result.StatusMessage, "OK");
        }

        private void SelectTexture(string texturePath)
        {
            var texture = HoyoToonAssetService.LoadTexture(texturePath);
            if (texture != null)
            {
                Selection.activeObject = texture;
                EditorGUIUtility.PingObject(texture);
            }
        }

        #endregion
    }
}