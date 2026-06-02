using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Logging;

namespace UnityMCP.Editor.Window
{
    internal class LogsTabController : TabController
    {
        private readonly McpLogBuffer _logBuffer;

        private TextField _searchField;
        private ListView _logList;
        private Label _countLabel;
        private Toggle _autoScrollToggle;
        private readonly List<Button> _categoryChips = new();
        private readonly Dictionary<McpLogCategory, bool> _enabledCategories = new();

        private List<McpLogEntry> _filteredEntries = new();

        public LogsTabController(McpLogBuffer logBuffer)
        {
            _logBuffer = logBuffer;

            // Enable all categories by default
            foreach (McpLogCategory category in System.Enum.GetValues(typeof(McpLogCategory)))
                _enabledCategories[category] = true;
        }

        public override void Build(VisualElement container)
        {
            if (_logBuffer != null)
                _logBuffer.EntryAdded -= OnNewLogEntry;

            container.Add(BuildLogsCard());
            RefreshLogs();
        }

        // --- Logs card ---

        private VisualElement BuildLogsCard()
        {
            var card = Card("Logs", "Filtered log output from the MCP plugin");

            // Category filter chips
            var chipBar = new VisualElement();
            chipBar.AddToClassList(WindowStyles.FilterBar);
            foreach (McpLogCategory category in System.Enum.GetValues(typeof(McpLogCategory)))
            {
                var chip = CreateCategoryChip(category);
                _categoryChips.Add(chip);
                chipBar.Add(chip);
            }
            card.Add(chipBar);

            // Search + auto-scroll
            var searchBar = new VisualElement();
            searchBar.AddToClassList(WindowStyles.FilterBar);

            _searchField = new TextField { value = "" };
            _searchField.AddToClassList(WindowStyles.Search);
            _searchField.RegisterValueChangedCallback(_ => RefreshLogs());
            searchBar.Add(_searchField);

            _autoScrollToggle = new Toggle("Auto-scroll") { value = true };
            searchBar.Add(_autoScrollToggle);

            var clearButton = new Button(_logBuffer.Clear) { text = "Clear" };
            searchBar.Add(clearButton);

            var exportButton = new Button(ExportLogs) { text = "Export" };
            searchBar.Add(exportButton);

            card.Add(searchBar);

            // Count
            _countLabel = new Label();
            _countLabel.AddToClassList(WindowStyles.KvLabel);
            card.Add(_countLabel);

            // List
            _logList = new ListView(
                _filteredEntries,
                itemHeight: 22,
                makeItem: () => new Label(),
                bindItem: (element, index) => BindLogEntry((Label)element, index)
            );
            _logList.AddToClassList(WindowStyles.List);
            _logList.selectionType = SelectionType.None;
            card.Add(_logList);

            // Subscribe to new entries
            _logBuffer.EntryAdded += OnNewLogEntry;

            return card;
        }

        private Button CreateCategoryChip(McpLogCategory category)
        {
            var chip = new Button(() => ToggleCategory(category)) { text = category.ToString() };
            chip.AddToClassList(WindowStyles.Chip);
            chip.AddToClassList(WindowStyles.ChipActive);
            return chip;
        }

        private void ToggleCategory(McpLogCategory category)
        {
            _enabledCategories[category] = !_enabledCategories[category];

            // Update chip visual
            var index = (int)category;
            if (index >= 0 && index < _categoryChips.Count)
            {
                var chip = _categoryChips[index];
                if (_enabledCategories[category])
                    chip.AddToClassList(WindowStyles.ChipActive);
                else
                    chip.RemoveFromClassList(WindowStyles.ChipActive);
            }

            RefreshLogs();
        }

        private void OnNewLogEntry(McpLogEntry entry)
        {
            if (ShouldShowEntry(entry))
            {
                _filteredEntries.Add(entry);
                _logList.RefreshItems();
                UpdateCount();
                if (_autoScrollToggle?.value == true)
                    _logList.ScrollToItem(_filteredEntries.Count - 1);
            }
        }

        private void RefreshLogs()
        {
            if (_logBuffer == null || _logList == null) return;

            _filteredEntries = _logBuffer.Entries.Where(ShouldShowEntry).ToList();
            _logList.itemsSource = _filteredEntries;
            _logList.Rebuild();
            UpdateCount();
        }

        private void UpdateCount()
        {
            if (_countLabel == null) return;
            _countLabel.text = $"{_filteredEntries.Count} of {_logBuffer.Count} entries";
        }

        private bool ShouldShowEntry(McpLogEntry entry)
        {
            if (_enabledCategories.TryGetValue(entry.Category, out var enabled) && !enabled)
                return false;

            var search = _searchField?.value;
            if (!string.IsNullOrEmpty(search) &&
                !entry.Message.Contains(search, System.StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private void BindLogEntry(Label label, int index)
        {
            if (index >= _filteredEntries.Count) return;
            var entry = _filteredEntries[index];

            // Plain text — coloring is handled by USS classes
            label.text = $"{entry.Timestamp:HH:mm:ss}  [{entry.Category}]  {entry.Message}";
            label.enableRichText = false;

            // Apply level class for color
            label.RemoveFromClassList(WindowStyles.LogInfo);
            label.RemoveFromClassList(WindowStyles.LogWarn);
            label.RemoveFromClassList(WindowStyles.LogError);
            label.RemoveFromClassList(WindowStyles.LogDebug);

            var levelClass = entry.Level switch
            {
                McpLogLevel.Warning => WindowStyles.LogWarn,
                McpLogLevel.Error   => WindowStyles.LogError,
                McpLogLevel.Debug   => WindowStyles.LogDebug,
                _                   => WindowStyles.LogInfo,
            };
            label.AddToClassList(levelClass);
            label.AddToClassList(WindowStyles.LogEntry);
        }

        private void ExportLogs()
        {
            var content = _logBuffer.ExportAll();
            var path = EditorUtility.SaveFilePanel("Export Logs", "", "unity-mcp-logs.txt", "txt");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, content);
                Debug.Log($"[UnityMCP] Logs exported to: {path}");
            }
        }
    }
}
