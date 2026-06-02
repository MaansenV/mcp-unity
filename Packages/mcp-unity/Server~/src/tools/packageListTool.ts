import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'package_list';
const packageManagerTimeoutMs = 60000;
const toolDescription = 'Lists packages in the Unity Package Manager';
const paramsSchema = z.object({
  includeIndirect: z.boolean().optional().default(true).describe('Whether to include indirect dependencies in the package list (optional)'),
  offlineMode: z.boolean().optional().default(false).describe('Whether to use offline mode for the package list request (optional)'),
  source: z.enum(['all', 'any', 'registry', 'built_in', 'builtin', 'embedded', 'local', 'git', 'cache']).optional().describe('Optional source filter')
});

export function registerPackageListTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const { includeIndirect = true, offlineMode = false, source } = params;
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { includeIndirect, offlineMode, source }
  }, {
    timeout: packageManagerTimeoutMs
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to list packages'
    );
  }

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
