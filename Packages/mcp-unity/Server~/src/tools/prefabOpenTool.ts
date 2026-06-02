import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'prefab_open';
const toolDescription = 'Opens a prefab asset in Prefab Mode.';

const paramsSchema = z.object({
  prefabPath: z.string().describe('The path of the prefab asset to open')
});

export function registerPrefabOpenTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  if (!params.prefabPath || params.prefabPath.trim() === '') {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'prefabPath' not provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { prefabPath: params.prefabPath }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to open prefab'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || 'Successfully opened prefab'
    }],
    data: {
      prefabPath: response.prefabPath
    }
  };
}
