import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'assets_refresh';
const toolDescription = 'Refreshes the Unity AssetDatabase with optional import options';

const paramsSchema = z.object({
  option: z.enum(['Default', 'ForceUpdate', 'ForceSynchronousImport', 'ImportRecursive']).default('Default').describe('The AssetDatabase refresh option to use')
});

export function registerAssetsRefreshTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>) {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      option: params.option ?? 'Default'
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to refresh assets'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || `Successfully refreshed assets using ${response.option || params.option}`
    }],
    data: {
      option: response.option ?? params.option
    }
  };
}
