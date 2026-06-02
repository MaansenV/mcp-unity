import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'editor_application_set_state';
const toolDescription = 'Sets Unity Editor application play and pause state';
const paramsSchema = z.object({
  isPlaying: z.boolean().optional().describe('Whether the Unity Editor should be in play mode'),
  isPaused: z.boolean().optional().describe('Whether the Unity Editor should be paused')
});

export function registerEditorApplicationSetStateTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      ...(params.isPlaying !== undefined ? { isPlaying: params.isPlaying } : {}),
      ...(params.isPaused !== undefined ? { isPaused: params.isPaused } : {})
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to update Unity Editor application state'
    );
  }

  return {
    content: [{
      type: response.type,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
