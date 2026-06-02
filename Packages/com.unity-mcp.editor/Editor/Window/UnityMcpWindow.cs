using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Core;
using UnityMCP.Editor.Logging;
using UnityMCP.Editor.Settings;

namespace UnityMCP.Editor.Window
{
    public class UnityMcpWindow : EditorWindow
    {
        // --- Tab labels (order matches _tabs array) ---
        private static readonly string[] TabLabels =
        {
            "Status",
            "Setup",
            "Tools",
            "Settings",
        };

        // --- Serialized state (persisted across domain reloads) ---
        [SerializeField] private McpRuntimeState _runtimeState;
        [SerializeField] private int _selectedTabIndex;

        // --- Services ---
        private McpSettings _settings;
        private McpLogBuffer _logBuffer;

        // --- UI references ---
        private TabController[] _tabs;
        private Button[] _tabButtons;
        private Label _connectionStatusText;
        private Label _connectionPill;
        private Label _clientCountLabel;

        // --- Repaint throttling ---
        private double _lastRepaintTime;
        private const double RepaintIntervalSeconds = 1.0;

        [MenuItem("Window/Unity MCP %#m")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnityMcpWindow>();
            window.titleContent = new GUIContent("Unity MCP", EditorGUIUtility.IconContent("d_UnityEditor.Consoles").image);
            window.minSize = new Vector2(700, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = McpSettingsLocator.GetOrCreateSettings();
            // Use the shared plugin log buffer instead of creating a separate one.
            _logBuffer = UnityMcpPlugin.LogBuffer;
            if (_logBuffer != null)
                _logBuffer.SetMaxEntries(_settings.MaxLogEntries);

            if (_runtimeState == null)
                _runtimeState = new McpRuntimeState();

            _runtimeState.Changed += ScheduleRepaint;
            UnityMcpPlugin.ConnectionStateChanged += OnConnectionStateChanged;
            UnityMcpPlugin.McpClientSeen += OnMcpClientSeen;

            _tabs = new TabController[]
            {
                new ConnectionTabController(_settings, _runtimeState),
                new SetupTabController(_settings, _logBuffer, _runtimeState),
                new ToolsTabController(_runtimeState),
                new SettingsTabController(_settings),
            };

            EditorApplication.update += OnEditorUpdate;

            EditorApplication.delayCall += ConnectWebSocket;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (_runtimeState != null)
                _runtimeState.Changed -= ScheduleRepaint;
            UnityMcpPlugin.ConnectionStateChanged -= OnConnectionStateChanged;
            UnityMcpPlugin.McpClientSeen -= OnMcpClientSeen;
        }

        private void OnEditorUpdate()
        {
            // Throttled repaint for live status (clients, uptime).
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastRepaintTime < RepaintIntervalSeconds) return;
            _lastRepaintTime = now;
            RefreshHeader();
            RefreshCurrentTabState();
        }

        private void ScheduleRepaint()
        {
            RefreshHeader();
            RefreshCurrentTabState();
            Repaint();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(WindowStyles.Root);

            // Apply the wireframe USS if it ships in Resources.
            var styleSheet = Resources.Load<StyleSheet>("UnityMCP/UnityMcpWindow");
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            BuildHeader(root);
            BuildTabBar(root);
            BuildContent(root);

            SelectTab(_selectedTabIndex);
        }

        // --- Layout ---

        private void BuildHeader(VisualElement root)
        {
            var header = new VisualElement();
            header.AddToClassList(WindowStyles.Header);

            var title = new Label("Unity MCP");
            title.AddToClassList(WindowStyles.HeaderTitle);
            header.Add(title);

            var statusGroup = new VisualElement();
            statusGroup.AddToClassList(WindowStyles.HeaderStatus);

            _clientCountLabel = new Label();
            _clientCountLabel.AddToClassList(WindowStyles.StatusPill);
            _clientCountLabel.AddToClassList(WindowStyles.StatusDisconnected);
            statusGroup.Add(_clientCountLabel);

            _connectionPill = new Label();
            _connectionPill.AddToClassList(WindowStyles.StatusPill);
            _connectionPill.AddToClassList(WindowStyles.StatusDisconnected);
            _connectionStatusText = new Label("Disconnected");
            _connectionStatusText.AddToClassList(WindowStyles.StatusText);
            _connectionPill.Add(_connectionStatusText);
            statusGroup.Add(_connectionPill);

            header.Add(statusGroup);
            root.Add(header);
        }

