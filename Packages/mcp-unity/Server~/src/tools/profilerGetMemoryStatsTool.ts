import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'profiler_get_memory_stats';
const toolDescription = 'Gets Unity Profiler memory statistics';

const paramsSchema = z.object({});

export function registerProfilerGetMemoryStatsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to get profiler memory statistics'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'Retrieved profiler memory statistics'
    }],
    data: {
      totalAllocatedMemoryMB: response.totalAllocatedMemoryMB,
      totalReservedMemoryMB: response.totalReservedMemoryMB,
      totalUnusedReservedMemoryMB: response.totalUnusedReservedMemoryMB,
      monoUsedSizeMB: response.monoUsedSizeMB,
      monoHeapSizeMB: response.monoHeapSizeMB,
      tempAllocatorSizeMB: response.tempAllocatorSizeMB
    }
  };
}
