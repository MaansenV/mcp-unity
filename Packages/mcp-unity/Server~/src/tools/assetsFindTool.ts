import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

// Constants for the tool
const toolName = 'assets_find';
const toolDescription = 'Searches the Unity AssetDatabase for assets matching the provided filter';

// Parameter schema for the tool
const paramsSchema = z.object({
  filter: z.string().default('').describe('The filter text used to search assets'),
  searchInFolders: z.array(z.string()).optional().describe('Folders to limit the search to'),
  maxResults: z.number().int().positive().max(200).default(10).describe('Maximum number of results to return'),
  includeFolders: z.boolean().default(false).describe('Whether to include folders in the results')
});

/**
 * Creates and registers the AssetsFind tool with the MCP server
 * 
 * @param server The MCP server to register the tool with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerAssetsFindTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

/**
 * Handler function for the AssetsFind tool
 * 
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The validated parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if validation fails or the request to Unity fails
 */
async function toolHandler(mcpUnity: McpUnity, params: any) {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });
  
  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to search assets`
    );
  }
  
  return {
    content: [{
      type: response.type,
      text: response.message
    }]
  };
}
