package websocket

import (
	"context"
	"crypto/subtle"
	"encoding/json"
	"errors"
	"fmt"
	"log"
	"net/http"
	"net/url"
	"os"
	"sort"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"time"

	gw "github.com/gorilla/websocket"
)

const (
	defaultReadTimeout  = 45 * time.Second
	defaultWriteTimeout = 10 * time.Second
	defaultDialTimeout  = 30 * time.Second
)

// Hub coordinates Unity websocket clients and request/response correlation.
type Hub struct {
	port int
	host string
	path string

	requestTimeout    time.Duration
	heartbeatInterval time.Duration

	server *http.Server

	mu        sync.RWMutex
	clients   map[string]*Client
	pending   map[string]chan responseEnvelope
	nextReq   atomic.Uint64
	logger    *log.Logger
	started   atomic.Bool
	stopped   atomic.Bool
	shutdown  chan struct{}
	authToken string
}

// NewHub constructs a websocket hub listening on the provided port.
func NewHub(port int) *Hub {
	return &Hub{
		port:              port,
		host:              "127.0.0.1",
		path:              "/ws",
		requestTimeout:    defaultDialTimeout,
		heartbeatInterval: 20 * time.Second,
		clients:           make(map[string]*Client),
		pending:           make(map[string]chan responseEnvelope),
		logger:            log.New(os.Stderr, "[unity-mcp/ws] ", log.LstdFlags|log.Lmicroseconds),
		shutdown:          make(chan struct{}),
	}
}

// Configure updates the websocket host, path, timing, and auth settings.
func (h *Hub) Configure(host, path string, requestTimeout, heartbeatInterval time.Duration, authToken string) {
	if strings.TrimSpace(host) != "" {
		h.host = host
	}
	if strings.TrimSpace(path) != "" {
		h.path = path
	}
	if requestTimeout > 0 {
		h.requestTimeout = requestTimeout
	}
	if heartbeatInterval > 0 {
		h.heartbeatInterval = heartbeatInterval
	}
	if strings.TrimSpace(authToken) != "" {
		h.authToken = authToken
	}
}

// Start launches the websocket HTTP server and blocks until shutdown.
func (h *Hub) Start() {
	if h.started.Swap(true) {
		return
	}

	mux := http.NewServeMux()
	mux.HandleFunc(h.path, h.handleWS)
	mux.HandleFunc("/healthz", func(w http.ResponseWriter, _ *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte(`{"ok":true}`))
	})

	addr := netJoinHostPort(h.host, h.port)
	h.server = &http.Server{
		Addr:              addr,
		Handler:           mux,
		ReadHeaderTimeout: 5 * time.Second,
		IdleTimeout:       60 * time.Second,
		ReadTimeout:       defaultReadTimeout,
		WriteTimeout:      defaultWriteTimeout,
	}

	h.logger.Printf("websocket hub listening on %s%s", addr, h.path)
	if err := h.server.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		h.logger.Printf("websocket server stopped with error: %v", err)
	}
}

// Stop gracefully shuts down the websocket server and disconnects clients.
func (h *Hub) Stop() {
	if h.stopped.Swap(true) {
		return
	}

	close(h.shutdown)
	h.mu.Lock()
	for id, client := range h.clients {
		_ = client.Close()
		delete(h.clients, id)
	}
	for id, ch := range h.pending {
		close(ch)
		delete(h.pending, id)
	}
	h.mu.Unlock()

	if h.server != nil {
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		_ = h.server.Shutdown(ctx)
	}
}

// InvokeTool forwards a Unity tool call to the best available connected client.
func (h *Hub) InvokeTool(ctx context.Context, toolName string, args map[string]any) (json.RawMessage, error) {
	client := h.pickClient()
	if client == nil {
		return nil, fmt.Errorf("no Unity clients are connected")
	}

	requestID := h.newRequestID()
	respCh := make(chan responseEnvelope, 1)

	h.mu.Lock()
	h.pending[requestID] = respCh
	h.mu.Unlock()

	// JSON-RPC 2.0 request format
	req := jsonRPCRequest{
		JSONRPC: "2.0",
		ID:      requestID,
		Method:  "tools/call",
		Params: toolCallParams{
			Name:      toolName,
			Arguments: args,
		},
	}

	payload, err := json.Marshal(req)
	if err != nil {
		h.deletePending(requestID)
		return nil, err
	}

	if err := client.Send(payload); err != nil {
		h.deletePending(requestID)
		return nil, err
	}

	timeout := h.requestTimeout
	select {
	case <-ctx.Done():
		h.deletePending(requestID)
		return nil, ctx.Err()
	case resp, ok := <-respCh:
		if !ok {
			return nil, fmt.Errorf("request %s was cancelled", requestID)
		}
		if resp.Error != nil {
			return nil, errors.New(resp.Error.Message)
		}
		return resp.Result, nil
	case <-time.After(timeout):
		h.deletePending(requestID)
		return nil, fmt.Errorf("timed out waiting for Unity response to %s", toolName)
	}
}

// NotifyMcpClientActivity tells connected Unity editors that the stdio MCP side
// is active. Unity uses this as UI-only status so users can see clients such as
// the current OpenCode session, even though they are not WebSocket clients.
func (h *Hub) NotifyMcpClientActivity(toolName string) {
	h.BroadcastNotification("mcp/client_seen", map[string]any{
		"id":            fmt.Sprintf("mcp-stdio-%d", os.Getpid()),
		"name":          "OpenCode / MCP stdio client",
		"remoteAddress": fmt.Sprintf("stdio pid %d", os.Getpid()),
		"tool":          toolName,
		"lastSeenUtc":   time.Now().UTC().Format(time.RFC3339Nano),
	})
}

