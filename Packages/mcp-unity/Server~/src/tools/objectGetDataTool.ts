import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'object_get_data';
const toolDescription = 'Gets metadata and optional serialized data for any UnityEngine.Object by instance ID.';
const paramsSchema = z.object({
  instanceId: z.number().int().describe('The instance ID of the UnityEngine.Object'),
  includeSerializedProperties: z.boolean().optional().default(true).describe('Whether to include serialized property data (default: true)'),
  maxProperties: z.number().int().positive().max(500).optional().default(100).describe('Maximum number of serialized properties to return (1-500, default: 100)')
});

export function registerObjectGetDataTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const { instanceId, includeSerializedProperties = true, maxProperties = 100 } = params;

  if (typeof instanceId !== 'number') {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'instanceId' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { instanceId, includeSerializedProperties, maxProperties }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to retrieve object data'
    );
  }

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
