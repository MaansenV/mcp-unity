// Import MCP SDK components
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { McpUnity } from './unity/mcpUnity.js';
import { Logger, LogLevel } from './utils/logger.js';
import { registerCreateSceneTool } from './tools/createSceneTool.js';
import { registerMenuItemTool } from './tools/menuItemTool.js';
import { registerSelectGameObjectTool } from './tools/selectGameObjectTool.js';
import { registerAddPackageTool } from './tools/addPackageTool.js';
import { registerPackageListTool } from './tools/packageListTool.js';
import { registerPackageRemoveTool } from './tools/packageRemoveTool.js';
import { registerPackageSearchTool } from './tools/packageSearchTool.js';
import { registerRunTestsTool } from './tools/runTestsTool.js';
import { registerSendConsoleLogTool } from './tools/sendConsoleLogTool.js';
import { registerGetConsoleLogsTool } from './tools/getConsoleLogsTool.js';
import { registerUpdateComponentTool } from './tools/updateComponentTool.js';
import { registerAddAssetToSceneTool } from './tools/addAssetToSceneTool.js';
import { registerUpdateGameObjectTool } from './tools/updateGameObjectTool.js';
import { registerCreatePrefabTool } from './tools/createPrefabTool.js';
import { registerDeleteSceneTool } from './tools/deleteSceneTool.js';
import { registerLoadSceneTool } from './tools/loadSceneTool.js';
import { registerSaveSceneTool } from './tools/saveSceneTool.js';
import { registerGetSceneInfoTool } from './tools/getSceneInfoTool.js';
import { registerUnloadSceneTool } from './tools/unloadSceneTool.js';
import { registerRecompileScriptsTool } from './tools/recompileScriptsTool.js';
import { registerGetGameObjectTool } from './tools/getGameObjectTool.js';
import { registerTransformTools } from './tools/transformTools.js';
import { registerCreateMaterialTool, registerAssignMaterialTool, registerModifyMaterialTool, registerGetMaterialInfoTool } from './tools/materialTools.js';
import { registerDuplicateGameObjectTool, registerDeleteGameObjectTool, registerReparentGameObjectTool } from './tools/gameObjectTools.js';
import { registerBatchExecuteTool } from './tools/batchExecuteTool.js';
import { registerAssetsFindTool } from './tools/assetsFindTool.js';
import { registerAssetsFindBuiltInTool } from './tools/assetsFindBuiltInTool.js';
import { registerAssetsGetDataTool } from './tools/assetsGetDataTool.js';
import { registerAssetsCreateFolderTool } from './tools/assetsCreateFolderTool.js';
import { registerAssetsCopyTool } from './tools/assetsCopyTool.js';
import { registerAssetsRefreshTool } from './tools/assetsRefreshTool.js';
import { registerAssetsMoveTool } from './tools/assetsMoveTool.js';
import { registerAssetsDeleteTool } from './tools/assetsDeleteTool.js';
import { registerAssetsModifyTool } from './tools/assetsModifyTool.js';
import { registerGameObjectFindTool } from './tools/gameObjectFindTool.js';
import { registerGameObjectCreateTool } from './tools/gameObjectCreateTool.js';
import { registerGameObjectComponentDestroyTool } from './tools/gameObjectComponentDestroyTool.js';
import { registerGameObjectComponentGetDataTool } from './tools/gameObjectComponentGetDataTool.js';
import { registerGameObjectComponentListAllTool } from './tools/gameObjectComponentListAllTool.js';
import { registerObjectGetDataTool } from './tools/objectGetDataTool.js';
import { registerObjectModifyTool } from './tools/objectModifyTool.js';
import { registerPrefabCreateFromSceneTool } from './tools/prefabCreateFromSceneTool.js';
import { registerPrefabOpenTool } from './tools/prefabOpenTool.js';
import { registerPrefabCloseTool } from './tools/prefabCloseTool.js';
import { registerPrefabSaveTool } from './tools/prefabSaveTool.js';
import { registerPrefabGetHierarchyTool } from './tools/prefabGetHierarchyTool.js';
import { registerConsoleClearLogsTool } from './tools/consoleClearLogsTool.js';
import { registerEditorApplicationGetStateTool } from './tools/editorApplicationGetStateTool.js';
import { registerEditorApplicationSetStateTool } from './tools/editorApplicationSetStateTool.js';
import { registerEditorSelectionGetTool } from './tools/editorSelectionGetTool.js';
import { registerProfilerStartTool } from './tools/profilerStartTool.js';
import { registerProfilerStopTool } from './tools/profilerStopTool.js';
import { registerProfilerGetStatusTool } from './tools/profilerGetStatusTool.js';
import { registerProfilerGetMemoryStatsTool } from './tools/profilerGetMemoryStatsTool.js';
import { registerProfilerCaptureFrameTool } from './tools/profilerCaptureFrameTool.js';
import { registerProfilerStatusTool } from './tools/profilerStatusTool.js';
import { registerProfilerEnableRecordingTool } from './tools/profilerEnableRecordingTool.js';
import { registerProfilerGetSelectedFrameTool } from './tools/profilerGetSelectedFrameTool.js';
import { registerReflectionMethodFindTool } from './tools/reflectionMethodFindTool.js';
import { registerReflectionMethodCallTool } from './tools/reflectionMethodCallTool.js';
import { registerTypeGetJsonSchemaTool } from './tools/typeGetJsonSchemaTool.js';
import { registerSceneSetActiveTool } from './tools/sceneSetActiveTool.js';
import { registerSceneGetDataTool } from './tools/sceneGetDataTool.js';
import { registerSceneListOpenedTool } from './tools/sceneListOpenedTool.js';
import { registerAssetsShaderListAllTool } from './tools/assetsShaderListAllTool.js';
import { registerGetMenuItemsResource } from './resources/getMenuItemResource.js';
import { registerGetConsoleLogsResource } from './resources/getConsoleLogsResource.js';
import { registerGetHierarchyResource } from './resources/getScenesHierarchyResource.js';
import { registerGetPackagesResource } from './resources/getPackagesResource.js';
import { registerGetAssetsResource } from './resources/getAssetsResource.js';
import { registerGetTestsResource } from './resources/getTestsResource.js';
import { registerGetGameObjectResource } from './resources/getGameObjectResource.js';
import { registerGameObjectHandlingPrompt } from './prompts/gameobjectHandlingPrompt.js';

