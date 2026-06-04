import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'get_test_job_status';
const toolDescription = 'Gets status and result for a persisted Unity test job. Use this to poll test job status after the Unity editor reconnects following a domain reload or play mode transition.';
const paramsSchema = z.object({
  jobId: z.string().describe('The test job ID to query status for')
});

export function registerGetTestJobStatusTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any = {}) => {
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

async function toolHandler(mcpUnity: McpUnity, params: any = {}): Promise<CallToolResult> {
  const { jobId } = params;

  if (!jobId) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Missing required parameter: jobId'
    );
  }

  const response = await mcpUnity.sendRequest(
    {
      method: toolName,
      params: { jobId }
    },
    { timeout: 30_000 }
  );

  if (!response.success && response.status === 'notFound') {
    return {
      content: [
        {
          type: 'text',
          text: `Test job not found: ${jobId}`
        }
      ],
      isError: true
    };
  }

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to get test job status`
    );
  }

  // Format the response
  const status = response.status;
  const message = response.message || `Job ${jobId}: ${status}`;

  return {
    content: [
      {
        type: 'text',
        text: message
      },
      {
        type: 'text',
        text: JSON.stringify(response, null, 2)
      }
    ]
  };
}
