import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'package_remove';
const packageManagerTimeoutMs = 60000;
const toolDescription = 'Removes a package from the Unity Package Manager';
const paramsSchema = z.object({
  packageName: z.string().trim().min(1).describe('The package name to remove (e.g. com.unity.textmeshpro)')
});

export function registerPackageRemoveTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
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

async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  const packageName = params.packageName?.trim();
  if (!packageName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Required parameter "packageName" not provided'
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { packageName }
  }, {
    timeout: packageManagerTimeoutMs
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to remove package: ${packageName}`
    );
  }

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
