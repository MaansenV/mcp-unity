import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'reflection_method_find';
const toolDescription = 'Searches loaded assemblies for methods matching optional type, method, and text filters.';

const paramsSchema = z.object({
  typeName: z.string().optional().describe('Optional type name filter'),
  methodName: z.string().optional().describe('Optional method name filter'),
  search: z.string().optional().describe('Optional search text filter'),
  maxResults: z.number().int().positive().max(100).default(20).describe('Maximum number of methods to return (1-100, default: 20)')
});

export function registerReflectionMethodFindTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

async function toolHandler(mcpUnity: McpUnity, params: any) {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to find reflection methods'
    );
  }

  return {
    content: [{
      type: response.type,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
