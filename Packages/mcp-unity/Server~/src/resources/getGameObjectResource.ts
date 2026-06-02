import { Logger } from '../utils/logger.js';
import { ResourceTemplate, McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ListResourcesResult, ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Variables } from '@modelcontextprotocol/sdk/shared/uriTemplate.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { resourceName as hierarchyResourceName } from './getScenesHierarchyResource.js';

// Constants for the resource
const resourceName = 'get_gameobject';
const resourceUri = 'unity://gameobject/{idOrName}';
const resourceMimeType = 'application/json';
const gameObjectListCacheTtlMs = 5000;
const gameObjectListFailureCacheTtlMs = 1000;
const gameObjectListRequestTimeoutMs = 2000;

type GameObjectHierarchyNode = {
  instanceId?: number;
  name?: string;
  active?: boolean;
  activeSelf?: boolean;
  children?: GameObjectHierarchyNode[];
};

type SceneHierarchyNode = {
  rootObjects?: GameObjectHierarchyNode[];
};

type GameObjectReference = {
  instanceId?: number;
  name: string;
  path: string;
  active?: boolean;
  uri?: string;
};

/**
 * Creates and registers the GameObject resource with the MCP server
 * This resource provides access to GameObjects in Unity scenes
 * 
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerGetGameObjectResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  const listGameObjectsCached = createCachedGameObjectLister(mcpUnity, logger, resourceMimeType);

  // Create a resource template with the MCP SDK
  const resourceTemplate = new ResourceTemplate(
    resourceUri, 
    { 
      list: listGameObjectsCached
    }
  );
  logger.info(`Registering resource: ${resourceName}`);
      
  // Register this resource with the MCP server
  server.resource(
    resourceName,
    resourceTemplate,
    {
      description: 'Retrieve a GameObject by instance ID, name, or hierarchical path (e.g., "Parent/Child/MyObject")',
      mimeType: resourceMimeType
    },
    async (uri, variables) => {
      try {
        return await resourceHandler(mcpUnity, uri, variables, logger);
      } catch (error) {
        logger.error(`Error handling resource ${resourceName}: ${error}`);
        throw error;
      }
    }
  );
}

function createCachedGameObjectLister(
  mcpUnity: McpUnity,
  logger: Logger,
  resourceMimeType: string
): () => Promise<ListResourcesResult> {
  let cachedList: ListResourcesResult | undefined;
  let cacheExpiresAt = 0;
  let inFlight: Promise<ListResourcesResult> | undefined;

  return async () => {
    const now = Date.now();

    if (now < cacheExpiresAt) {
      if (cachedList) {
        return cachedList;
      }
    }

    if (inFlight) {
      return inFlight;
    }

    inFlight = listGameObjects(mcpUnity, logger, resourceMimeType)
      .then((result) => {
        cachedList = result;
        cacheExpiresAt = Date.now() + gameObjectListCacheTtlMs;
        return result;
      })
      .catch((error) => {
        logger.error(`[getGameObjectResource] Failed to list GameObjects: ${error}`);
        cachedList = cachedList ?? { resources: [] };
        cacheExpiresAt = Date.now() + gameObjectListFailureCacheTtlMs;
        return cachedList;
      })
      .finally(() => {
        inFlight = undefined;
      });

    return inFlight;
  };
}

/**
 * Handles requests for GameObject information from Unity
 * 
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param uri The requested resource URI
 * @param variables Variables extracted from the URI template
 * @param logger The logger instance for diagnostic information
 * @returns A promise that resolves to the GameObject data
 * @throws McpUnityError if the request to Unity fails
 */