// Initialize loggers
const serverLogger = new Logger('Server', LogLevel.INFO);
const unityLogger = new Logger('Unity', LogLevel.INFO);
const toolLogger = new Logger('Tools', LogLevel.INFO);
const resourceLogger = new Logger('Resources', LogLevel.INFO);

// Initialize the MCP server
const server = new McpServer (
  {
    name: "MCP Unity Server",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {},
      resources: {},
      prompts: {},
    },
  }
);

// Initialize MCP HTTP bridge with Unity editor
const mcpUnity = new McpUnity(unityLogger);

// Register all tools into the MCP server
registerMenuItemTool(server, mcpUnity, toolLogger);
registerSelectGameObjectTool(server, mcpUnity, toolLogger);
registerAddPackageTool(server, mcpUnity, toolLogger);
registerPackageListTool(server, mcpUnity, toolLogger);
registerPackageRemoveTool(server, mcpUnity, toolLogger);
registerPackageSearchTool(server, mcpUnity, toolLogger);
registerRunTestsTool(server, mcpUnity, toolLogger);
registerSendConsoleLogTool(server, mcpUnity, toolLogger);
registerGetConsoleLogsTool(server, mcpUnity, toolLogger);
registerUpdateComponentTool(server, mcpUnity, toolLogger);
registerAddAssetToSceneTool(server, mcpUnity, toolLogger);
registerUpdateGameObjectTool(server, mcpUnity, toolLogger);
registerCreatePrefabTool(server, mcpUnity, toolLogger);
registerCreateSceneTool(server, mcpUnity, toolLogger);
registerDeleteSceneTool(server, mcpUnity, toolLogger);
registerLoadSceneTool(server, mcpUnity, toolLogger);
registerSaveSceneTool(server, mcpUnity, toolLogger);
registerGetSceneInfoTool(server, mcpUnity, toolLogger);
registerUnloadSceneTool(server, mcpUnity, toolLogger);
registerRecompileScriptsTool(server, mcpUnity, toolLogger);
registerGetGameObjectTool(server, mcpUnity, toolLogger);
registerTransformTools(server, mcpUnity, toolLogger);
registerDuplicateGameObjectTool(server, mcpUnity, toolLogger);
registerDeleteGameObjectTool(server, mcpUnity, toolLogger);
registerReparentGameObjectTool(server, mcpUnity, toolLogger);

// Register Material Tools
registerCreateMaterialTool(server, mcpUnity, toolLogger);
registerAssignMaterialTool(server, mcpUnity, toolLogger);
registerModifyMaterialTool(server, mcpUnity, toolLogger);
registerGetMaterialInfoTool(server, mcpUnity, toolLogger);

// Register Batch Execute Tool (high-priority for performance)
registerBatchExecuteTool(server, mcpUnity, toolLogger);

// Register Asset CRUD tools
registerAssetsFindTool(server, mcpUnity, toolLogger);
registerAssetsFindBuiltInTool(server, mcpUnity, toolLogger);
registerAssetsGetDataTool(server, mcpUnity, toolLogger);
registerAssetsCreateFolderTool(server, mcpUnity, toolLogger);
registerAssetsCopyTool(server, mcpUnity, toolLogger);
registerAssetsRefreshTool(server, mcpUnity, toolLogger);
registerAssetsMoveTool(server, mcpUnity, toolLogger);
registerAssetsDeleteTool(server, mcpUnity, toolLogger);
registerAssetsModifyTool(server, mcpUnity, toolLogger);

