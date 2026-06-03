# MCP Unity Server (Fork)

Fork of MCP Unity for controlling the Unity Editor from MCP clients.

This repository contains a Unity package at `Packages/mcp-unity`. The package runs a WebSocket bridge inside the Unity Editor and includes a Node.js MCP stdio server in `Server~/`.

## What this fork changes

- Package name: `com.gamelovers.mcp-unity`
- Fork MCP name: `io.github.maansenv/mcp-unity`
- Adds Unity object reference resolution in `update_component`
- Allows wiring `ScriptableObject`, `Component`, and `GameObject` references via:
  - asset paths
  - scene paths
  - instance IDs

## Requirements

- Unity 2022.3 or newer
- Node.js 18 or newer
- An MCP client such as OpenCode, Cursor, Windsurf, Claude Code, Codex CLI, GitHub Copilot, Google Antigravity, or another MCP-compatible client

## Install via Unity Package Manager

In Unity:

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL...**
3. Enter:

```text
https://github.com/MaansenV/mcp-unity.git?path=/Packages/mcp-unity
```

4. Click **Add**

> Use the URL above for this fork. The repository root is a Unity project, so the `?path=/Packages/mcp-unity` suffix is required for Unity Package Manager.

## Install locally during development

This repository already uses the package locally:

```json
{
  "dependencies": {
    "com.gamelovers.mcp-unity": "file:mcp-unity"
  }
}
```

For another Unity project, either add the package from the Git URL above or copy/link `Packages/mcp-unity` into that project's `Packages/` folder.

## Unity setup

1. Install the package.
2. Open **Tools → MCP Unity → Server Window** in Unity.
3. Use the window to install/build the Node server and configure your MCP client.

Default Unity bridge settings:

- WebSocket endpoint: `ws://localhost:8090/McpUnity`
- Settings file: `ProjectSettings/McpUnitySettings.json`
- Remote connections: disabled by default

## Manual server build

Only needed if you are developing/debugging the MCP server manually.

```bash
cd Packages/mcp-unity/Server~
npm install
npm run build
```

The MCP server entrypoint is:

```text
Packages/mcp-unity/Server~/build/index.js
```

## Manual MCP client configuration

Prefer the Unity server window configuration when possible. For manual setup, point your MCP client at Node and the built server entrypoint.

Example:

```json
{
  "mcp": {
    "mcp-unity": {
      "type": "local",
      "command": [
        "node",
        "ABSOLUTE/PATH/TO/Packages/mcp-unity/Server~/build/index.js"
      ],
      "enabled": true
    }
  }
}
```

## Development layout

```text
Packages/mcp-unity/
├── Editor/                       Unity Editor package code
│   ├── Tools/                    Unity-side MCP tool handlers
│   ├── Resources/                Unity-side MCP resources
│   ├── UnityBridge/              WebSocket server and editor window
│   ├── Services/                 Shared editor services
│   └── Utils/                    Helpers and client config utilities
├── Runtime/                      Shared runtime/protocol code
├── Server~/                      Node.js MCP stdio server
│   ├── src/index.ts              Registers MCP tools/resources/prompts
│   ├── src/tools/                Node-side MCP tool definitions
│   ├── src/resources/            Node-side MCP resources
│   └── src/unity/mcpUnity.ts      WebSocket client to Unity
├── package.json                  Unity package manifest
└── server.json                   MCP registry metadata
```

## Documentation

- [Tools & Resources reference](docs/TOOLS.md) – full list of all 68 MCP tools, 7 resources, and 1 prompt, grouped by category with anchor links.
- [Package AGENTS guide](Packages/mcp-unity/AGENTS.md) – bridge contract, defaults, debugging, and how to add new capabilities.
- [Package README](Packages/mcp-unity/README.md) – end-user docs and screenshots.

## Add a tool

1. Add a Unity tool under `Packages/mcp-unity/Editor/Tools/`.
2. Register it in `Editor/UnityBridge/McpUnityServer.cs`.
3. Add the matching Node tool under `Packages/mcp-unity/Server~/src/tools/`.
4. Register it in `Server~/src/index.ts`.
5. Build the Node server:

```bash
cd Packages/mcp-unity/Server~
npm run build
```

Tool names must match exactly between Node and Unity.

## Troubleshooting

- If Unity cannot connect, check that the server window is open and the bridge is listening on port `8090`.
- If a tool returns `unknown_method`, verify that the Node tool name matches the Unity tool `Name` exactly.
- If package installation fails, make sure the Git URL includes `?path=/Packages/mcp-unity`.
- If Node is not found, install Node.js 18+ and make sure `node`/`npm` are available on `PATH`.

## Original project

This fork is based on MCP Unity by CoderGamester:

```text
https://github.com/CoderGamester/mcp-unity
```
