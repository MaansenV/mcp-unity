import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'run_tests';
const toolDescription = 'Runs Unity\'s Test Runner tests. Starts a persistent job that survives domain reloads. Use get_test_job_status to poll for results.';
const paramsSchema = z.object({
  testMode: z.string().optional().default('EditMode').describe('The test mode to run (EditMode or PlayMode) - defaults to EditMode (optional)'),
  testFilter: z.string().optional().default('').describe('The specific test filter to run (e.g. specific test name or class name, must include namespace) (optional)'),
  returnOnlyFailures: z.boolean().optional().default(true).describe('Whether to show only failed tests in the results (optional)'),
  returnWithLogs: z.boolean().optional().default(false).describe('Whether to return the test logs in the results (optional)')
});

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

export function registerRunTestsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any = {}) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, logger, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, logger: Logger, params: any = {}): Promise<CallToolResult> {
  const {
    testMode = 'EditMode',
    testFilter = '',
    returnOnlyFailures = true,
    returnWithLogs = false
  } = params;

  // Step 1: Start the test job (returns immediately with jobId)
  const startResponse = await mcpUnity.sendRequest(
    {
      method: toolName,
      params: {
        testMode,
        testFilter,
        returnOnlyFailures,
        returnWithLogs
      }
    },
    { timeout: 30_000 }  // Short timeout for job creation
  );

  if (!startResponse.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      startResponse.message || `Failed to start test job: Mode=${testMode}, Filter=${testFilter || 'none'}`
    );
  }

  const jobId = startResponse.jobId;

  // If Unity returned a full result (e.g. editor_busy), return it directly
  if (!jobId) {
    return {
      content: [
        {
          type: 'text',
          text: startResponse.message || 'Test execution completed'
        },
        {
          type: 'text',
          text: JSON.stringify(startResponse, null, 2)
        }
      ]
    };
  }

  logger.info(`Test job started: ${jobId}. Polling for results...`);

  // Step 2: Poll for results until completion
  const pollIntervalMs = 3000;
  const maxPollTimeMs = 300_000; // 5 minutes max
  const deadline = Date.now() + maxPollTimeMs;
  let lastStatus: string = 'started';
  let lastError: unknown = null;

  while (Date.now() < deadline) {
    await sleep(pollIntervalMs);

    try {
        const statusResponse = await mcpUnity.sendRequest(
        {
          method: 'get_test_job_status',
          params: { jobId }
        },
        {
          timeout: 30_000,
          queueIfDisconnected: true
        }
      );

      lastStatus = statusResponse.status;

      if (statusResponse.status === 'completed') {
        // Test run finished - return the results
        const result = statusResponse.result || {};
        return {
          content: [
            {
              type: 'text',
              text: result.message || `Test job ${jobId} completed`
            },
            {
              type: 'text',
              text: JSON.stringify({
                testCount: result.testCount || 0,
                passCount: result.passCount || 0,
                failCount: result.failCount || 0,
                skipCount: result.skipCount || 0,
                results: result.results || []
              }, null, 2)
            }
          ]
        };
      }

      if (statusResponse.status === 'failed') {
        throw new McpUnityError(
          ErrorType.TOOL_EXECUTION,
          statusResponse.errorMessage || `Test job ${jobId} failed`
        );
      }

      if (statusResponse.status === 'timedOut') {
        throw new McpUnityError(
          ErrorType.TIMEOUT,
          `Test job ${jobId} timed out`
        );
      }

      if (statusResponse.status === 'notFound') {
        throw new McpUnityError(
          ErrorType.TOOL_EXECUTION,
          `Test job ${jobId} not found - may have been lost during domain reload`
        );
      }

      // Still running - log progress if available
      if (statusResponse.partial) {
        const p = statusResponse.partial;
        logger.info(`Job ${jobId} progress: ${p.testCount || 0} tests, ${p.passCount || 0} passed, ${p.failCount || 0} failed`);
      }

    } catch (err) {
      lastError = err;
      // Transient connection errors during polling are expected (domain reload, play mode)
      // Keep polling until timeout
      if (err instanceof McpUnityError && err.type !== ErrorType.CONNECTION) {
        throw err; // Re-throw non-connection errors
      }
      logger.debug(`Polling job ${jobId} failed (transient): ${String(err)}`);
    }
  }

  // Timeout reached
  throw new McpUnityError(
    ErrorType.TIMEOUT,
    `Timed out waiting for test job ${jobId} after ${maxPollTimeMs / 1000}s. Last status: ${lastStatus}. ` +
    `You can manually poll with get_test_job_status(jobId: "${jobId}")`
  );
}
