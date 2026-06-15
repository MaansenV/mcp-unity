import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'script_update_or_create';
const toolDescription = 'Creates or updates a C# script file in the Unity project. Writes the content and refreshes the AssetDatabase.';
const paramsSchema = z.object({
  filePath: z.string().min(1).describe('Path to the script file relative to project root (e.g. "Assets/Scripts/MyClass.cs"). .cs extension is added automatically if missing.'),
  content: z.string().describe('The C# source code content to write to the file'),
  recompile: z.boolean().optional().describe('Whether to explicitly trigger a recompile (default: false, Unity auto-recompiles on AssetDatabase refresh)')
});

export function registerScriptUpdateOrCreateTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      filePath: params.filePath,
      content: params.content,
      recompile: params.recompile ?? false
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to write script file'
    );
  }

  return {
    content: [{
      type: 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
