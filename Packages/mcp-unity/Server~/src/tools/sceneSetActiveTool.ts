import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'scene_set_active';
const toolDescription = 'Finds a scene by name or path and sets it as the active scene.';

const paramsSchema = z.object({
  sceneName: z.string().optional().describe('Scene name to activate'),
  scenePath: z.string().optional().describe('Scene path to activate')
});

export function registerSceneSetActiveTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  if (!params.sceneName && !params.scenePath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'sceneName' or 'scenePath' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to set active scene'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'Successfully set active scene'
    }],
    data: {
      scene: response.scene
    }
  };
}
