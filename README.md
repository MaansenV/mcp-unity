# Unity MCP

Unity MCP is a local Model Context Protocol bridge for controlling the Unity Editor from MCP clients such as OpenCode.

It consists of:

- a **Go MCP server** that exposes Unity tools over stdio
- a **Unity Editor package** that connects to the server over localhost WebSocket and executes editor-safe tool calls on Unity's main thread

```text
OpenCode / MCP client
        │ stdio JSON-RPC
        ▼
Go MCP server
        │ localhost WebSocket
        ▼
Unity Editor package
```

## Features

- 80+ Unity Editor tools for scene, GameObject, asset, script, material, prefab, console, editor, and profiler workflows
- Auto-reconnect WebSocket transport
- Main-thread dispatch for Unity API calls
- Unity MCP window with status, tool list, logs, setup, and connected-client visibility
- Local-only by default
- No external runtime dependencies beyond Go and Unity

## Requirements

- Unity 6 / 6000.x or newer
- Go 1.23+
- An MCP client, for example OpenCode

## Install the Unity package

### Option A: Unity Package Manager from Git

In Unity:

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL...**
3. Enter:

```text
https://github.com/YOUR_ORG/unity-mcp.git?path=unity-plugin
```

Replace `YOUR_ORG` with the GitHub organization/user you publish this repository under.

### Option B: Local package during development

Add this to your Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unity-mcp.editor": "file:../path/to/unity-mcp/unity-plugin"
  }
}
```

Or copy `unity-plugin/` into your project's `Packages/com.unity-mcp.editor/` folder.

## Build the MCP server

From the repository root:

```bash
cd server
go build -o bin/unity-mcp ./cmd/unity-mcp
```

On Windows:

```powershell
cd server
go build -o bin/unity-mcp.exe ./cmd/unity-mcp
```

The binary is intentionally ignored by git. Build it locally or publish release artifacts separately.

## Configure OpenCode

Copy `opencode.example.json` or add the MCP entry to your existing OpenCode config.

Linux/macOS example:

```json
{
  "mcp": {
    "unity": {
      "type": "local",
      "command": ["/absolute/path/to/unity-mcp/server/bin/unity-mcp"],
      "enabled": true,
      "environment": {
        "UNITY_MCP_WS_HOST": "127.0.0.1",
        "UNITY_MCP_WS_PORT": "8081",
        "UNITY_MCP_WS_PATH": "/ws"
      },
      "timeout": 30000
    }
  }
}
```

Windows example:

```json
{
  "mcp": {
    "unity": {
      "type": "local",
      "command": ["C:/path/to/unity-mcp/server/bin/unity-mcp.exe"],
      "enabled": true,
      "environment": {
        "UNITY_MCP_WS_HOST": "127.0.0.1",
        "UNITY_MCP_WS_PORT": "8081",
        "UNITY_MCP_WS_PATH": "/ws"
      },
      "timeout": 30000
    }
  }
}
```

## Start using it

1. Open your Unity project.
2. Open **Window → Unity MCP**.
3. Build/configure the Go server as above.
4. Start or restart your MCP client.
5. In the Unity MCP window, use **Reconnect** if the status is not connected.

The window should show:

- WebSocket connection status
- Unity MCP bridge peer
- active MCP/stdio clients such as OpenCode after tool calls
- all registered Unity MCP tools from the live Unity registry

## Tool groups

The server exposes tools in these groups:

- `unity.scene.*` — scene list/open/save/create/hierarchy/find
- `unity.gameobject.*` — create/delete/rename/find/components/transform/parent/duplicate
- `unity.asset.*` — find/info/import/delete/move/copy/folder/refresh/dependencies/GUID lookup
- `unity.script.*` — create/update/delete/read/compile status/errors
- `unity.material.*` — create/copy/get/set shader properties
- `unity.shader.*` — find shaders and inspect properties
- `unity.prefab.*` — create/instantiate/open/save/apply/revert/get info
- `unity.console.*` — logs/count/clear/subscribe
- `unity.editor.*` — state/play/stop/pause/selection/menu/undo/redo
- `unity.profiler.*` — start/stop/status/memory/rendering/script samples/modules/save/load/clear

## Development

```text
unity-mcp/
├── server/          Go MCP server
│   ├── cmd/         CLI entrypoint
│   ├── internal/    MCP, websocket, config internals
│   └── third_party/ vendored minimal dependencies
├── unity-plugin/    Unity package
│   ├── Editor/      editor window, transport, tools
│   └── Runtime/     shared protocol types
└── opencode.example.json
```

### Add a Unity tool

1. Add an `IToolHandler` class under `unity-plugin/Editor/Tools/<Group>/`.
2. Annotate it with `[McpTool("unity.group.name", "Description")]`.
3. Use `MainThreadDispatcher.EnqueueAsync(...)` for Unity API calls.
4. Add the matching Go tool definition in `server/internal/mcp/tools.go`.
5. Build the server and let Unity recompile the package.

Minimal example:

```csharp
[McpTool("unity.example.ping", "Ping Unity")]
public sealed class PingTool : IToolHandler
{
    public Task<object?> ExecuteAsync(ToolContext context)
    {
        return MainThreadDispatcher.EnqueueAsync<object?>(() => new
        {
            success = true,
            message = "pong"
        });
    }
}
```

## Security notes

This bridge can create, modify, and delete Unity assets and scene objects. Treat it like local developer tooling with editor-level permissions.

Defaults are local-only:

- WebSocket host: `127.0.0.1`
- WebSocket path: `/ws`
- optional bearer-token auth via package settings/environment

Do not expose the WebSocket port to untrusted networks.

## Troubleshooting

### No Unity clients are connected

- Make sure Unity is open.
- Open **Window → Unity MCP** and click **Reconnect**.
- Check that OpenCode and Unity use the same port/path, usually `127.0.0.1:8081/ws`.

### Tools time out

- Check the Unity Console for compile errors.
- Restart the MCP server binary after rebuilding it.
- Avoid running many Unity-mutating tools in parallel.

### OpenCode is not visible in the Unity MCP window

- Rebuild and restart the Go MCP server.
- Trigger any Unity MCP tool call from OpenCode.
- The client entry appears after the server sends a `mcp/client_seen` notification.

## License

MIT
