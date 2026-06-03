import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { z } from 'zod';

export function registerProfilerCaptureFrameTool(
  server: McpServer,
  mcpUnity: McpUnity,
  logger: Logger
) {
  server.tool(
    'profiler_capture_frame',
    'Captures the current frame timing data including deltaTime, FPS, frame count, time since startup, and time scale.',
    {
      // No parameters needed
    },
    async (_params) => {
      logger.info('Capturing frame data');
      return await mcpUnity.sendRequest({
        method: 'profiler_capture_frame',
        params: {}
      });
    }
  );
}
