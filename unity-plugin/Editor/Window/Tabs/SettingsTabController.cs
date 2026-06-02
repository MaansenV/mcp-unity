using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Services;
using UnityMCP.Editor.Settings;

namespace UnityMCP.Editor.Window
{
    internal class SettingsTabController : TabController
    {
        private readonly McpSettings _settings;

        public SettingsTabController(McpSettings settings)
        {
            _settings = settings;
        }

        public override void Build(VisualElement container)
        {
            container.Add(BuildWebSocketCard());
            container.Add(BuildServerCard());
            container.Add(BuildAutomationCard());
            container.Add(BuildConfigExportCard());
            container.Add(BuildActionsRow());
        }

        // --- WebSocket ---

        private VisualElement BuildWebSocketCard()
        {
            var card = Card("WebSocket", "Connection endpoint configuration");

            card.Add(BuildFieldRow("Host", _settings.Host, v => _settings.SetHost(v)));
            card.Add(BuildFieldRow("Port", _settings.Port.ToString(),
                v => { if (int.TryParse(v, out var n)) _settings.SetPort(n); }, mono: true));
            card.Add(BuildFieldRow("Path", _settings.Path, v => _settings.SetPath(v)));

            var urlPreview = new Label($"URL: {_settings.WebSocketUrl}");
            urlPreview.AddToClassList(WindowStyles.CodeBlock);
            card.Add(urlPreview);

            return card;
        }

        // --- Server ---

        private VisualElement BuildServerCard()
        {
            var card = Card("Server", "Server binary and paths");

            card.Add(BuildFieldRow("Server root", _settings.ServerRootPath, v => _settings.SetServerRootPath(v)));
            card.Add(BuildFieldRow("Build output", _settings.ServerBuildOutputPath, v => _settings.SetServerBuildOutputPath(v)));
            card.Add(BuildToggleRow("Auto build if missing", _settings.AutoBuildIfMissing, v => _settings.SetAutoBuildIfMissing(v)));
            card.Add(BuildToggleRow("Auto start server", _settings.AutoStartServer, v => _settings.SetAutoStartServer(v)));

            return card;
        }

        // --- Automation ---

        private VisualElement BuildAutomationCard()
        {
            var card = Card("Automation", "Automatic behaviors");

            card.Add(BuildToggleRow("Auto connect", _settings.AutoConnect, v => _settings.SetAutoConnect(v)));
            card.Add(BuildToggleRow("Auto reconnect", _settings.AutoReconnect, v => _settings.SetAutoReconnect(v)));

            return card;
        }

        // --- Config Export ---

        private VisualElement BuildConfigExportCard()
        {
            var card = Card("Config Export", "OpenCode integration configuration");

            card.Add(BuildFieldRow("opencode.json path", _settings.OpencodeConfigPath, v => _settings.SetOpencodeConfigPath(v)));

            var exportButton = new Button(ExportConfig) { text = "Export opencode.json" };
            card.Add(exportButton);

            return card;
        }

        // --- Actions ---

        private VisualElement BuildActionsRow()
        {
            var row = ActionsRow();

            var saveButton = new Button(Save) { text = "Save Settings" };
            saveButton.AddToClassList(WindowStyles.BtnPrimary);
            row.Add(saveButton);

            row.Add(new Button(ResetDefaults) { text = "Reset Defaults" });

            return row;
        }

        // --- Row builders ---

        private static VisualElement BuildFieldRow(string label, string value, System.Action<string> onChange, bool mono = false)
        {
            var row = new VisualElement();
            row.AddToClassList(WindowStyles.KvRow);

            var labelEl = new Label(label);
            labelEl.AddToClassList(WindowStyles.KvLabel);
            row.Add(labelEl);

            var field = new TextField { value = value };
            field.style.minWidth = 200;
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            if (mono) field.AddToClassList(WindowStyles.KvValueMono);
            field.RegisterValueChangedCallback(e => onChange?.Invoke(e.newValue));
            row.Add(field);

            return row;
        }

        private static VisualElement BuildToggleRow(string label, bool value, System.Action<bool> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList(WindowStyles.KvRow);

            var labelEl = new Label(label);
            labelEl.AddToClassList(WindowStyles.KvLabel);
            row.Add(labelEl);

            var toggle = new Toggle { value = value };
            toggle.style.flexGrow = 1;
            toggle.RegisterValueChangedCallback(e => onChange?.Invoke(e.newValue));
            row.Add(toggle);

            return row;
        }

        // --- Actions ---

        private void Save() => _settings.Save();

        private async void ExportConfig()
        {
            var result = await OpencodeConfigService.ExportAsync(_settings);
            if (result.Success)
                Debug.Log($"[UnityMCP] Config exported to: {result.Path}");
            else
                Debug.LogError($"[UnityMCP] Export failed: {result.Error}");
        }

        private void ResetDefaults()
        {
            if (!EditorUtility.DisplayDialog("Reset Settings", "Reset all settings to defaults?", "Reset", "Cancel"))
                return;

            _settings.SetHost("127.0.0.1");
            _settings.SetPort(8081);
            _settings.SetPath("/ws");
            _settings.SetAutoStartServer(true);
            _settings.SetAutoConnect(true);
            _settings.SetAutoReconnect(true);
            _settings.SetAutoBuildIfMissing(true);
            _settings.SetConnectTimeoutSeconds(10f);
            _settings.SetReconnectDelaySeconds(2f);
            _settings.SetHeartbeatIntervalSeconds(15f);
            _settings.SetMaxLogEntries(500);
            _settings.Save();

            Debug.Log("[UnityMCP] Settings reset to defaults");
        }
    }
}
