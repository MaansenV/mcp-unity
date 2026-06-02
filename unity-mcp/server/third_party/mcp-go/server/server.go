package server

import (
	"bufio"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"sync"

	mcp "github.com/mark3labs/mcp-go/mcp"
)

// ToolHandlerFunc handles a tool call.
type ToolHandlerFunc func(context.Context, mcp.CallToolRequest) (*mcp.CallToolResult, error)

// ServerOption configures the MCP server.
type ServerOption func(*MCPServer)

// MCPServer is a minimal stdio MCP server implementation.
type MCPServer struct {
	Name        string
	Version     string
	tools       map[string]mcp.Tool
	handlers    map[string]ToolHandlerFunc
	toolEnabled bool
	mu          sync.RWMutex
}

// NewMCPServer creates a new server instance.
func NewMCPServer(name, version string, opts ...ServerOption) *MCPServer {
	s := &MCPServer{
		Name:     name,
		Version:  version,
		tools:    make(map[string]mcp.Tool),
		handlers: make(map[string]ToolHandlerFunc),
	}
	for _, opt := range opts {
		opt(s)
	}
	return s
}

// WithToolCapabilities enables tool support in initialize responses.
func WithToolCapabilities(enabled bool) ServerOption {
	return func(s *MCPServer) { s.toolEnabled = enabled }
}

// AddTool registers a tool and its handler.
func (s *MCPServer) AddTool(tool mcp.Tool, handler ToolHandlerFunc) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if tool.Name == "" {
		return errors.New("tool name cannot be empty")
	}
	s.tools[tool.Name] = tool
	s.handlers[tool.Name] = handler
	return nil
}

// Run serves a very small JSON-RPC stdio loop.
func (s *MCPServer) Run(ctx context.Context) error {
	reader := bufio.NewReader(os.Stdin)
	encoder := json.NewEncoder(os.Stdout)

	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		default:
		}

		line, err := reader.ReadBytes('\n')
		if err != nil {
			if errors.Is(err, io.EOF) {
				return nil
			}
			return err
		}
		line = trimSpace(line)
		if len(line) == 0 {
			continue
		}

		var req rpcRequest
		if err := json.Unmarshal(line, &req); err != nil {
			_ = encoder.Encode(rpcResponse{JSONRPC: "2.0", ID: nil, Error: &rpcError{Code: -32700, Message: err.Error()}})
			continue
		}

		if req.Method == "" {
			continue
		}
		resp := s.handleRequest(ctx, req)
		if req.ID != nil {
			if err := encoder.Encode(resp); err != nil {
				return err
			}
		}
	}
}

func (s *MCPServer) handleRequest(ctx context.Context, req rpcRequest) rpcResponse {
	switch req.Method {
	case "initialize":
		return rpcResponse{
			JSONRPC: "2.0",
			ID:      req.ID,
			Result: map[string]any{
				"protocolVersion": "2024-11-05",
				"serverInfo": map[string]any{
					"name":    s.Name,
					"version": s.Version,
				},
				"capabilities": map[string]any{
					"tools": map[string]any{"listChanged": false},
				},
			},
		}
	case "notifications/initialized":
		return rpcResponse{}
	case "ping":
		return rpcResponse{JSONRPC: "2.0", ID: req.ID, Result: map[string]any{"ok": true}}
	case "tools/list":
		s.mu.RLock()
		tools := make([]mcp.Tool, 0, len(s.tools))
		for _, tool := range s.tools {
			tools = append(tools, tool)
		}
		s.mu.RUnlock()
		return rpcResponse{JSONRPC: "2.0", ID: req.ID, Result: map[string]any{"tools": tools}}
	case "tools/call":
		var payload mcp.CallToolRequest
		if len(req.Params) > 0 {
			_ = json.Unmarshal(req.Params, &payload)
		}
		if payload.Name == "" {
			return rpcResponse{JSONRPC: "2.0", ID: req.ID, Error: &rpcError{Code: -32602, Message: "tool name is required"}}
		}
		s.mu.RLock()
		handler := s.handlers[payload.Name]
		s.mu.RUnlock()
		if handler == nil {
			return rpcResponse{JSONRPC: "2.0", ID: req.ID, Error: &rpcError{Code: -32601, Message: fmt.Sprintf("unknown tool %q", payload.Name)}}
		}
		result, err := handler(ctx, payload)
		if err != nil {
			return rpcResponse{JSONRPC: "2.0", ID: req.ID, Error: &rpcError{Code: -32000, Message: err.Error()}}
		}
		return rpcResponse{JSONRPC: "2.0", ID: req.ID, Result: result}
	default:
		return rpcResponse{JSONRPC: "2.0", ID: req.ID, Error: &rpcError{Code: -32601, Message: "method not found"}}
	}
}

type rpcRequest struct {
	JSONRPC string          `json:"jsonrpc"`
	ID      any             `json:"id,omitempty"`
	Method  string          `json:"method"`
	Params  json.RawMessage `json:"params,omitempty"`
}

type rpcResponse struct {
	JSONRPC string    `json:"jsonrpc"`
	ID      any       `json:"id,omitempty"`
	Result  any       `json:"result,omitempty"`
	Error   *rpcError `json:"error,omitempty"`
}

type rpcError struct {
	Code    int    `json:"code"`
	Message string `json:"message"`
}

func trimSpace(b []byte) []byte {
	start := 0
	for start < len(b) && (b[start] == ' ' || b[start] == '\t' || b[start] == '\n' || b[start] == '\r') {
		start++
	}
	end := len(b)
	for end > start && (b[end-1] == ' ' || b[end-1] == '\t' || b[end-1] == '\n' || b[end-1] == '\r') {
		end--
	}
	return b[start:end]
}
