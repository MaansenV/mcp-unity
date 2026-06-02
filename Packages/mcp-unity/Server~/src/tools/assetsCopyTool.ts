import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'assets_copy';
const toolDescription = 'Copies one or more assets within the Unity AssetDatabase';

const copySchema = z.object({
  srcPath: z.string().describe('The source asset path'),
  destPath: z.string().describe('The destination asset path')
});

const paramsSchema = z.object({
  srcPath: z.string().optional().describe('The source asset path'),
  destPath: z.string().optional().describe('The destination asset path'),
  copies: z.array(copySchema).max(50, 'Maximum of 50 copies allowed per batch').optional().describe('Batch copy requests')
});

export function registerAssetsCopyTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  if (!params.srcPath && !params.destPath && (!params.copies || params.copies.length === 0)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Provide either 'srcPath' and 'destPath', or a non-empty 'copies' array"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to copy asset(s)'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
