package mcp

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"os"
	"strings"

	mcpgo "github.com/mark3labs/mcp-go/mcp"
	mcpserver "github.com/mark3labs/mcp-go/server"
	"github.com/unity-mcp/server/internal/config"
	"github.com/unity-mcp/server/internal/websocket"
)

// Server wraps the stdio MCP server and forwards tools to Unity over WebSocket.
type Server struct {
	cfg          config.Config
	hub          *websocket.Hub
	srv          *mcpserver.MCPServer
	toolManifest []mcpgo.Tool
	logger       *log.Logger
}

// NewServer creates a configured MCP server and registers Unity tools.
func NewServer(cfg config.Config, hub *websocket.Hub) *Server {
	logger := log.New(os.Stderr, "[unity-mcp] ", log.LstdFlags|log.Lmicroseconds)
	hub.Configure(cfg.WebSocketHost, cfg.WebSocketPath, cfg.RequestTimeout, cfg.HeartbeatInterval, cfg.AuthToken)

	s := &Server{
		cfg:          cfg,
		hub:          hub,
		toolManifest: ToolDefinitions(),
		logger:       logger,
	}

	s.srv = mcpserver.NewMCPServer(
		cfg.ServerName,
		cfg.ServerVersion,
		mcpserver.WithToolCapabilities(true),
	)

	for _, tool := range s.toolManifest {
		toolCopy := tool
		if err := s.srv.AddTool(toolCopy, s.handleTool(toolCopy.Name)); err != nil {
			logger.Printf("failed to register tool %q: %v", toolCopy.Name, err)
		}
	}

	return s
}

// Run starts the stdio MCP server and blocks until the context is cancelled.
func (s *Server) Run(ctx context.Context) error {
	s.logger.Println("Starting MCP stdio server")
	return s.srv.Run(ctx)
}

// Manifest returns a cached copy of the tool manifest.
func (s *Server) Manifest() []mcpgo.Tool {
	manifest := make([]mcpgo.Tool, len(s.toolManifest))
	copy(manifest, s.toolManifest)
	return manifest
}

func (s *Server) handleTool(toolName string) mcpserver.ToolHandlerFunc {
	return func(ctx context.Context, req mcpgo.CallToolRequest) (*mcpgo.CallToolResult, error) {
		args := req.Arguments
		if args == nil {
			args = map[string]any{}
		}

		s.hub.NotifyMcpClientActivity(toolName)

		payload, err := s.hub.InvokeTool(ctx, toolName, args)
		if err != nil {
			return &mcpgo.CallToolResult{
				Content: []any{mcpgo.NewTextContent(fmt.Sprintf("tool %s failed: %v", toolName, err))},
				IsError: true,
			}, nil
		}

		text := strings.TrimSpace(string(payload))
		if text == "" {
			text = "{}"
		}

		// Normalize valid JSON payloads into text content so MCP clients can render them.
		if json.Valid(payload) {
			text = string(payload)
		}

		return &mcpgo.CallToolResult{
			Content: []any{mcpgo.NewTextContent(text)},
		}, nil
	}
}
