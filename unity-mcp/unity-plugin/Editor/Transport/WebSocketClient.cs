#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityMCP.Shared;

namespace UnityMCP.Editor
{
    public sealed class WebSocketClient : IDisposable
    {
        readonly Uri _uri;
        readonly string _authToken;
        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        readonly object _stateLock = new object();

        ClientWebSocket _client;
        CancellationTokenSource _cts;
        Task _receiveTask;
        Task _heartbeatTask;
        int _connecting;
        int _disposed;

        public event Action<string> OnMessage;
        public event Action<ConnectionState> OnConnectionChanged;
        public event Action<string> OnError;

        public bool IsConnected
        {
            get
            {
                if (IsDisposed) return false;
                lock (_stateLock)
                {
                    return _client != null && _client.State == WebSocketState.Open;
                }
            }
        }

        public bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 0, 0) == 1;

        public WebSocketClient(string url, string authToken = null)
        {
            _uri = new Uri(url);
            _authToken = authToken;
        }

        void RaiseConnectionChanged(ConnectionState state)
        {
            if (IsDisposed) return;
            MainThreadDispatcher.Enqueue(() => OnConnectionChanged?.Invoke(state));
        }

        void RaiseError(string message)
        {
            if (IsDisposed) return;
            MainThreadDispatcher.Enqueue(() => OnError?.Invoke(message));
        }

        public void Connect()
        {
            if (IsDisposed) return;
            if (Interlocked.Exchange(ref _connecting, 1) == 1) return;

            _ = ConnectAsync();
        }

        public void Disconnect()
        {
            if (IsDisposed) return;

            CancellationTokenSource cts;
            lock (_stateLock)
            {
                cts = _cts;
            }

            try { cts?.Cancel(); }
            catch { }

            _ = DisconnectAsync();
        }

        public void Reconnect()
        {
            if (IsDisposed) return;
            Disconnect();
            Connect();
        }

        public void ProcessMessages()
        {
            if (IsDisposed) return;
            while (_incoming.TryDequeue(out var message))
            {
                OnMessage?.Invoke(message);
            }
        }

        public async Task SendJsonAsync<T>(T message, CancellationToken cancellationToken = default)
        {
            if (IsDisposed) return;
            string json;
            if (message is JsonRpcResponse response)
                json = JsonRpcParser.ResponseToJson(response);
            else
                json = SimpleJson.SerializeObject(message);
            await SendTextAsync(json, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendTextAsync(string message, CancellationToken cancellationToken = default)
        {
            if (IsDisposed) return;
            if (!IsConnected) return;

            var bytes = Encoding.UTF8.GetBytes(message ?? string.Empty);

            // Check if disposed before waiting on semaphore
            if (IsDisposed) return;

            try
            {
                await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                if (IsDisposed || !IsConnected) return;

                await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try { _sendLock.Release(); }
                catch { }
            }
        }

        public Task SendJsonRpcResponseAsync(JsonRpcResponse response, CancellationToken cancellationToken = default)
        {
            return SendJsonAsync(response, cancellationToken);
        }

        async Task ConnectAsync()
        {
            if (IsDisposed) return;

            var shouldReconnect = false;

            try
            {
                RaiseConnectionChanged(ConnectionState.Connecting);

                ClientWebSocket client;
                CancellationTokenSource cts;

                lock (_stateLock)
                {
                    // Cancel existing connection
                    try { _cts?.Cancel(); } catch { }
                    try { _cts?.Dispose(); } catch { }
                    try { _client?.Dispose(); } catch { }

                    _cts = new CancellationTokenSource();
                    _client = new ClientWebSocket();
                    _client.Options.KeepAliveInterval = Config.HeartbeatInterval;

                    // Set auth token header if configured (cached from constructor, main thread safe)
                    if (!string.IsNullOrEmpty(_authToken))
                    {
                        _client.Options.SetRequestHeader("Authorization", $"Bearer {_authToken}");
                    }

                    client = _client;
                    cts = _cts;
                }

                if (IsDisposed) return;

                using (var timeoutCts = new CancellationTokenSource(Config.ConnectTimeout))
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token))
                {
                    await client.ConnectAsync(_uri, linked.Token).ConfigureAwait(false);
                }

                if (IsDisposed)
                {
                    try { client.Abort(); } catch { }
                    return;
                }

                RaiseConnectionChanged(ConnectionState.Connected);

                _receiveTask = ReceiveLoopAsync(cts.Token);
                _heartbeatTask = HeartbeatLoopAsync(cts.Token);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    RaiseError($"WebSocket connect failed: {ex.GetType().Name}: {ex.Message}");
                    RaiseConnectionChanged(ConnectionState.Error);
                    if (Config.AutoReconnect)
                    {
                        shouldReconnect = true;
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _connecting, 0);

                if (shouldReconnect && !IsDisposed)
                {
                    _ = Task.Delay(Config.ReconnectDelay).ContinueWith(_ =>
                    {
                        if (!IsDisposed && _cts != null && !_cts.IsCancellationRequested)
                        {
                            Connect();
                        }
                    });
                }
            }
        }

