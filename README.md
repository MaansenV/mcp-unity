<div align="center">

# MCP Unity

**Give your AI agent full control over Unity Editor.**

[![MCP Enabled](https://badge.mcpx.dev?status=on)](https://modelcontextprotocol.io/introduction)
[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-000000?style=flat&logo=unity&logoColor=white)](https://unity.com/releases/editor/archive)
[![Node.js](https://img.shields.io/badge/Node.js-18%2B-339933?style=flat&logo=nodedotjs&logoColor=white)](https://nodejs.org/en/download/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![CI](https://img.shields.io/github/actions/workflow/status/MaansenV/mcp-unity/ci.yml?label=CI)](https://github.com/MaansenV/mcp-unity/actions)

77 tools · 7 resources · 1 prompt · Scene View screenshots · Script editing · Full editor automation

**[Install](#installation)** · **[Quick Start](#quick-start)** · **[Tools Reference](docs/TOOLS.md)** · **[Add Your Own](#extending)**

</div>

---

## What is this?

MCP Unity connects AI assistants — Claude, Cursor, OpenCode, Windsurf, GitHub Copilot, and any MCP-compatible client — directly to the Unity Editor.

Your agent can **create GameObjects**, **modify scenes**, **run tests**, **capture screenshots**, **edit scripts**, **profile performance**, and **manage the entire editor lifecycle** — all through natural language.

```
┌──────────────┐       stdio        ┌────────────────┐      WebSocket       ┌──────────────┐
│  MCP Client  │ ◄────────────────► │  Node.js MCP   │ ◄──────────────────► │ Unity Editor │
│  (Claude,    │                    │  Server (TS)   │    ws://8090         │  (C# Tools)  │
│   Cursor...) │                    └────────────────┘                      └──────────────┘
└──────────────┘
```

## What makes this different?

MCP Unity builds on [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) with production-ready reliability. The original works — this one works *everywhere*.

| | Original | This fork |
|---|---|---|
| **Agent works without Unity focused** | ❌ Timeout | ✅ `MainThreadDispatcher` |
| **Scene View updates after mutations** | ❌ Stale | ✅ Auto-repaint |
| **Multi-object selection** | ❌ Single only | ✅ Array + additive + frame |
| **Object reference wiring** | ❌ Manual | ✅ `update_component` resolves refs |
| **Screenshots** | ❌ | ✅ Scene View + Game View capture |
| **Script read/write** | ❌ | ✅ Create and edit `.cs` files |
| **Profiler integration** | 5 tools | 8 tools (recording, history, frame capture) |
| **CI/CD** | ❌ | ✅ GitHub Actions |
| **Structured error messages** | Ad-hoc | ✅ `ToolErrors` helper |

## Installation

### Unity Package Manager (recommended)

1. **Window → Package Manager → + → Add package from git URL**
2. Paste:
   ```
   https://github.com/MaansenV/mcp-unity.git?path=/Packages/mcp-unity
   ```
3. Click **Add**

That's it. The Node server auto-installs on first use.

### Verify it works

1. Open **Tools → MCP Unity → Server Window** in Unity
2. You should see `Connected` status on port `8090`
3. Point your MCP client at the server (see [client config](#mcp-client-configuration) below)

## Quick Start

Once connected, ask your AI agent to:

> *"Create a 3D scene with a red cube on a green plane, position the camera to look at them, and take a screenshot"*

> *"Find all GameObjects with a Rigidbody component and set their mass to 2.5"*

> *"Read the PlayerController.cs script and add a double-jump mechanic"*

> *"Run the test suite and tell me which tests fail"*

> *"Open the profiler, capture 10 frames, and show me the memory allocation breakdown"*

Your agent has **77 tools** covering every aspect of Unity Editor — from scene management to shader inspection to prefab workflows.

## MCP Client Configuration

Point your MCP client at the built Node server. The Unity Server Window (**Tools → MCP Unity → Server Window**) provides copy-paste snippets for all clients.

<details>
<summary><strong>OpenCode</strong></summary>

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
</details>

<details>
<summary><strong>Cursor / Windsurf / Claude Desktop / Generic</strong></summary>

```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": ["/absolute/path/to/Packages/mcp-unity/Server~/build/index.js"]
    }
  }
}
```
</details>

<details>
<summary><strong>Claude Code</strong></summary>

```bash
claude mcp add mcp-unity node /absolute/path/to/Packages/mcp-unity/Server~/build/index.js
```
</details>

## Available Tools

<details>
<summary><strong>Scene Management</strong> — 9 tools</summary>

`create_scene` · `load_scene` · `save_scene` · `delete_scene` · `unload_scene` · `get_scene_info` · `scene_set_active` · `scene_get_data` · `scene_list_opened`
</details>

<details>
<summary><strong>GameObjects</strong> — 8 tools</summary>

`gameobject_create` · `gameobject_find` · `select_gameobject` · `update_gameobject` · `duplicate_gameobject` · `delete_gameobject` · `reparent_gameobject` · `get_gameobject`
</details>

<details>
<summary><strong>Transform</strong> — 4 tools</summary>

`move_gameobject` · `rotate_gameobject` · `scale_gameobject` · `set_transform`
</details>

<details>
<summary><strong>Components</strong> — 4 tools</summary>

`gameobject_component_get` · `gameobject_component_destroy` · `gameobject_component_list_all` · `update_component`
</details>

<details>
<summary><strong>Assets</strong> — 10 tools</summary>

`assets_find` · `assets_find_built_in` · `assets_get_data` · `assets_create_folder` · `assets_copy` · `assets_move` · `assets_delete` · `assets_modify` · `assets_refresh` · `add_asset_to_scene`
</details>

<details>
<summary><strong>Materials & Shaders</strong> — 5 tools</summary>

`create_material` · `assign_material` · `modify_material` · `get_material_info` · `assets_shader_list_all`
</details>

<details>
<summary><strong>Prefabs</strong> — 6 tools</summary>

`create_prefab` · `prefab_create_from_scene` · `prefab_open` · `prefab_close` · `prefab_save` · `prefab_get_hierarchy`
</details>

<details>
<summary><strong>Screenshots</strong> — 2 tools ✨</summary>

`screenshot_scene_view` · `screenshot_game_view`
</details>

<details>
<summary><strong>Scripts</strong> — 3 tools ✨</summary>

`recompile_scripts` · `script_read` · `script_update_or_create`
</details>

<details>
<summary><strong>Profiler</strong> — 8 tools</summary>

`profiler_start` · `profiler_stop` · `profiler_get_status` · `profiler_get_memory_stats` · `profiler_capture_frame` · `profiler_status` · `profiler_enable_recording` · `profiler_get_selected_frame`
</details>

<details>
<summary><strong>Editor & Console</strong> — 7 tools</summary>

`execute_menu_item` · `editor_application_get_state` · `editor_application_set_state` · `editor_selection_get` · `get_console_logs` · `console_clear_logs` · `send_console_log`
</details>

<details>
<summary><strong>Reflection & Types</strong> — 3 tools</summary>

`reflection_method_find` · `reflection_method_call` · `type_get_json_schema`
</details>

<details>
<summary><strong>Package Manager</strong> — 4 tools</summary>

`add_package` · `package_list` · `package_remove` · `package_search`
</details>

<details>
<summary><strong>Object</strong> — 2 tools</summary>

`object_get_data` · `object_modify`
</details>

<details>
<summary><strong>Testing & Batch</strong> — 2 tools</summary>

`run_tests` · `batch_execute`
</details>

> **77 tools total.** See [docs/TOOLS.md](docs/TOOLS.md) for full descriptions, parameters, and example prompts.

## Requirements

- **Unity** 2022.3 or newer
- **Node.js** 18 or newer
- An **MCP client**: OpenCode, Cursor, Windsurf, Claude Code, Claude Desktop, GitHub Copilot, Codex CLI, or any MCP-compatible tool

## Extending

Adding a new tool follows a simple pattern — C# handler on the Unity side, TypeScript registration on the Node side.

<details>
<summary><strong>Step-by-step: Add a tool</strong></summary>

**1. Unity (C#)** — Create `Editor/Tools/YourTool.cs`:

```csharp
namespace McpUnity.Tools
{
    public class YourTool : McpToolBase
    {
        public YourTool()
        {
            Name = "your_tool";
            Description = "Does something useful";
        }

        public override JObject Execute(JObject parameters)
        {
            // Your logic here
            return new JObject { ["success"] = true, ["message"] = "Done!" };
        }
    }
}
```

**2. Register** in `Editor/UnityBridge/McpUnityServer.cs` → `RegisterTools()`:
```csharp
AddTool(new YourTool());
```

**3. Node (TypeScript)** — Create `Server~/src/tools/yourTool.ts`:

```typescript
import { z } from 'zod';

export function registerYourTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  server.tool('your_tool', 'Does something useful', {}, async () => {
    return await mcpUnity.sendRequest({ method: 'your_tool', params: {} });
  });
}
```

**4. Register** in `Server~/src/index.ts` and run `npm run build`.

Tool names must match **exactly** between C# and TypeScript.
</details>

## Project Structure

```
Packages/mcp-unity/
├── Editor/                          C# Unity Editor package
│   ├── Tools/                       Tool handlers (McpToolBase)
│   ├── Resources/                   Resource handlers
│   ├── UnityBridge/                 WebSocket server + routing
│   └── Utils/                       MainThreadDispatcher, ToolErrors, Logger
├── Server~/                         Node.js MCP server (TypeScript)
│   ├── src/index.ts                 Tool/resource/prompt registration
│   ├── src/tools/                   Tool definitions (Zod schemas)
│   ├── src/__tests__/               Jest test suite (198 tests)
│   └── src/unity/                   WebSocket client + connection
├── package.json                     Unity package manifest
└── server.json                      MCP registry metadata
```

## Quality

- **198 tests** — Unit tests for every tool, resource, and connection handler
- **CI/CD** — GitHub Actions runs build + test on every push
- **Type-safe** — Strict TypeScript on Node side, strongly-typed C# on Unity side
- **Zero runtime dependencies** on the Unity side — only editor APIs

## Troubleshooting

| Problem | Solution |
|---|---|
| Agent times out | This fork fixes it — `MainThreadDispatcher` works without Unity focus. Verify port `8090` is open. |
| `unknown_method` error | Tool name mismatch between Node and Unity. Names must match exactly. |
| Scene View not updating | This fork auto-repaints after every mutation. |
| Package won't install | Git URL must include `?path=/Packages/mcp-unity` |
| Node not found | Install Node.js 18+, ensure `node` and `npm` are on your PATH |

## Contributing

Contributions welcome! Here's how:

1. **Fork** this repository
2. **Create** a feature branch (`git checkout -b my-feature`)
3. **Make** your changes — follow the [Extending](#extending) guide for new tools
4. **Test** your changes (`cd Packages/mcp-unity/Server~ && npm test`)
5. **Submit** a pull request

For architecture details, see [AGENTS.md](Packages/mcp-unity/AGENTS.md).

## License

[MIT](LICENSE)
