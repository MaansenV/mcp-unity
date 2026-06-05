import * as z from "zod";
import { Logger } from "../utils/logger.js";
import { McpUnity } from "../unity/mcpUnity.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { McpUnityError, ErrorType } from "../utils/errors.js";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";

// Constants for the tool
const toolName = "prefab_get_hierarchy";
const toolDescription =
  "Retrieves the hierarchy of a prefab currently open in Prefab Mode. Returns the root GameObject and its children with component info. Use 'maxDepth' to control traversal depth. Must have a prefab open in Prefab Mode first.";
const paramsSchema = z.object({
  maxDepth: z
    .number()
    .int()
    .min(0)
    .max(50)
    .optional()
    .describe(
      "Maximum child hierarchy depth to traverse. 0 = no children, 1 = direct children, 2 = grandchildren. Default: 2."
    ),
  includeComponents: z
    .boolean()
    .optional()
    .describe(
      "Include the component list on each node. Set false to get a hierarchy-only outline. Default: true."
    ),
  includeComponentProperties: z
    .boolean()
    .optional()
    .describe(
      "Include serialized property values for each component. Set false to keep component type names only. Default: true."
    ),
});

/**
 * Creates and registers the Prefab Get Hierarchy tool with the MCP server
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerPrefabGetHierarchyTool(
  server: McpServer,
  mcpUnity: McpUnity,
  logger: Logger
) {
  logger.info(`Registering tool: ${toolName}`);

  // Register this tool with the MCP server
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

/**
 * Handles requests for Prefab hierarchy information from Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>
): Promise<CallToolResult> {
  const { maxDepth, includeComponents, includeComponentProperties } = params;

  // Send request to Unity
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      maxDepth,
      includeComponents,
      includeComponentProperties,
    },
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || "Failed to fetch prefab hierarchy from Unity"
    );
  }

  return {
    content: [
      {
        type: "text",
        text: JSON.stringify(response, null, 2),
      },
    ],
  };
}