        async Task DisconnectAsync()
        {
            ClientWebSocket client;
            CancellationTokenSource cts;

            lock (_stateLock)
            {
                client = _client;
                cts = _cts;
                _client = null;
                _cts = null;
                _receiveTask = null;
                _heartbeatTask = null;
            }

            try
            {
                if (client != null && client.State == WebSocketState.Open)
                {
                    using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                    {
                        try
                        {
                            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", timeoutCts.Token).ConfigureAwait(false);
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                try { client?.Dispose(); } catch { }
                try { cts?.Dispose(); } catch { }

                if (!IsDisposed)
                {
                    RaiseConnectionChanged(ConnectionState.Disconnected);
                }
            }
        }

        async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[Math.Max(1024, Config.ReceiveBufferSize)];

            try
            {
                while (!cancellationToken.IsCancellationRequested && !IsDisposed)
                {
                    ClientWebSocket client;
                    lock (_stateLock)
                    {
                        client = _client;
                    }

                    if (client == null || client.State != WebSocketState.Open)
                    {
                        if (!cancellationToken.IsCancellationRequested && !IsDisposed && Config.AutoReconnect)
                        {
                            RaiseError("WebSocket receive loop ended while disconnected; reconnecting...");
                            Reconnect();
                        }
                        break;
                    }

                    using (var ms = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await DisconnectAsync().ConfigureAwait(false);
                                if (!cancellationToken.IsCancellationRequested && !IsDisposed && Config.AutoReconnect)
                                {
                                    RaiseError("WebSocket closed by remote; reconnecting...");
                                    Reconnect();
                                }
                                return;
                            }

                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage && !cancellationToken.IsCancellationRequested);

                        var message = Encoding.UTF8.GetString(ms.ToArray());
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            _incoming.Enqueue(message);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                if (!IsDisposed && !cancellationToken.IsCancellationRequested)
                {
                    RaiseError($"WebSocket receive failed: {ex.GetType().Name}: {ex.Message}");
                    RaiseConnectionChanged(ConnectionState.Error);
                    if (Config.AutoReconnect)
                    {
                        Reconnect();
                    }
                }
            }
        }

        async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && !IsDisposed)
                {
                    await Task.Delay(Config.HeartbeatInterval, cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested || IsDisposed) break;

                    if (IsConnected)
                    {
                        await SendJsonAsync(new JsonRpcNotification
                        {
                            method = "heartbeat"
                        }, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    RaiseError($"WebSocket heartbeat failed: {ex.GetType().Name}: {ex.Message}");
                    RaiseConnectionChanged(ConnectionState.Error);
                    if (Config.AutoReconnect)
                    {
                        Reconnect();
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

            CancellationTokenSource cts;
            ClientWebSocket client;

            lock (_stateLock)
            {
                cts = _cts;
                client = _client;
                _cts = null;
                _client = null;
                _receiveTask = null;
                _heartbeatTask = null;
            }

            try { cts?.Cancel(); } catch { }

            try { client?.Abort(); } catch { }
            try { client?.Dispose(); } catch { }
            try { cts?.Dispose(); } catch { }

            try { _sendLock.Dispose(); } catch { }
        }
    }
}
#endif
