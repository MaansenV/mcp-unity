import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'assets_shader_list_all';
const toolDescription = 'Lists shaders in the project using the AssetDatabase.';

const paramsSchema = z.object({
  search: z.string().optional().describe('Optional search text filter'),
  maxResults: z.number().int().positive().max(500).default(50).describe('Maximum number of shaders to return (1-500, default: 50)')
});

export function registerAssetsShaderListAllTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to list shaders'
    );
  }

  return {
    content: [{
      type: response.type,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
