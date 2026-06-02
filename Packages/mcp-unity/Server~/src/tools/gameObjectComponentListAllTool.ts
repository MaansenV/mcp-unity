import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'gameobject_component_list_all';
const toolDescription = 'Lists all available Component types across loaded assemblies, with optional search filtering and pagination.';
const paramsSchema = z.object({
  search: z.string().optional().describe('Optional case-insensitive name filter'),
  page: z.number().int().positive().default(1).describe('Page number (positive integer, default: 1)'),
  pageSize: z.number().int().positive().max(200).default(50).describe('Page size (1-200, default: 50)')
});

export function registerGameObjectComponentListAllTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      search: params.search,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 50
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to list component types'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
