package config

import (
	"os"
	"strconv"
	"time"
)

// Config holds runtime settings for the Unity MCP server.
type Config struct {
	ServerName       string
	ServerVersion    string
	Mode             string // "stdio" or "websocket-only"
	WebSocketHost    string
	WebSocketPort    int
	WebSocketPath    string
	AuthToken        string
	RequestTimeout   time.Duration
	HeartbeatInterval time.Duration
	ShutdownTimeout  time.Duration
}

// Load builds configuration from environment variables with sane defaults.
func Load() Config {
	return Config{
		ServerName:       getEnv("UNITY_MCP_SERVER_NAME", "unity-mcp"),
		ServerVersion:    getEnv("UNITY_MCP_SERVER_VERSION", "0.1.0"),
		Mode:             getEnv("UNITY_MCP_MODE", "stdio"),
		WebSocketHost:    getEnv("UNITY_MCP_WS_HOST", "127.0.0.1"),
		WebSocketPort:    getEnvInt("UNITY_MCP_WS_PORT", 8081),
		WebSocketPath:    getEnv("UNITY_MCP_WS_PATH", "/ws"),
		AuthToken:        getEnv("UNITY_MCP_AUTH_TOKEN", ""),
		RequestTimeout:   getEnvDuration("UNITY_MCP_REQUEST_TIMEOUT", 30*time.Second),
		HeartbeatInterval: getEnvDuration("UNITY_MCP_HEARTBEAT_INTERVAL", 20*time.Second),
		ShutdownTimeout:  getEnvDuration("UNITY_MCP_SHUTDOWN_TIMEOUT", 5*time.Second),
	}
}

func getEnv(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func getEnvInt(key string, fallback int) int {
	if v := os.Getenv(key); v != "" {
		if parsed, err := strconv.Atoi(v); err == nil {
			return parsed
		}
	}
	return fallback
}

func getEnvDuration(key string, fallback time.Duration) time.Duration {
	if v := os.Getenv(key); v != "" {
		if parsed, err := time.ParseDuration(v); err == nil {
			return parsed
		}
	}
	return fallback
}