        private void BuildTabBar(VisualElement root)
        {
            var tabs = new VisualElement();
            tabs.AddToClassList(WindowStyles.Tabs);

            _tabButtons = new Button[TabLabels.Length];
            for (var i = 0; i < TabLabels.Length; i++)
            {
                var index = i;
                var button = new Button(() => SelectTab(index)) { text = TabLabels[i] };
                button.AddToClassList(WindowStyles.Tab);
                if (i == _selectedTabIndex) button.AddToClassList(WindowStyles.TabActive);
                _tabButtons[i] = button;
                tabs.Add(button);
            }
            root.Add(tabs);
        }

        private void BuildContent(VisualElement root)
        {
            var content = new ScrollView(ScrollViewMode.Vertical) { name = "tab-content" };
            content.AddToClassList(WindowStyles.Content);
            root.Add(content);
        }

        // --- Tab navigation ---

        private void SelectTab(int index)
        {
            if (_tabs == null || _tabs.Length == 0) return;

            _selectedTabIndex = Mathf.Clamp(index, 0, _tabs.Length - 1);

            var content = rootVisualElement.Q<VisualElement>("tab-content");
            if (content == null) return;
            content.Clear();

            UpdateTabButtonStates();
            _tabs[_selectedTabIndex]?.Build(content);
            RefreshHeader();
        }

        private void UpdateTabButtonStates()
        {
            if (_tabButtons == null) return;
            for (var i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                if (i == _selectedTabIndex)
                    _tabButtons[i].AddToClassList(WindowStyles.TabActive);
                else
                    _tabButtons[i].RemoveFromClassList(WindowStyles.TabActive);
            }
        }

        // --- Header updates ---

        private void RefreshHeader()
        {
            if (_runtimeState == null) return;
            UpdateConnectionPill(_runtimeState.ConnectionState);
            UpdateClientCount(_runtimeState.ClientCount);
        }

        private void RefreshCurrentTabState()
        {
            if (_tabs == null || _selectedTabIndex < 0 || _selectedTabIndex >= _tabs.Length) return;
            if (_tabs[_selectedTabIndex] is ConnectionTabController connectionTab)
                connectionTab.RefreshState();
        }

        private void UpdateConnectionPill(ConnectionState state)
        {
            if (_connectionPill == null || _connectionStatusText == null) return;

            // Update text
            _connectionStatusText.text = state switch
            {
                ConnectionState.Connected   => "Connected",
                ConnectionState.Connecting  => "Connecting",
                ConnectionState.Compiling   => "Compiling",
                ConnectionState.Error       => "Error",
                _                           => "Disconnected",
            };

            // Update pill color class
            SetStatusClass(_connectionPill, state);
        }

        private void UpdateClientCount(int count)
        {
            if (_clientCountLabel == null) return;
            _clientCountLabel.text = count switch
            {
                0 => "No clients",
                1 => "1 client",
                _ => $"{count} clients",
            };
        }

        private static void SetStatusClass(VisualElement element, ConnectionState state)
        {
            element.RemoveFromClassList(WindowStyles.StatusConnected);
            element.RemoveFromClassList(WindowStyles.StatusConnecting);
            element.RemoveFromClassList(WindowStyles.StatusError);
            element.RemoveFromClassList(WindowStyles.StatusDisconnected);

            var className = state switch
            {
                ConnectionState.Connected  => WindowStyles.StatusConnected,
                ConnectionState.Connecting => WindowStyles.StatusConnecting,
                ConnectionState.Compiling  => WindowStyles.StatusConnecting,
                ConnectionState.Error      => WindowStyles.StatusError,
                _                          => WindowStyles.StatusDisconnected,
            };
            element.AddToClassList(className);
        }

        // --- Connection bootstrap ---

        private void ConnectWebSocket()
        {
            if (_runtimeState == null || _logBuffer == null || _settings == null)
                return;
            UnityMcpPlugin.Connect();
        }

        private void OnConnectionStateChanged(ConnectionState state)
        {
            _runtimeState.UpdateFromPlugin(state, false, null);
            if (state == ConnectionState.Connected)
            {
                _runtimeState.AddClient(new ConnectedClientInfo(
                    "unity-mcp-bridge",
                    "Unity MCP bridge",
                    _settings?.WebSocketUrl ?? "WebSocket"));
            }
            else if (state == ConnectionState.Disconnected || state == ConnectionState.Error)
            {
                _runtimeState.ClearClients();
            }
        }

        private void OnMcpClientSeen(ConnectedClientInfo client)
        {
            if (_runtimeState == null || client == null) return;

            var existing = _runtimeState.GetClient(client.Id);
            if (existing == null)
            {
                _runtimeState.AddClient(client);
                return;
            }

            existing.Name = client.Name;
            existing.RemoteAddress = client.RemoteAddress;
            existing.LastSeen = System.DateTime.Now;
            _runtimeState.NotifyChanged();
        }
    }
}