async function resourceHandler(mcpUnity: McpUnity, uri: URL, variables: Variables, logger: Logger): Promise<ReadResourceResult> {
  // Extract and convert the parameter from the template variables
  const idOrName = decodeURIComponent(variables["idOrName"] as string);
      
  // Send request to Unity
  const response = await mcpUnity.sendRequest({
    method: resourceName,
    params: {
      idOrName: idOrName
    }
  });
  
  if (!response.success) {
    throw new McpUnityError(
      ErrorType.RESOURCE_FETCH,
      response.message || 'Failed to fetch GameObject from Unity'
    );
  }
  
  return {
    contents: [{
      uri: uri.toString(),
      mimeType: resourceMimeType,
      text: JSON.stringify(response, null, 2)
    }]
  };
}

/**
 * Get a list of all GameObjects in the scene
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance
 * @param resourceMimeType The MIME type for the resource
 * @returns A promise that resolves to a list of GameObject resources
 */
async function listGameObjects(mcpUnity: McpUnity, logger: Logger, resourceMimeType: string): Promise<ListResourcesResult> {
  const hierarchyResponse = await mcpUnity.sendRequest({
    method: hierarchyResourceName,
    params: {}
  }, {
    queueIfDisconnected: false,
    timeout: gameObjectListRequestTimeoutMs
  });
  
  if (!hierarchyResponse.success) {
    logger.error(`Failed to fetch hierarchy: ${hierarchyResponse.message}`);
    throw new Error(hierarchyResponse.message || 'Failed to fetch hierarchy');
  }
  
  // Process the hierarchy to create a list of GameObject references
  const gameObjects = processHierarchyToGameObjectList(hierarchyResponse.hierarchy || []);

  logger.info(`[getGameObjectResource] Fetched hierarchy with ${gameObjects.length} GameObjects`);
  
  // Create resources array with both instance ID and path URIs
  const resources: Array<{
    uri: string;
    name: string;
    description: string;
    mimeType: string;
  }> = [];
  
  // Add resources for each GameObject
  gameObjects.forEach(obj => {
    if (obj.instanceId !== undefined) {
      resources.push({
        uri: `unity://gameobject/${obj.instanceId}`,
        name: obj.name,
        description: `GameObject with instance ID ${obj.instanceId} at path: ${obj.path}`,
        mimeType: resourceMimeType
      });
    }
    
    // Add resource with path URI if path exists
    if (obj.path) {
      resources.push({
        uri: `unity://gameobject/${encodeURIComponent(obj.path)}`,
        name: obj.name,
        description: `GameObject with instance ID ${obj.instanceId} at path: ${obj.path}`,
        mimeType: resourceMimeType
      });
    }
  });
  
  return { resources };
}

/**
 * Process the hierarchy data to create a list of GameObject references
 * @param hierarchyData The hierarchy data from Unity
 * @returns An array of GameObject references with their instance IDs and paths
 */
function processHierarchyToGameObjectList(hierarchyData: unknown): GameObjectReference[] {
  const gameObjects: GameObjectReference[] = [];
  
  // Helper function to traverse the hierarchy recursively
  function traverseHierarchy(node: GameObjectHierarchyNode, path: string = ''): void {
    if (!node || !node.name) return;
    
    // Current path is parent path + node name
    const currentPath = path ? `${path}/${node.name}` : node.name;
    
    // Add this GameObject to the list
    gameObjects.push({
      instanceId: node.instanceId,
      name: node.name,
      path: currentPath,
      active: node.active ?? node.activeSelf,
      uri: `unity://gameobject/${node.instanceId}`
    });
    
    // Process children recursively
    if (node.children && Array.isArray(node.children)) {
      for (const child of node.children) {
        traverseHierarchy(child, currentPath);
      }
    }
  }
  
  // Start traversal with each root GameObject
  if (!Array.isArray(hierarchyData)) {
    return gameObjects;
  }

  for (const hierarchyNode of hierarchyData) {
    const sceneNode = hierarchyNode as SceneHierarchyNode;
    if (Array.isArray(sceneNode.rootObjects)) {
      for (const rootNode of sceneNode.rootObjects) {
        traverseHierarchy(rootNode);
      }
      continue;
    }

    traverseHierarchy(hierarchyNode as GameObjectHierarchyNode);
  }
  
  return gameObjects;
}