// BroadcastNotification sends a JSON-RPC notification to all connected Unity
// websocket clients. It is best-effort and must never block a tool call.
func (h *Hub) BroadcastNotification(method string, params map[string]any) {
	notification := struct {
		JSONRPC string         `json:"jsonrpc"`
		Method  string         `json:"method"`
		Params  map[string]any `json:"params"`
	}{
		JSONRPC: "2.0",
		Method:  method,
		Params:  params,
	}

	payload, err := json.Marshal(notification)
	if err != nil {
		h.logger.Printf("failed to marshal notification %s: %v", method, err)
		return
	}

	h.mu.RLock()
	clients := make([]*Client, 0, len(h.clients))
	for _, client := range h.clients {
		if client.IsAlive() {
			clients = append(clients, client)
		}
	}
	h.mu.RUnlock()

	for _, client := range clients {
		if err := client.Send(payload); err != nil {
			h.logger.Printf("failed to send notification %s to %s: %v", method, client.ID, err)
		}
	}
}

func (h *Hub) pickClient() *Client {
	h.mu.RLock()
	defer h.mu.RUnlock()

	if len(h.clients) == 0 {
		return nil
	}

	clients := make([]*Client, 0, len(h.clients))
	for _, client := range h.clients {
		if client.IsAlive() {
			clients = append(clients, client)
		}
	}
	if len(clients) == 0 {
		return nil
	}

	sort.Slice(clients, func(i, j int) bool {
		return clients[i].LastSeen().After(clients[j].LastSeen())
	})
	return clients[0]
}

func (h *Hub) newRequestID() string {
	return strconv.FormatUint(h.nextReq.Add(1), 10)
}

func (h *Hub) deletePending(id string) {
	h.mu.Lock()
	if ch, ok := h.pending[id]; ok {
		delete(h.pending, id)
		close(ch)
	}
	h.mu.Unlock()
}

func (h *Hub) registerClient(c *Client) {
	h.mu.Lock()
	h.clients[c.ID] = c
	h.mu.Unlock()
	h.logger.Printf("Unity client connected: %s (%s)", c.ID, c.RemoteAddr)
}

func (h *Hub) unregisterClient(c *Client) {
	h.mu.Lock()
	if existing, ok := h.clients[c.ID]; ok && existing == c {
		delete(h.clients, c.ID)
	}
	h.mu.Unlock()
	h.logger.Printf("Unity client disconnected: %s", c.ID)
}

func (h *Hub) handleResponse(resp responseEnvelope) {
	h.mu.Lock()
	ch, ok := h.pending[resp.ID]
	if ok {
		delete(h.pending, resp.ID)
	}
	h.mu.Unlock()
	if ok {
		ch <- resp
		close(ch)
	}
}

// authenticate validates the WebSocket upgrade request.
func (h *Hub) authenticate(r *http.Request) bool {
	// If no auth token configured, only allow localhost connections
	if h.authToken == "" {
		return isLocalhost(r.RemoteAddr)
	}

	header := r.Header.Get("Authorization")
	expected := "Bearer " + h.authToken
	return subtle.ConstantTimeCompare([]byte(header), []byte(expected)) == 1
}

// checkOrigin validates the WebSocket origin header.
func (h *Hub) checkOrigin(r *http.Request) bool {
	origin := r.Header.Get("Origin")
	if origin == "" {
		// Non-browser clients (like Unity) may not send Origin
		return true
	}

	parsed, err := url.Parse(origin)
	if err != nil {
		return false
	}

	hostname := parsed.Hostname()
	// Allow localhost and loopback
	return hostname == "localhost" ||
		hostname == "127.0.0.1" ||
		hostname == "::1" ||
		hostname == h.host
}

func (h *Hub) handleWS(w http.ResponseWriter, r *http.Request) {
	// Authenticate before upgrade
	if !h.authenticate(r) {
		h.logger.Printf("authentication failed from %s", r.RemoteAddr)
		http.Error(w, "unauthorized", http.StatusUnauthorized)
		return
	}

	upgrader := gw.Upgrader{
		CheckOrigin: h.checkOrigin,
	}
	if !upgrader.CheckOrigin(r) {
		h.logger.Printf("origin check failed from %s origin=%s", r.RemoteAddr, r.Header.Get("Origin"))
		http.Error(w, "forbidden", http.StatusForbidden)
		return
	}

	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		h.logger.Printf("websocket upgrade failed: %v", err)
		return
	}

	client := NewClient(h, conn, r)
	h.registerClient(client)
	go client.run()
}

func netJoinHostPort(host string, port int) string {
	return fmt.Sprintf("%s:%d", strings.TrimSpace(host), port)
}

// isLocalhost checks if a remote address is on localhost.
func isLocalhost(remoteAddr string) bool {
	return strings.HasPrefix(remoteAddr, "127.0.0.1:") ||
		strings.HasPrefix(remoteAddr, "[::1]:") ||
		strings.HasPrefix(remoteAddr, "localhost")
}

// JSON-RPC 2.0 request format
type jsonRPCRequest struct {
	JSONRPC string         `json:"jsonrpc"`
	ID      string         `json:"id"`
	Method  string         `json:"method"`
	Params  toolCallParams `json:"params"`
}

type toolCallParams struct {
	Name      string         `json:"name"`
	Arguments map[string]any `json:"arguments,omitempty"`
}

// JSON-RPC 2.0 response format
type responseError struct {
	Code    int    `json:"code"`
	Message string `json:"message"`
}

type responseEnvelope struct {
	JSONRPC string          `json:"jsonrpc"`
	ID      string          `json:"id"`
	Result  json.RawMessage `json:"result,omitempty"`
	Error   *responseError  `json:"error,omitempty"`
}
