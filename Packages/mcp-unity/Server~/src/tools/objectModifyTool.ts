import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'object_modify';
const toolDescription = 'Modifies serialized properties of any UnityEngine.Object by instance ID.';

const paramsSchema = z.object({
  instanceId: z.number().int().describe('The instance ID of the UnityEngine.Object'),
  properties: z.record(z.string(), z.any()).describe('Mapping of serialized property paths to new values')
});

export function registerObjectModifyTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const { instanceId, properties } = params;

  if (typeof instanceId !== 'number') {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'instanceId' must be provided"
    );
  }

  if (!properties || Object.keys(properties).length === 0) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'properties' must be provided and non-empty"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { instanceId, properties }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to modify object'
    );
  }

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
