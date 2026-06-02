import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

// Constants for the tool
const toolName = 'assets_find_built_in';
const toolDescription = 'Finds Unity built-in resources such as shaders and materials';

// Parameter schema for the tool
const paramsSchema = z.object({
  query: z.string().default('').describe('Case-insensitive search query for the built-in asset name, type, path, or resource path'),
  assetType: z.enum(['Shader', 'Material']).optional().describe('Optional built-in asset type filter (V1 supports Shader and Material)'),
  maxResults: z.number().int().positive().max(100).default(10).describe('Maximum number of results to return (1-100)')
});

/**
 * Creates and registers the AssetsFindBuiltIn tool with the MCP server
 * 
 * @param server The MCP server to register the tool with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerAssetsFindBuiltInTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
 * Handler function for the AssetsFindBuiltIn tool
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
      response.message || 'Failed to find built-in assets'
    );
  }
  
  return {
    content: [{
      type: response.type,
      text: response.message || `Found ${response.count ?? 0} built-in asset(s)`
    }]
  };
}
