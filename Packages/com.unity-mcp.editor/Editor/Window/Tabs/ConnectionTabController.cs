using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Core;
using UnityMCP.Editor.Settings;

namespace UnityMCP.Editor.Window
{
    internal class ConnectionTabController : TabController
    {
        private readonly McpSettings _settings;
        private readonly McpRuntimeState _runtimeState;

        // UI refs that need to update on repaint
        private Label _heroStatusText;
        private VisualElement _heroStatusDot;
        private Label _uptimeValue;
        private Label _lastConnectedValue;
        private VisualElement _clientsContainer;

        public ConnectionTabController(McpSettings settings, McpRuntimeState runtimeState)
        {
            _settings = settings;
            _runtimeState = runtimeState;
        }

        public override void Build(VisualElement container)
        {
            container.Add(BuildHeroCard());
            container.Add(BuildClientsCard());
            container.Add(BuildOptionsCard());
            RefreshState();
        }

        /// <summary>
        /// Repaints time-sensitive elements (uptime, last connected, client list).
        /// </summary>
        public void RefreshState()
        {
            if (_runtimeState == null) return;

            if (_heroStatusText != null)
                _heroStatusText.text = _runtimeState.ConnectionState.ToString();

            if (_heroStatusDot != null)
                SetHeroDotClass(_runtimeState.ConnectionState);

            if (_uptimeValue != null)
                _uptimeValue.text = CalculateUptime();

            if (_lastConnectedValue != null)
                _lastConnectedValue.text = _runtimeState.LastConnectedAt?.ToString("HH:mm:ss") ?? "Never";

            RefreshClients();
        }

        // --- Hero card (prominent status) ---

        private VisualElement BuildHeroCard()
        {
            var card = Card("WebSocket");
            card.AddToClassList(WindowStyles.StatusCard);

            // Hero status row
            var hero = new VisualElement();
            hero.AddToClassList(WindowStyles.HeroStatus);

            _heroStatusDot = new VisualElement();
            _heroStatusDot.AddToClassList(WindowStyles.HeroStatusDot);
            hero.Add(_heroStatusDot);

            _heroStatusText = new Label();
            _heroStatusText.AddToClassList(WindowStyles.HeroStatusText);
            hero.Add(_heroStatusText);

            card.Add(hero);

            // Connection details
            card.Add(KvRow("URL", _settings.WebSocketUrl, mono: true));
            card.Add(KvRow("Host", _settings.Host, mono: true));
            card.Add(KvRow("Port", _settings.Port.ToString(), mono: true));
            card.Add(BuildUptimeRow());
            card.Add(BuildLastConnectedRow());

            // Actions
            var actions = ActionsRow();
            actions.Add(new Button(Reconnect) { text = "Reconnect" });
            actions.Add(new Button(Disconnect) { text = "Disconnect" });
            card.Add(actions);

            return card;
        }

        private VisualElement BuildUptimeRow()
        {
            var row = new VisualElement();
            row.AddToClassList(WindowStyles.KvRow);

            var label = new Label("Uptime");
            label.AddToClassList(WindowStyles.KvLabel);
            row.Add(label);

            _uptimeValue = new Label("--");
            _uptimeValue.AddToClassList(WindowStyles.KvValue);
            _uptimeValue.AddToClassList(WindowStyles.KvValueSuccess);
            row.Add(_uptimeValue);

            return row;
        }

        private VisualElement BuildLastConnectedRow()
        {
            var row = new VisualElement();
            row.AddToClassList(WindowStyles.KvRow);

            var label = new Label("Last connected");
            label.AddToClassList(WindowStyles.KvLabel);
            row.Add(label);

            _lastConnectedValue = new Label("Never");
            _lastConnectedValue.AddToClassList(WindowStyles.KvValue);
            _lastConnectedValue.AddToClassList(WindowStyles.KvValueMono);
            row.Add(_lastConnectedValue);

            return row;
        }

        // --- Clients card ---

        private VisualElement BuildClientsCard()
        {
            var card = Card("Connected Clients", "Unity MCP bridge and active MCP/stdio clients");

            _clientsContainer = new VisualElement();
            card.Add(_clientsContainer);

            var actions = ActionsRow();
            actions.Add(new Button(RefreshClients) { text = "Refresh" });
            actions.Add(new Button(DisconnectAll) { text = "Disconnect all" });
            card.Add(actions);

            return card;
        }

