import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerGameObjectFindTool } from '../tools/gameObjectFindTool.js';
import { registerGameObjectCreateTool } from '../tools/gameObjectCreateTool.js';
import { registerGameObjectComponentDestroyTool } from '../tools/gameObjectComponentDestroyTool.js';
import { registerGameObjectComponentGetDataTool } from '../tools/gameObjectComponentGetDataTool.js';
import { registerGameObjectComponentListAllTool } from '../tools/gameObjectComponentListAllTool.js';

const mockSendRequest = jest.fn();
const mockMcpUnity = { sendRequest: mockSendRequest };
const mockLogger = { info: jest.fn(), debug: jest.fn(), warn: jest.fn(), error: jest.fn() };
const mockServerTool = jest.fn();
const mockServer = { tool: mockServerTool };

function getToolHandler() {
  const calls = mockServerTool.mock.calls;
  return calls[calls.length - 1][3] as (params: any) => Promise<any>;
}

function getToolName() {
  const calls = mockServerTool.mock.calls;
  return calls[calls.length - 1][0] as string;
}

describe('GameObject/Component Discovery Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('gameobject_find', () => {
    it('should register with correct name', () => {
      registerGameObjectFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('gameobject_find');
    });

    it('should throw when no filter provided', async () => {
      registerGameObjectFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = getToolHandler();
      await expect(handler({})).rejects.toThrow();
    });

    it('should send request with name filter', async () => {
      registerGameObjectFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Found 1 object(s).', results: [], count: 1
      });
      const handler = getToolHandler();
      await handler({ name: 'Player' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'gameobject_find',
        params: expect.objectContaining({ name: 'Player' })
      });
    });
  });

  describe('gameobject_create', () => {
    it('should register with correct name', () => {
      registerGameObjectCreateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('gameobject_create');
    });

    it('should send request to create empty GameObject', async () => {
      registerGameObjectCreateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Created GameObject', instanceId: 123
      });
      const handler = getToolHandler();
      await handler({ name: 'MyObject' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'gameobject_create',
        params: expect.objectContaining({ name: 'MyObject' })
      });
    });

    it('should send request to create primitive', async () => {
      registerGameObjectCreateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Created Cube', instanceId: 456
      });
      const handler = getToolHandler();
      await handler({ name: 'MyCube', primitiveType: 'Cube' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'gameobject_create',
        params: expect.objectContaining({ primitiveType: 'Cube' })
      });
    });
  });

  describe('gameobject_component_destroy', () => {
    it('should register with correct name', () => {
      registerGameObjectComponentDestroyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('gameobject_component_destroy');
    });

    it('should throw when neither instanceId nor objectPath provided', async () => {
      registerGameObjectComponentDestroyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = getToolHandler();
      await expect(handler({ componentName: 'Rigidbody' })).rejects.toThrow();
    });

    it('should send request with objectPath', async () => {
      registerGameObjectComponentDestroyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Component removed'
      });
      const handler = getToolHandler();
      await handler({ objectPath: 'Player', componentName: 'Rigidbody' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'gameobject_component_destroy',
        params: expect.objectContaining({ componentName: 'Rigidbody' })
      });
    });
  });

  describe('gameobject_component_get', () => {
    it('should register with correct name', () => {
      registerGameObjectComponentGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('gameobject_component_get');
    });

    it('should throw when neither instanceId nor objectPath provided', async () => {
      registerGameObjectComponentGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = getToolHandler();
      await expect(handler({ componentName: 'Transform' })).rejects.toThrow();
    });

    it('should send request', async () => {
      registerGameObjectComponentGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Component data', component: {}
      });
      const handler = getToolHandler();
      await handler({ instanceId: 123, componentName: 'Transform' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'gameobject_component_get',
        params: expect.objectContaining({ componentName: 'Transform' })
      });
    });
  });

  describe('gameobject_component_list_all', () => {
    it('should register with correct name', () => {
      registerGameObjectComponentListAllTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('gameobject_component_list_all');
    });

    it('should send request with default params', async () => {
      registerGameObjectComponentListAllTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Found 100 types', components: [], count: 100
      });
      const handler = getToolHandler();
      await handler({});
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'gameobject_component_list_all',
        params: expect.objectContaining({ page: 1, pageSize: 50 })
      });
    });

    it('should send request with search filter', async () => {
      registerGameObjectComponentListAllTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Found 5 types', components: [], count: 5
      });
      const handler = getToolHandler();
      await handler({ search: 'Rigidbody' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'gameobject_component_list_all',
        params: expect.objectContaining({ search: 'Rigidbody' })
      });
    });
  });

  describe('all tools registered', () => {
    it('should register all 5 GameObject/component tools', () => {
      registerGameObjectFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerGameObjectCreateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerGameObjectComponentDestroyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerGameObjectComponentGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerGameObjectComponentListAllTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(5);
      const toolNames = mockServerTool.mock.calls.map((call: any[]) => call[0]);
      expect(toolNames).toContain('gameobject_find');
      expect(toolNames).toContain('gameobject_create');
      expect(toolNames).toContain('gameobject_component_destroy');
      expect(toolNames).toContain('gameobject_component_get');
      expect(toolNames).toContain('gameobject_component_list_all');
    });
  });
});
