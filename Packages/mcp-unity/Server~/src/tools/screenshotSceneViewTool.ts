import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'screenshot_scene_view';
const toolDescription = 'Captures a screenshot of the Unity Scene View and returns it as a base64-encoded PNG';
const paramsSchema = z.object({
  width: z.number().int().min(1).max(3840).optional().describe('Screenshot width in pixels (default: 1920, max: 3840)'),
  height: z.number().int().min(1).max(3840).optional().describe('Screenshot height in pixels (default: 1080, max: 3840)')
});

export function registerScreenshotSceneViewTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      width: params.width ?? 1920,
      height: params.height ?? 1080
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to capture Scene View screenshot'
    );
  }

  // Return as image content if base64 data is present
  if (response.data && response.mimeType) {
    return {
      content: [{
        type: 'image',
        data: response.data,
        mimeType: response.mimeType
      }]
    };
  }

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