        private void RefreshClients()
        {
            if (_clientsContainer == null || _runtimeState == null) return;

            _clientsContainer.Clear();

            var clients = _runtimeState.ConnectedClients;
            if (clients == null || clients.Count == 0)
            {
                var empty = new VisualElement();
                empty.AddToClassList(WindowStyles.Empty);

                var emptyText = new Label("No clients connected");
                emptyText.AddToClassList(WindowStyles.EmptyText);
                empty.Add(emptyText);

                _clientsContainer.Add(empty);
                return;
            }

            foreach (var client in clients)
            {
                _clientsContainer.Add(BuildClientRow(client));
            }
        }

        private static VisualElement BuildClientRow(ConnectedClientInfo client)
        {
            var row = new VisualElement();
            row.AddToClassList(WindowStyles.Client);

            // Status dot (green = connected)
            var dot = new VisualElement();
            dot.AddToClassList(WindowStyles.StatusDot);
            dot.style.backgroundColor = new Color(0.298f, 0.686f, 0.314f);
            row.Add(dot);

            // Client info
            var info = new VisualElement();
            info.AddToClassList(WindowStyles.ClientInfo);

            var name = new Label(client.Name);
            name.AddToClassList(WindowStyles.ClientName);
            info.Add(name);

            var address = new Label(client.RemoteAddress);
            address.AddToClassList(WindowStyles.ClientAddress);
            info.Add(address);

            row.Add(info);

            // Uptime
            var meta = new Label(client.FormatUptime());
            meta.AddToClassList(WindowStyles.ClientMeta);
            row.Add(meta);

            return row;
        }

        // --- Options card ---

        private VisualElement BuildOptionsCard()
        {
            var card = Card("Options");

            // Auto-reconnect toggle row
            var toggleRow = new VisualElement();
            toggleRow.AddToClassList(WindowStyles.KvRow);

            var toggleLabel = new Label("Auto reconnect");
            toggleLabel.AddToClassList(WindowStyles.KvLabel);
            toggleRow.Add(toggleLabel);

            var toggle = new Toggle { value = _settings.AutoReconnect };
            toggle.RegisterValueChangedCallback(e =>
            {
                _settings.SetAutoReconnect(e.newValue);
                _settings.Save();
            });
            toggleRow.Add(toggle);

            card.Add(toggleRow);

            card.Add(KvRow("Project path", Application.dataPath, mono: true));

            return card;
        }

        // --- Helpers ---

        private void SetHeroDotClass(ConnectionState state)
        {
            if (_heroStatusDot == null) return;

            _heroStatusDot.RemoveFromClassList(WindowStyles.StatusConnected);
            _heroStatusDot.RemoveFromClassList(WindowStyles.StatusConnecting);
            _heroStatusDot.RemoveFromClassList(WindowStyles.StatusError);
            _heroStatusDot.RemoveFromClassList(WindowStyles.StatusDisconnected);

            var className = state switch
            {
                ConnectionState.Connected  => WindowStyles.StatusConnected,
                ConnectionState.Connecting => WindowStyles.StatusConnecting,
                ConnectionState.Compiling  => WindowStyles.StatusConnecting,
                ConnectionState.Error      => WindowStyles.StatusError,
                _                          => WindowStyles.StatusDisconnected,
            };
            _heroStatusDot.AddToClassList(className);
        }

        private string CalculateUptime()
        {
            if (_runtimeState.ConnectionState != ConnectionState.Connected) return "--";
            if (!_runtimeState.LastConnectedAt.HasValue) return "--";
            var elapsed = DateTime.Now - _runtimeState.LastConnectedAt.Value;
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            if (elapsed.TotalMinutes >= 1)
                return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
            return $"{(int)elapsed.TotalSeconds}s";
        }

        private void Reconnect()
        {
            UnityMcpPlugin.Reconnect();
        }
        private void Disconnect()
        {
            UnityMcpPlugin.Disconnect();
        }

        private void DisconnectAll()
        {
            _runtimeState.ClearClients();
            RefreshState();
        }
    }
}
