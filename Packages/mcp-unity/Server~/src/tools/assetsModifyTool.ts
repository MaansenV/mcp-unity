import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'assets_modify';
const toolDescription = 'Modifies serialized properties of a Unity asset. Blocks modification of built-in and Packages/ assets.';

const paramsSchema = z.object({
  assetPath: z.string().optional().describe('The path of the asset in the AssetDatabase'),
  guid: z.string().optional().describe('The GUID of the asset'),
  properties: z.record(z.string(), z.any()).describe('Mapping of serialized property paths to new values')
});

export function registerAssetsModifyTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const { assetPath, guid, properties } = params;

  if (!assetPath && !guid) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'assetPath' or 'guid' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { assetPath, guid, properties }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to modify asset'
    );
  }

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