// Register GameObject/component discovery tools
registerGameObjectFindTool(server, mcpUnity, toolLogger);
registerGameObjectCreateTool(server, mcpUnity, toolLogger);
registerGameObjectComponentDestroyTool(server, mcpUnity, toolLogger);
registerGameObjectComponentGetDataTool(server, mcpUnity, toolLogger);
registerGameObjectComponentListAllTool(server, mcpUnity, toolLogger);

// Register Object get/modify tools
registerObjectGetDataTool(server, mcpUnity, toolLogger);
registerObjectModifyTool(server, mcpUnity, toolLogger);

// Register Prefab workflow tools
registerPrefabCreateFromSceneTool(server, mcpUnity, toolLogger);
registerPrefabOpenTool(server, mcpUnity, toolLogger);
registerPrefabCloseTool(server, mcpUnity, toolLogger);
registerPrefabSaveTool(server, mcpUnity, toolLogger);
registerPrefabGetHierarchyTool(server, mcpUnity, toolLogger);

// Register Console/Editor tools
registerConsoleClearLogsTool(server, mcpUnity, toolLogger);
registerEditorApplicationGetStateTool(server, mcpUnity, toolLogger);
registerEditorApplicationSetStateTool(server, mcpUnity, toolLogger);
registerEditorSelectionGetTool(server, mcpUnity, toolLogger);

// Register Profiler tools
registerProfilerStartTool(server, mcpUnity, toolLogger);
registerProfilerStopTool(server, mcpUnity, toolLogger);
registerProfilerGetStatusTool(server, mcpUnity, toolLogger);
registerProfilerGetMemoryStatsTool(server, mcpUnity, toolLogger);
registerProfilerCaptureFrameTool(server, mcpUnity, toolLogger);

// Register Profiler History tools
registerProfilerStatusTool(server, mcpUnity, toolLogger);
registerProfilerEnableRecordingTool(server, mcpUnity, toolLogger);
registerProfilerGetSelectedFrameTool(server, mcpUnity, toolLogger);

// Register Reflection tools
registerReflectionMethodFindTool(server, mcpUnity, toolLogger);
registerReflectionMethodCallTool(server, mcpUnity, toolLogger);
registerTypeGetJsonSchemaTool(server, mcpUnity, toolLogger);

// Register Scene tools
registerSceneSetActiveTool(server, mcpUnity, toolLogger);
registerSceneGetDataTool(server, mcpUnity, toolLogger);
registerSceneListOpenedTool(server, mcpUnity, toolLogger);

// Register Shader tools
registerAssetsShaderListAllTool(server, mcpUnity, toolLogger);

// Register all resources into the MCP server
registerGetTestsResource(server, mcpUnity, resourceLogger);
registerGetGameObjectResource(server, mcpUnity, resourceLogger);
registerGetMenuItemsResource(server, mcpUnity, resourceLogger);
registerGetConsoleLogsResource(server, mcpUnity, resourceLogger);
registerGetHierarchyResource(server, mcpUnity, resourceLogger);
registerGetPackagesResource(server, mcpUnity, resourceLogger);
registerGetAssetsResource(server, mcpUnity, resourceLogger);

// Register all prompts into the MCP server
registerGameObjectHandlingPrompt(server);

// Server startup function
async function startServer() {
  try {
    // Initialize STDIO transport for MCP client communication
    const stdioTransport = new StdioServerTransport();
    
    // Connect the server to the transport
    await server.connect(stdioTransport);

    serverLogger.info('MCP Server started');
    
    // Get the client name from the MCP server
    const clientName = server.server.getClientVersion()?.name || 'Unknown MCP Client';
    serverLogger.info(`Connected MCP client: ${clientName}`);
    
    // Start Unity Bridge connection with client name in headers
    await mcpUnity.start(clientName);
    
  } catch (error) {
    serverLogger.error('Failed to start server', error);
    process.exit(1);
  }
}

// Graceful shutdown handler
let isShuttingDown = false;
async function shutdown() {
  if (isShuttingDown) return;
  isShuttingDown = true;

  try {
    serverLogger.info('Shutting down...');
    await mcpUnity.stop();
    await server.close();
  } catch (error) {
    // Ignore errors during shutdown
  }
  process.exit(0);
}

// Start the server
startServer();

// Handle shutdown signals
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
process.on('SIGHUP', shutdown);

// Handle stdin close (when MCP client disconnects)
process.stdin.on('close', shutdown);
process.stdin.on('end', shutdown);
process.stdin.on('error', shutdown);

// Handle uncaught exceptions - exit cleanly if it's just a closed pipe
process.on('uncaughtException', (error: NodeJS.ErrnoException) => {
  // EPIPE/EOF errors are expected when the MCP client disconnects
  if (error.code === 'EPIPE' || error.code === 'EOF' || error.code === 'ERR_USE_AFTER_CLOSE') {
    shutdown();
    return;
  }
  serverLogger.error('Uncaught exception', error);
  process.exit(1);
});

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason) => {
  serverLogger.error('Unhandled rejection', reason);
  process.exit(1);
});
