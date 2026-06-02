import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'scene_get_data';
const toolDescription = 'Gets scene data including root objects and basic state. Defaults to the active scene.';

const paramsSchema = z.object({
  sceneName: z.string().optional().describe('Optional scene name to inspect'),
  scenePath: z.string().optional().describe('Optional scene path to inspect')
});

export function registerSceneGetDataTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
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

async function toolHandler(mcpUnity: McpUnity, params: any) {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to get scene data'
    );
  }

  return {
    content: [{
      type: response.type,
      text: JSON.stringify(response, null, 2)
    }],
    data: {
      scene: response.scene
    }
  };
}
