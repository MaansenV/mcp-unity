import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'assets_move';
const toolDescription = 'Moves one or more assets within the Unity AssetDatabase';

const moveItemSchema = z.object({
  srcPath: z.string().describe('The source asset path to move'),
  destPath: z.string().describe('The destination asset path')
});

const paramsSchema = z.object({
  srcPath: z.string().optional().describe('The source asset path to move'),
  destPath: z.string().optional().describe('The destination asset path'),
  moves: z.array(moveItemSchema).max(50).optional().describe('Batch move operations, up to 50 items')
});

export function registerAssetsMoveTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const hasBatch = Array.isArray(params.moves) && params.moves.length > 0;
  const hasSingle = Boolean(params.srcPath || params.destPath);

  if (!hasBatch && !hasSingle) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Provide either 'srcPath' and 'destPath' or a 'moves' array"
    );
  }

  if (hasSingle && (!params.srcPath || !params.destPath)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Both 'srcPath' and 'destPath' must be provided for single asset moves"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: hasBatch
      ? { moves: params.moves }
      : { srcPath: params.srcPath, destPath: params.destPath }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to move assets'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'Successfully moved assets'
    }],
    data: {
      results: response.results,
      summary: response.summary
    }
  };
}
