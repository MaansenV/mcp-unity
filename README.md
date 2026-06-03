# MCP Unity (Fork)

[![](https://badge.mcpx.dev?status=on 'MCP Enabled')](https://modelcontextprotocol.io/introduction)
[![](https://img.shields.io/badge/Unity-000000?style=flat&logo=unity&logoColor=white 'Unity')](https://unity.com/releases/editor/archive)
[![](https://img.shields.io/badge/Node.js-339933?style=flat&logo=nodedotjs&logoColor=white 'Node.js')](https://nodejs.org/en/download/)
[![](https://img.shields.io/github/stars/MaansenV/mcp-unity 'Stars')](https://github.com/MaansenV/mcp-unity/stargazers)
[![](https://img.shields.io/badge/License-MIT-red.svg 'MIT License')](https://opensource.org/licenses/MIT)

**LLM-powered Unity Editor automation via Model Context Protocol.** This fork builds on [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) with significant reliability, usability, and performance improvements.

```
MCP Client (OpenCode, Claude, Cursor...) ← stdio → Node.js MCP Server ← WebSocket → Unity Editor
```

---

## Why this fork

This fork makes the MCP Unity bridge **production-ready**. It fixes critical reliability issues, adds missing editor-control features, and improves the agent's ability to interact with Unity without manual intervention.

### 🔴 Critical fixes

| Fix | Problem | Solution |
|---|---|---|
| **No-focus timeouts** | Agent timed out when Unity Editor wasn't the active window. `EditorApplication.delayCall` is throttled/paused by Unity when it loses focus. | Replaced all `delayCall` with `EditorApplication.update`-based dispatch via `MainThreadDispatcher`. Tool execution now works even when Unity is unfocused or minimized. |
| **Stale Scene View** | Scene/Game view didn't update after MCP mutations. Agent was blind to its own changes. | Added `SceneView.RepaintAll()` + `InternalEditorUtility.RepaintAllViews()` after every mutation: create, delete, duplicate, reparent, move, rotate, scale, set_transform. |

### 🟡 Editor control improvements

| Feature | Details |
|---|---|
| **Multi-object selection** | `select_gameobject` supports `objectPaths[]` array for batch selection |
| **Additive selection** | `additive: true` adds to existing selection instead of replacing |
| **Frame-on-select** | `frame: true` frames the selected object in Scene View |
| **Object reference resolution** | `update_component` can wire `ScriptableObject`, `Component`, and `GameObject` references via asset paths, scene paths, and instance IDs |

### 🟢 New tools

| Tool | Description |
|---|---|
| `profiler_capture_frame` | Returns deltaTime, FPS, frameCount, timeSinceStartup, timeScale in one call |

### 🔵 Developer improvements

| Feature | Details |
|---|---|
| **MainThreadDispatcher** | Thread-safe `ConcurrentQueue<Action>` dispatcher wired to `EditorApplication.update`. Safe to call from any thread (WebSocket background threads included). |
| **ToolErrors helper** | Structured error messages (`NotFound`, `InvalidInput`, `ExecutionError`, `MultipleFound`) for consistent, LLM-friendly error responses |
| **Repaint infrastructure** | All mutation tools now repaint Scene View automatically |

### 📊 Feature comparison

| Capability | Original | This fork |
|---|---|---|
| Works without Unity focus | ❌ Timeout | ✅ Works |
| Scene View updates after mutations | ❌ Stale | ✅ Auto-repaint |
| Multi-object selection | ❌ Single only | ✅ Array + additive |
| Frame-on-select | ❌ | ✅ |
| Object refs in update_component | ❌ | ✅ |
| profiler_capture_frame | ❌ | ✅ |
| Structured errors | ❌ Ad-hoc | ✅ ToolErrors |
| MainThreadDispatcher | ❌ delayCall only | ✅ update-based |

---

## Requirements

- Unity 2022.3 or newer
- Node.js 18 or newer
- An MCP client: OpenCode, Cursor, Windsurf, Claude Code, Claude Desktop, GitHub Copilot, Codex CLI, Google Antigravity

---

## Install

### Via Unity Package Manager (recommended)

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL...**
3. Enter:
```
https://github.com/MaansenV/mcp-unity.git?path=/Packages/mcp-unity
```
4. Click **Add**

### Local development

This repo already references the package locally in `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.gamelovers.mcp-unity": "file:mcp-unity"
  }
}
```

---

## Setup

1. Install the package (see above)
2. Open **Tools → MCP Unity → Server Window** in Unity
3. The window shows connection status, port, and MCP client configuration snippets
4. Build the Node server (auto-installed on first open, or manually):
```bash
cd Packages/mcp-unity/Server~
npm install
npm run build
```

Default settings:
- WebSocket endpoint: `ws://localhost:8090/McpUnity`
- Settings file: `ProjectSettings/McpUnitySettings.json`
- Request timeout: 10 seconds
- Remote connections: disabled by default

---

## MCP client configuration

Point your MCP client at the built Node server entrypoint.

**OpenCode** (`opencode.json` in project root):
```json
{
  "mcp": {
    "mcp-unity": {
      "type": "local",
      "command": ["node", "Packages/mcp-unity/Server~/build/index.js"],
      "enabled": true
    }
  }
}
```

**Generic MCP clients** (Cursor, Windsurf, Claude, etc.):
```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": ["ABSOLUTE/PATH/TO/Packages/mcp-unity/Server~/build/index.js"]
    }
  }
}
```

The Unity Server Window (Tools → MCP Unity → Server Window) provides copy-paste snippets for all major clients with correct paths.

---

## Available tools

> See [docs/TOOLS.md](docs/TOOLS.md) for the full reference with descriptions and example prompts.

### Scene & GameObject (16 tools)
`gameobject_create` `gameobject_find` `get_gameobject` `select_gameobject` `update_gameobject` `update_component` `duplicate_gameobject` `delete_gameobject` `reparent_gameobject` `add_asset_to_scene` `create_prefab` `prefab_create_from_scene` `prefab_open` `prefab_close` `prefab_save` `object_get_data` `object_modify`

### Transform (4 tools)
`move_gameobject` `rotate_gameobject` `scale_gameobject` `set_transform`

### Components (3 tools)
`gameobject_component_get` `gameobject_component_destroy` `gameobject_component_list_all`

### Assets (11 tools)
`assets_find` `assets_find_built_in` `assets_get_data` `assets_create_folder` `assets_copy` `assets_move` `assets_delete` `assets_modify` `assets_refresh` `assets_shader_list_all`

### Materials (4 tools)
`create_material` `assign_material` `modify_material` `get_material_info`

### Scenes (9 tools)
`create_scene` `load_scene` `save_scene` `delete_scene` `unload_scene` `get_scene_info` `scene_set_active` `scene_get_data` `scene_list_opened`

### Editor & Console (7 tools)
`editor_application_get_state` `editor_application_set_state` `editor_selection_get` `execute_menu_item` `get_console_logs` `console_clear_logs` `send_console_log` `recompile_scripts`

### Profiler (5 tools)
`profiler_start` `profiler_stop` `profiler_get_status` `profiler_get_memory_stats` `profiler_capture_frame` 🆕

### Reflection & Types (3 tools)
`reflection_method_find` `reflection_method_call` `type_get_json_schema`

### Testing & Packages (4 tools)
`run_tests` `add_package` `package_list` `package_remove` `package_search`

### Performance (1 tool)
`batch_execute`

**Total: 69 tools, 7 resources, 1 prompt**

---

## Development layout

```
Packages/mcp-unity/
├── Editor/                       Unity Editor package code (C#)
│   ├── Tools/                    Unity-side MCP tool handlers
│   ├── Resources/                Unity-side MCP resources
│   ├── UnityBridge/              WebSocket server + message routing
│   ├── Services/                 Shared editor services
│   └── Utils/                    Helpers, MainThreadDispatcher, ToolErrors, Logger
├── Server~/                      Node.js MCP stdio server (TypeScript)
│   ├── src/index.ts              MCP tool/resource/prompt registration
│   ├── src/tools/                Node-side tool definitions (zod schemas)
│   ├── src/resources/            Node-side MCP resources
│   └── src/unity/                WebSocket client + connection + command queue
├── package.json                  Unity package manifest
└── server.json                   MCP registry metadata
```

---

## Adding a tool

1. Add C# tool under `Editor/Tools/` (inherit `McpToolBase`)
2. Register in `Editor/UnityBridge/McpUnityServer.cs` → `RegisterTools()`
3. Add TypeScript tool under `Server~/src/tools/` (zod schema + handler)
4. Register in `Server~/src/index.ts`
5. Build: `cd Server~ && npm run build`

Tool names must match **exactly** between Node and Unity.

---

## Troubleshooting

| Issue | Fix |
|---|---|
| Agent times out | Update to this fork — the fix is in `MainThreadDispatcher`. Also verify port 8090 is open. |
| `unknown_method` | Tool name mismatch between Node and Unity. Must be exact. |
| Scene View not updating | Update to this fork — all mutation tools now auto-repaint. |
| Package won't install | Git URL must include `?path=/Packages/mcp-unity` |
| Node not found | Install Node.js 18+, ensure `node` and `npm` on PATH |

---

## Original projects

This fork builds on work by:

- [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) — original MCP Unity implementation
- [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) — reference for `EditorApplication.update`-based dispatch pattern and editor control features

---

## License

MIT — see [LICENSE](LICENSE)
