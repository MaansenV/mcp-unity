import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'prefab_create_from_scene';
const toolDescription = 'Creates a prefab asset from a scene GameObject by instance ID or hierarchy path.';

const paramsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject'),
  objectPath: z.string().optional().describe('The hierarchy path of the GameObject'),
  prefabPath: z.string().describe("Destination prefab asset path (e.g. 'Assets/Prefabs/MyPrefab.prefab')")
});

export function registerPrefabCreateFromSceneTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const { instanceId, objectPath, prefabPath } = params;

  if (instanceId === undefined && (!objectPath || objectPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'instanceId' or 'objectPath' must be provided"
    );
  }

  if (!prefabPath || prefabPath.trim() === '') {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'prefabPath' not provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { instanceId, objectPath, prefabPath }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to create prefab from scene object'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || 'Successfully created prefab asset'
    }],
    data: {
      prefabPath: response.prefabPath,
      guid: response.guid,
      instanceId: response.instanceId
    }
  };
}
