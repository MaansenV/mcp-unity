import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'package_search';
const packageManagerTimeoutMs = 60000;
const toolDescription = 'Searches for packages in the Unity Package Manager';
const paramsSchema = z.object({
  query: z.string().trim().min(1).optional().describe('Search query string (required unless searchAll is true)'),
  searchAll: z.boolean().optional().default(false).describe('Whether to search all packages without a query (optional)'),
  limit: z.number().int().min(1).max(250).optional().default(50).describe('Maximum number of results to return (1-250, optional)'),
  source: z.enum(['all', 'any', 'registry', 'built_in', 'builtin', 'embedded', 'local', 'git', 'cache']).optional().describe('Optional source filter'),
  includeInstalledState: z.boolean().optional().default(true).describe('Whether to include installed/not_installed state in results (optional)')
});

export function registerPackageSearchTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any = {}) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        return await toolHandler(mcpUnity, params);
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, params: any = {}): Promise<CallToolResult> {
  const { query, searchAll = false, limit = 50, source, includeInstalledState = true } = params;

  const trimmedQuery = query?.trim();

  if (!searchAll && !trimmedQuery) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Required parameter "query" not provided when "searchAll" is false'
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { query: trimmedQuery, searchAll, limit, source, includeInstalledState }
  }, {
    timeout: packageManagerTimeoutMs
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to search packages'
    );
  }

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
