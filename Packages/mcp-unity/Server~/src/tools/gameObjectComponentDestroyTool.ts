import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'gameobject_component_destroy';
const toolDescription = 'Removes a component from a GameObject by instance ID or hierarchy path.';
const paramsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject'),
  objectPath: z.string().optional().describe('The hierarchy path of the GameObject'),
  componentName: z.string().describe('The name of the component to remove')
});

export function registerGameObjectComponentDestroyTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const { instanceId, objectPath, componentName } = params;

  if (instanceId === undefined && (!objectPath || objectPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'instanceId' or 'objectPath' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { instanceId, objectPath, componentName }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to remove component from GameObject'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || 'Successfully removed component from GameObject'
    }]
  };
}
