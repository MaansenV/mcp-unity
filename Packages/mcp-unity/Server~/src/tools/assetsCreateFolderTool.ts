import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'assets_create_folder';
const toolDescription = 'Creates one or more folders in the Unity AssetDatabase';

const folderSchema = z.object({
  parentPath: z.string().describe('Parent folder path under Assets'),
  newName: z.string().describe('Name of the new folder')
});

const paramsSchema = z.object({
  parentPath: z.string().optional().describe('Parent folder path under Assets'),
  newName: z.string().optional().describe('Name of the new folder'),
  folders: z.array(folderSchema).max(50, 'Maximum of 50 folders allowed per batch').optional().describe('Batch folder creation requests')
});

export function registerAssetsCreateFolderTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  if (!params.parentPath && !params.newName && (!params.folders || params.folders.length === 0)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Provide either 'parentPath' and 'newName', or a non-empty 'folders' array"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to create folder(s)'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: JSON.stringify(response, null, 2)
    }]
  };
}
