import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'assets_delete';
const toolDescription = 'Deletes one or more assets from the Unity AssetDatabase';

const paramsSchema = z.object({
  path: z.string().optional().describe('A single asset path to delete'),
  paths: z.array(z.string()).max(50).optional().describe('Multiple asset paths to delete (max 50)'),
  confirmDelete: z.boolean().default(false).describe('Must be true to confirm deletion')
});

export function registerAssetsDeleteTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  if (params.confirmDelete !== true) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Deletion requires confirmDelete to be true"
    );
  }

  if (!params.path && (!params.paths || params.paths.length === 0)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Provide either 'path' or 'paths'"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      path: params.path,
      paths: params.paths,
      confirmDelete: true
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to delete assets'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'Successfully deleted assets'
    }],
    data: {
      deleted: response.deleted,
      failed: response.failed,
      summary: response.summary
    }
  };
}
