# MCP Unity – Tools & Resources Reference

> Generated from the live tool/resource registration in
> `Packages/mcp-unity/Server~/src/index.ts`, the tool schemas in
> `Server~/src/tools/`, the resource templates in `Server~/src/resources/`,
> and the AGENTS guide shipped with the package.
>
> If you add, rename, or remove a tool/resource/prompt, update this page
> (or regenerate it from the source) so the docs stay in sync.

**Last reviewed against source:** 1.3.1 package (Unity `com.gamelovers.mcp-unity`,
Node server `1.3.0`).

---

## At a glance

| Surface | Count | Where it lives |
| --- | ---: | --- |
| MCP **tools** | **68** | `Packages/mcp-unity/Server~/src/tools/` + `Editor/Tools/` |
| MCP **resources** | **7** | `Packages/mcp-unity/Server~/src/resources/` + `Editor/Resources/` |
| MCP **prompts** | **1** | `Packages/mcp-unity/Server~/src/prompts/` |

Tools are grouped below by what they operate on. Names are exactly the
`toolName` constants registered with the MCP server and must match the
`Name` on the Unity side.

### Categories

1. [Scene Management](#scene-management) – 9 tools
2. [GameObject](#gameobject) – 8 tools
3. [Transform](#transform) – 4 tools
4. [Components](#components) – 4 tools
5. [Materials](#materials) – 4 tools
6. [Shaders](#shaders) – 1 tool
7. [Prefabs](#prefabs) – 5 tools
8. [Assets](#assets) – 10 tools
9. [Console](#console) – 3 tools
10. [Editor](#editor) – 4 tools
11. [Profiler](#profiler) – 4 tools
12. [Reflection](#reflection) – 3 tools
13. [Object](#object) – 2 tools
14. [Package Manager](#package-manager) – 4 tools
15. [Test Runner](#test-runner) – 1 tool
16. [Scripts](#scripts) – 1 tool
17. [Batch](#batch) – 1 tool
18. [Resources](#resources) – 7 resources
19. [Prompts](#prompts) – 1 prompt
20. [Usage notes](#usage-notes)

---

## Scene Management

Create, load, save, unload, and inspect Unity scenes.

| Tool | Description |
| --- | --- |
| `create_scene` | Creates a new scene and saves it to the specified path. |
| `load_scene` | Loads a scene by path or name. Supports additive loading. |
| `save_scene` | Saves the current active scene. Optional Save As. |
| `unload_scene` | Unloads a scene by path or name. Optionally saves dirty scenes first. |
| `delete_scene` | Deletes a scene by path or name and removes it from Build Settings. |
| `scene_set_active` | Finds a scene by name or path and sets it as the active scene. |
| `scene_get_data` | Gets scene data including root objects and basic state. |
| `scene_list_opened` | Lists all currently open scenes in the editor. |
| `get_scene_info` | Gets information about the active scene and all loaded scenes. |

---

## GameObject

Create, find, select, mutate, duplicate, reparent, and delete GameObjects.

| Tool | Description |
| --- | --- |
| `gameobject_create` | Creates a new GameObject in the scene (empty or primitive) with optional parent and transform. |
| `gameobject_find` | Finds GameObjects by partial name, exact tag, or component type. |
| `select_gameobject` | Selects a GameObject in the hierarchy by path, name, or instance ID. |
| `update_gameobject` | Updates a GameObject's core properties or creates it if it does not exist. |
| `duplicate_gameobject` | Duplicates a GameObject with optional renaming, reparenting, and count. |
| `delete_gameobject` | Deletes a GameObject. By default also deletes all children. |
| `reparent_gameobject` | Changes the parent of a GameObject in the hierarchy. |
| `get_gameobject` | Returns detailed info for a GameObject (components + scoped child hierarchy). |

> **Fork note:** this fork wires `Component`, `ScriptableObject`, and
> `GameObject` references in `update_component` via asset paths, scene
> paths, or instance IDs.

---

## Transform

Move, rotate, scale, or set a GameObject's transform in one call.

| Tool | Description |
| --- | --- |
| `move_gameobject` | Moves a GameObject to a new position. Supports world/local space and absolute/relative. |
| `rotate_gameobject` | Rotates a GameObject using Euler angles. Supports world/local and absolute/relative. |
| `scale_gameobject` | Scales a GameObject to a new local scale. |
| `set_transform` | Sets position, rotation, and scale of a GameObject in a single operation. |

---

## Components

Inspect, add, and remove components on GameObjects.

| Tool | Description |
| --- | --- |
| `gameobject_component_get` | Gets serialized component data from a GameObject. |
| `gameobject_component_destroy` | Removes a component from a GameObject. |
| `gameobject_component_list_all` | Lists all available Component types with search and pagination. |
| `update_component` | Updates component fields on a GameObject or adds the component if missing. |

---

## Materials

Create, assign, modify, and inspect Unity materials.

| Tool | Description |
| --- | --- |
| `create_material` | Creates a new material with the specified shader and saves it. Supports base color shorthand. |
| `assign_material` | Assigns a material to a GameObject's Renderer at a specific material slot. |
| `modify_material` | Modifies material properties (colors, floats, textures, vectors). |
| `get_material_info` | Returns full material info including shader and all current property values. |

---

## Shaders

| Tool | Description |
| --- | --- |
| `assets_shader_list_all` | Lists shaders in the project using the AssetDatabase. |

---

## Prefabs

Create, open, save, and close prefabs.

| Tool | Description |
| --- | --- |
| `create_prefab` | Creates a prefab with an optional MonoBehaviour script and serialized field values. |
| `prefab_create_from_scene` | Creates a prefab asset from a scene GameObject by instance ID or hierarchy path. |
| `prefab_open` | Opens a prefab asset in Prefab Mode. |
| `prefab_close` | Closes the current Prefab Stage. Optionally saves changes first. |
| `prefab_save` | Saves a prefab asset directly, applies prefab instance overrides, or saves a scene object as a prefab. |

---

## Assets

Find, copy, move, delete, modify, and refresh assets in the AssetDatabase.

| Tool | Description |
| --- | --- |
| `assets_find` | Searches the AssetDatabase for assets matching the provided filter. |
| `assets_find_built_in` | Finds Unity built-in resources such as shaders and materials. |
| `assets_get_data` | Retrieves asset metadata and serialized properties by path or GUID. |
| `assets_create_folder` | Creates one or more folders in the AssetDatabase. |
| `assets_copy` | Copies one or more assets within the AssetDatabase. |
| `assets_move` | Moves one or more assets within the AssetDatabase. |
| `assets_delete` | Deletes one or more assets from the AssetDatabase (requires `confirmDelete: true`). |
| `assets_modify` | Modifies serialized properties of a Unity asset. Blocks built-in and `Packages/` assets. |
| `assets_refresh` | Refreshes the AssetDatabase with optional import options. |
| `add_asset_to_scene` | Adds an asset from the AssetDatabase to the Unity scene at a position. |

---

## Console

Read and clear the Unity console, and write synthetic messages.

| Tool | Description |
| --- | --- |
| `get_console_logs` | Retrieves Unity console logs with pagination and per-type filtering. |
| `console_clear_logs` | Clears the Unity Editor console. |
| `send_console_log` | Sends a console log message to Unity (`info`, `warning`, `error`). |

---

## Editor

Execute menu items and read or change the editor's high-level state.

| Tool | Description |
| --- | --- |
| `execute_menu_item` | Executes a Unity menu item by path, e.g. `"GameObject/Create Empty"`. |
| `editor_application_get_state` | Retrieves the current editor state (isPlaying, isPaused, isCompiling, ...). |
| `editor_application_set_state` | Sets Unity Editor play and pause state. |
| `editor_selection_get` | Returns the current editor selection details. |

---

## Profiler

| Tool | Description |
| --- | --- |
| `profiler_start` | Enables the Unity Profiler. |
| `profiler_stop` | Disables the Unity Profiler. |
| `profiler_get_status` | Gets the current profiler status and memory usage. |
| `profiler_get_memory_stats` | Gets detailed Unity Profiler memory statistics. |

---

## Reflection

Discover and invoke editor types and methods at runtime.

| Tool | Description |
| --- | --- |
| `reflection_method_find` | Searches loaded assemblies for methods matching optional type/method/text filters. |
| `reflection_method_call` | Finds a type and method by name, then invokes it with optional parameters. |
| `type_get_json_schema` | Builds a simple JSON schema describing a type's public properties and methods. |

---

## Object

Generic UnityEngine.Object access.

| Tool | Description |
| --- | --- |
| `object_get_data` | Gets metadata and optional serialized data for any UnityEngine.Object by instance ID. |
| `object_modify` | Modifies serialized properties of any UnityEngine.Object by instance ID. |

---

## Package Manager

Add, remove, list, and search packages via Unity's Package Manager.

| Tool | Description |
| --- | --- |
| `add_package` | Adds a package from the Unity registry, Git, or disk. |
| `package_list` | Lists packages in the Unity Package Manager with source and indirect flags. |
| `package_remove` | Removes a package from the Unity Package Manager. |
| `package_search` | Searches the Unity Package Manager registry and other sources. |

---

## Test Runner

| Tool | Description |
| --- | --- |
| `run_tests` | Runs Unity's Test Runner tests (`EditMode` or `PlayMode`) with optional filter and log options. |

---

## Scripts

| Tool | Description |
| --- | --- |
| `recompile_scripts` | Recompiles all scripts in the Unity project. Returns compilation logs. |

---

## Batch

| Tool | Description |
| --- | --- |
| `batch_execute` | Executes multiple tool operations in a single batch with optional atomic rollback (10-100x faster for repetitive operations). |

---

## Resources

Resources are read-only views over Unity state. Use them to discover
GameObjects, scenes, menu items, packages, tests, and logs before
calling tools.

| Resource | URI template | Description |
| --- | --- | --- |
| `get_assets` | `unity://assets` | Retrieve assets from the Unity Asset Database. |
| `get_console_logs` | `unity://logs/{logType}?offset={offset}&limit={limit}&includeStackTrace={includeStackTrace}` | Retrieve Unity console logs by type with pagination. |
| `get_gameobject` | `unity://gameobject/{idOrName}` | Retrieve a GameObject by instance ID, name, or hierarchical path. |
| `get_menu_items` | `unity://menu-items` | List of available menu items in Unity to execute. |
| `get_packages` | `unity://packages` | Retrieve all packages from the Unity Package Manager. |
| `get_scenes_hierarchy` | `unity://scenes_hierarchy` | Retrieve all GameObjects in the Unity loaded scenes with their active state. |
| `get_tests` | `unity://tests/{testMode}` | Retrieve tests from Unity's Test Runner (`EditMode`, `PlayMode`, or all). |

---

## Prompts

Reusable MCP prompt templates shipped with the server.

| Prompt | Description |
| --- | --- |
| `gameobject_handling` | Best-practice guidance for safe GameObject creation, selection, and update_component reference wiring on this fork. |

---

## Usage notes

- **Names must match exactly** between Node (`toolName`) and Unity
  (`Name`). Mismatch returns `unknown_method`.
- **GameObject identifiers**: most tools accept `instanceId` or
  `objectPath` (hierarchical path like `"Canvas/Panel/Button"`).
- **Transforms**: `position`, `rotation`, `scale` use a fresh Vector3
  schema per field to avoid local JSON pointer refs that break some
  MCP clients.
- **Long main-thread work** in Unity is dispatched via
  `EditorCoroutineUtility`. Use `IsAsync = true` on heavy tools.
- **Domain reload** stops and restarts the server. Don't rely on
  in-memory state across reloads.
- **Multiplayer Play Mode** clones skip the server; only the main
  editor hosts the bridge.
- **Remote connections**: set `AllowRemoteConnections = true` in Unity
  and `UNITY_HOST = <ip>` in the Node process to bridge across machines.
- **Response size**: large scenes can exceed the 15 MB MCP cap. Use
  `maxDepth`, `includeComponents`, and pagination knobs on the
  GameObject / hierarchy / log tools.
- **Fork changes**:
  - `update_component` resolves Unity object references via asset
    paths, scene paths, or instance IDs.
  - MCP name: `io.github.maansenv/mcp-unity`.
  - Package name: `com.gamelovers.mcp-unity`.

---

## See also

- [Root README](../README.md) – install, setup, architecture
- [Package AGENTS guide](../Packages/mcp-unity/AGENTS.md) – bridge contract, defaults, debugging
- [Package README](../Packages/mcp-unity/README.md) – end-user docs and screenshots
- Tool source: `Packages/mcp-unity/Server~/src/tools/`
- Resource source: `Packages/mcp-unity/Server~/src/resources/`
- Prompt source: `Packages/mcp-unity/Server~/src/prompts/`
- Unity handlers: `Packages/mcp-unity/Editor/Tools/` and `Editor/Resources/`
