using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnityMCP.Editor.Core;
using UnityMCP.Shared;

namespace UnityMCP.Editor.Window
{
    internal class ToolsTabController : TabController
    {
        private readonly McpRuntimeState _runtimeState;

        private TextField _searchField;
        private DropdownField _categoryFilter;
        private ListView _toolList;
        private Label _countLabel;
        private TextField _argsField;
        private Label _resultLabel;

        private readonly List<ToolInfo> _allTools;

        private List<ToolInfo> _filteredTools;

        public ToolsTabController(McpRuntimeState runtimeState)
        {
            _runtimeState = runtimeState;
            _allTools = LoadToolsFromRegistry();
            _filteredTools = new List<ToolInfo>(_allTools);
        }

        public override void Build(VisualElement container)
        {
            container.Add(BuildToolListCard());
            container.Add(BuildToolTestCard());
            UpdateCount();
        }

        // --- Tool list card ---

        private VisualElement BuildToolListCard()
        {
            var card = Card("Available Tools", "Tools exposed via the MCP server");

            // Filter row
            var filter = new VisualElement();
            filter.AddToClassList(WindowStyles.FilterBar);

            _searchField = new TextField { value = "" };
            _searchField.AddToClassList(WindowStyles.Search);
            _searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            filter.Add(_searchField);

            var categories = new List<string> { "All" };
            categories.AddRange(_allTools.Select(t => t.Category).Distinct().OrderBy(c => c));
            _categoryFilter = new DropdownField(categories, 0);
            _categoryFilter.style.width = 96;
            _categoryFilter.RegisterValueChangedCallback(_ => ApplyFilter());
            filter.Add(_categoryFilter);

            card.Add(filter);

            // Count
            _countLabel = new Label();
            _countLabel.AddToClassList(WindowStyles.KvLabel);
            card.Add(_countLabel);

            // List
            _toolList = new ListView(
                _filteredTools,
                itemHeight: 36,
                makeItem: () =>
                {
                    var row = new VisualElement();
                    row.AddToClassList(WindowStyles.ListRow);
                    return row;
                },
                bindItem: (element, index) => BindToolRow(element, index)
            );
            _toolList.selectionType = SelectionType.Single;
            _toolList.AddToClassList(WindowStyles.List);
            card.Add(_toolList);

            return card;
        }

        private void BindToolRow(VisualElement element, int index)
        {
            if (index >= _filteredTools.Count) return;
            element.Clear();

            var tool = _filteredTools[index];
            var title = new Label(tool.Name);
            title.AddToClassList(WindowStyles.ListRowTitle);
            element.Add(title);

            var meta = new Label($"{tool.Category} — {tool.Description}");
            meta.AddToClassList(WindowStyles.ListRowMeta);
            element.Add(meta);
        }

        private void ApplyFilter()
        {
            var search = (_searchField?.value ?? "").Trim().ToLowerInvariant();
            var category = _categoryFilter?.value ?? "All";

            _filteredTools = _allTools
                .Where(t => category == "All" || t.Category == category)
                .Where(t => string.IsNullOrEmpty(search) ||
                            t.Name.ToLowerInvariant().Contains(search) ||
                            t.Description.ToLowerInvariant().Contains(search))
                .ToList();

            _toolList.itemsSource = _filteredTools;
            _toolList.RefreshItems();
            UpdateCount();
        }

        private void UpdateCount()
        {
            if (_countLabel == null) return;
            _countLabel.text = _filteredTools.Count == _allTools.Count
                ? $"{_allTools.Count} tools"
                : $"{_filteredTools.Count} of {_allTools.Count} tools";
        }

        // --- Tool test card ---

        private VisualElement BuildToolTestCard()
        {
            var card = Card("Tool Test", "Run a tool with custom arguments");

            _argsField = new TextField { multiline = true, value = "{}" };
            _argsField.style.minHeight = 60;
            _argsField.style.marginBottom = 10;
            card.Add(_argsField);

            var runButton = new Button(RunToolTest) { text = "Run Test" };
            runButton.AddToClassList(WindowStyles.BtnPrimary);
            card.Add(runButton);

            _resultLabel = new Label("Result will appear here");
            _resultLabel.AddToClassList(WindowStyles.CodeBlock);
            card.Add(_resultLabel);

            return card;
        }

        private void RunToolTest()
        {
            _resultLabel.text = "Executing tool...";
            _resultLabel.text = "<color=#4caf50>Test completed (placeholder)</color>";
        }

        private static List<ToolInfo> LoadToolsFromRegistry()
        {
            var manifest = UnityMcpPlugin.Registry?.BuildManifest();
            if (manifest?.Tools == null || manifest.Tools.Count == 0)
            {
                return new List<ToolInfo>();
            }

            return manifest.Tools
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => new ToolInfo(CategoryFromName(t.Name), t.Name, t.Description))
                .ToList();
        }

        private static string CategoryFromName(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return "Other";

            var parts = toolName.Split('.');
            if (parts.Length < 2) return "Other";

            return parts[1].ToLowerInvariant() switch
            {
                "asset" => "Assets",
                "gameobject" => "GameObject",
                "material" => "Material",
                "prefab" => "Prefab",
                "profiler" => "Profiler",
                "scene" => "Scene",
                "script" => "Script",
                "shader" => "Shader",
                "console" => "Console",
                "editor" => "Editor",
                _ => char.ToUpperInvariant(parts[1][0]) + parts[1].Substring(1)
            };
        }

        // --- Data type ---

        private sealed class ToolInfo
        {
            public string Category { get; }
            public string Name { get; }
            public string Description { get; }

            public ToolInfo(string category, string name, string description)
            {
                Category = category;
                Name = name;
                Description = description;
            }
        }
    }
}
