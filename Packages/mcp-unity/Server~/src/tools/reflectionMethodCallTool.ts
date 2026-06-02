import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'reflection_method_call';
const toolDescription = 'Finds a type and method by name, then invokes it with optional parameters.';

const paramsSchema = z.object({
  typeName: z.string().describe('The type name to search for'),
  methodName: z.string().describe('The method name to invoke'),
  parameters: z.array(z.any()).optional().describe('Optional parameter values'),
  instanceId: z.number().int().optional().describe('Optional instance ID for instance methods')
});

export function registerReflectionMethodCallTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to invoke reflection method'
    );
  }

  return {
    content: [{
      type: response.type,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
