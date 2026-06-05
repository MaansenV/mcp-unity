import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'profiler_enable_recording';
const toolDescription = 'Enable or disable Unity Profiler recording';

const paramsSchema = z.object({
  enabled: z.boolean().default(true).describe('Whether to enable profiler recording')
});

export function registerProfilerEnableRecordingTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to set profiler recording',
      {
        unityErrorCode: response.errorCode,
        details: response.details,
        data: response.data
      }
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: response.message || 'Profiler recording updated'
    }],
    data: response.data
  };
}