import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'prefab_save';
const toolDescription = 'Saves a prefab asset directly, applies prefab instance overrides, or saves a scene object as a prefab asset.';

const paramsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject'),
  objectPath: z.string().optional().describe('The hierarchy path of the GameObject'),
  prefabPath: z.string().optional().describe("The prefab asset path to save to or update (e.g. 'Assets/Prefabs/MyPrefab.prefab')"),
  applyOverrides: z.boolean().default(false).describe('If true, apply prefab instance overrides instead of saving a prefab asset')
});

export function registerPrefabSaveTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  const { instanceId, objectPath, prefabPath, applyOverrides } = params;

  if (instanceId === undefined && (!objectPath || objectPath.trim() === '') && (!prefabPath || prefabPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Provide either 'prefabPath' or 'instanceId'/'objectPath'"
    );
  }

  if (applyOverrides && instanceId === undefined && (!objectPath || objectPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'instanceId' or 'objectPath' must be provided when 'applyOverrides' is true"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { instanceId, objectPath, prefabPath, applyOverrides }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to save prefab'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || 'Successfully saved prefab'
    }],
    data: {
      prefabPath: response.prefabPath,
      guid: response.guid,
      instanceId: response.instanceId,
      objectPath: response.objectPath
    }
  };
}
