import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'gameobject_create';
const toolDescription = 'Creates a new GameObject in the Unity scene, optionally as an empty object or primitive, and can parent it in the hierarchy.';

const positionSchema = z.object({
  x: z.number().describe('X component'),
  y: z.number().describe('Y component'),
  z: z.number().describe('Z component'),
});

const rotationSchema = z.object({
  x: z.number().describe('X component'),
  y: z.number().describe('Y component'),
  z: z.number().describe('Z component'),
});

const paramsSchema = z.object({
  name: z.string().default('New GameObject').describe('Name for the created GameObject'),
  primitiveType: z.enum(['Empty', 'Cube', 'Sphere', 'Capsule', 'Cylinder', 'Plane', 'Quad']).optional().describe('Primitive type to create. Use Empty or omit for a standard GameObject.'),
  parentPath: z.string().optional().describe('Path to the parent GameObject in the hierarchy'),
  parentId: z.number().optional().describe('Instance ID of the parent GameObject'),
  position: positionSchema.optional().describe('World or local position to apply, depending on worldSpace'),
  rotation: rotationSchema.optional().describe('World or local Euler rotation to apply, depending on worldSpace'),
  worldSpace: z.boolean().default(true).describe('If true (default), position and rotation are treated as world space values; otherwise local space values are used'),
});

export function registerGameObjectCreateTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      name: params.name,
      primitiveType: params.primitiveType,
      parentPath: params.parentPath,
      parentId: params.parentId,
      position: params.position,
      rotation: params.rotation,
      worldSpace: params.worldSpace ?? true,
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to create the GameObject'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || 'Successfully created the GameObject'
    }]
  };
}
