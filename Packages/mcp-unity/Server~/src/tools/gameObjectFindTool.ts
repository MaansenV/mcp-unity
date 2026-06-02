import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';

const toolName = 'gameobject_find';
const toolDescription = 'Finds GameObjects by partial name match, exact tag match, or component type across loaded scenes.';

const paramsSchema = z.object({
  name: z.string().optional().describe('Partial, case-insensitive GameObject name match'),
  tag: z.string().optional().describe('Exact GameObject tag match'),
  componentType: z.string().optional().describe('Component type name to search for, such as Rigidbody'),
  maxResults: z.number().int().positive().max(100).default(20).describe('Maximum number of results to return (1-100)')
});

export function registerGameObjectFindTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>
): Promise<CallToolResult> {
  const { name, tag, componentType, maxResults } = params;

  if (!name && !tag && !componentType) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'At least one of name, tag, or componentType must be provided'
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      name,
      tag,
      componentType,
      maxResults,
    },
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to find GameObjects'
    );
  }

  return {
    content: [
      {
        type: 'text',
        text: JSON.stringify(response, null, 2),
      },
    ],
  };
}
