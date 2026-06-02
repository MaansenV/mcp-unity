import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerPrefabCreateFromSceneTool } from '../tools/prefabCreateFromSceneTool.js';
import { registerPrefabOpenTool } from '../tools/prefabOpenTool.js';
import { registerPrefabCloseTool } from '../tools/prefabCloseTool.js';
import { registerPrefabSaveTool } from '../tools/prefabSaveTool.js';

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

describe('Prefab Workflow Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('prefab_create_from_scene', () => {
    it('should register with correct name', () => {
      registerPrefabCreateFromSceneTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('prefab_create_from_scene');
    });

    it('should throw when neither instanceId nor objectPath provided', async () => {
      registerPrefabCreateFromSceneTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = getToolHandler();
      await expect(handler({ prefabPath: 'Assets/Prefabs/Test.prefab' })).rejects.toThrow();
    });

    it('should send request with objectPath and prefabPath', async () => {
      registerPrefabCreateFromSceneTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Prefab created', prefabPath: 'Assets/Prefabs/Test.prefab'
      });
      const handler = getToolHandler();
      await handler({ objectPath: 'MyObject', prefabPath: 'Assets/Prefabs/Test.prefab' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'prefab_create_from_scene',
        params: expect.objectContaining({ prefabPath: 'Assets/Prefabs/Test.prefab' })
      });
    });
  });

  describe('prefab_open', () => {
    it('should register with correct name', () => {
      registerPrefabOpenTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('prefab_open');
    });

    it('should send request with prefabPath', async () => {
      registerPrefabOpenTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Prefab opened'
      });
      const handler = getToolHandler();
      await handler({ prefabPath: 'Assets/Prefabs/Test.prefab' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'prefab_open',
        params: expect.objectContaining({ prefabPath: 'Assets/Prefabs/Test.prefab' })
      });
    });

    it('should throw error on Unity failure', async () => {
      registerPrefabOpenTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({ success: false, message: 'Not a prefab' });
      const handler = getToolHandler();
      await expect(handler({ prefabPath: 'Assets/NotAPrefab.mat' })).rejects.toThrow();
    });
  });

  describe('prefab_close', () => {
    it('should register with correct name', () => {
      registerPrefabCloseTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('prefab_close');
    });

    it('should send request with default saveChanges', async () => {
      registerPrefabCloseTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Prefab stage closed'
      });
      const handler = getToolHandler();
      await handler({});
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'prefab_close',
        params: expect.objectContaining({ saveChanges: true })
      });
    });

    it('should send request with saveChanges false', async () => {
      registerPrefabCloseTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Prefab stage closed without saving'
      });
      const handler = getToolHandler();
      await handler({ saveChanges: false });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'prefab_close',
        params: expect.objectContaining({ saveChanges: false })
      });
    });
  });

  describe('prefab_save', () => {
    it('should register with correct name', () => {
      registerPrefabSaveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('prefab_save');
    });

    it('should send request with prefabPath', async () => {
      registerPrefabSaveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Prefab saved'
      });
      const handler = getToolHandler();
      await handler({ prefabPath: 'Assets/Prefabs/Test.prefab' });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'prefab_save',
        params: expect.objectContaining({ prefabPath: 'Assets/Prefabs/Test.prefab' })
      });
    });

    it('should send request with applyOverrides', async () => {
      registerPrefabSaveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Overrides applied'
      });
      const handler = getToolHandler();
      await handler({ instanceId: 123, applyOverrides: true });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'prefab_save',
        params: expect.objectContaining({ applyOverrides: true })
      });
    });
  });

  describe('all tools registered', () => {
    it('should register all 4 prefab tools', () => {
      registerPrefabCreateFromSceneTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerPrefabOpenTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerPrefabCloseTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerPrefabSaveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(4);
      const toolNames = mockServerTool.mock.calls.map((call: any[]) => call[0]);
      expect(toolNames).toContain('prefab_create_from_scene');
      expect(toolNames).toContain('prefab_open');
      expect(toolNames).toContain('prefab_close');
      expect(toolNames).toContain('prefab_save');
    });
  });
});
