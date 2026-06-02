package main

import (
	"context"
	"flag"
	"log"
	"os"
	"os/signal"
	"syscall"

	"github.com/unity-mcp/server/internal/config"
	"github.com/unity-mcp/server/internal/mcp"
	"github.com/unity-mcp/server/internal/websocket"
)

func main() {
	log.SetOutput(os.Stderr)

	// Parse flags
	mode := flag.String("mode", "", "server mode: stdio or websocket-only")
	flag.Parse()

	// Load config
	cfg := config.Load()

	// Override mode from flag if provided
	if *mode != "" {
		cfg.Mode = *mode
	}

	// Validate mode
	switch cfg.Mode {
	case "stdio", "websocket-only":
		// valid
	default:
		log.Fatalf("invalid mode %q, must be 'stdio' or 'websocket-only'", cfg.Mode)
	}

	// Create WebSocket hub for Unity connections
	hub := websocket.NewHub(cfg.WebSocketPort)
	hub.Configure(cfg.WebSocketHost, cfg.WebSocketPath, cfg.RequestTimeout, cfg.HeartbeatInterval, cfg.AuthToken)

	// Handle graceful shutdown
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)

	// Start WebSocket hub in background
	go hub.Start()

	go func() {
		<-sigCh
		log.Println("Shutting down...")
		hub.Stop()
		cancel()
	}()

	log.Printf("Starting Unity MCP server v%s in %s mode", cfg.ServerVersion, cfg.Mode)

	switch cfg.Mode {
	case "websocket-only":
		// Unity started this server - just run WebSocket hub and block
		log.Println("Running in websocket-only mode (no MCP stdio)")
		<-ctx.Done()
		hub.Stop()

	case "stdio":
		// MCP client (opencode) started this server - run MCP stdio
		mcpServer := mcp.NewServer(cfg, hub)
		if err := mcpServer.Run(ctx); err != nil {
			log.Fatalf("Server error: %v", err)
		}
	}
}
