import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerAssetsFindTool } from '../tools/assetsFindTool.js';
import { registerAssetsFindBuiltInTool } from '../tools/assetsFindBuiltInTool.js';
import { registerAssetsGetDataTool } from '../tools/assetsGetDataTool.js';
import { registerAssetsCreateFolderTool } from '../tools/assetsCreateFolderTool.js';
import { registerAssetsCopyTool } from '../tools/assetsCopyTool.js';
import { registerAssetsRefreshTool } from '../tools/assetsRefreshTool.js';
import { registerAssetsMoveTool } from '../tools/assetsMoveTool.js';
import { registerAssetsDeleteTool } from '../tools/assetsDeleteTool.js';
import { registerAssetsModifyTool } from '../tools/assetsModifyTool.js';

// Mock the McpUnity class
const mockSendRequest = jest.fn();
const mockMcpUnity = {
  sendRequest: mockSendRequest
};

// Mock the Logger
const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

// Mock the McpServer
const mockServerTool = jest.fn();
const mockServer = {
  tool: mockServerTool
};

// Helper to extract tool handler from the LAST registration call
function getToolHandler() {
  const calls = mockServerTool.mock.calls;
  return calls[calls.length - 1][3] as (params: any) => Promise<any>;
}

function getToolName() {
  const calls = mockServerTool.mock.calls;
  return calls[calls.length - 1][0] as string;
}

describe('Asset Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('assets_find', () => {
    it('should register with correct name', () => {
      registerAssetsFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_find');
    });

    it('should send request with correct parameters', async () => {
      registerAssetsFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Found 2 asset(s).',
        assets: [],
        count: 2
      });

      const handler = getToolHandler();
      const result = await handler({ filter: 't:Prefab', maxResults: 10 });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_find',
        params: expect.objectContaining({ filter: 't:Prefab', maxResults: 10 })
      });
      expect(result.content[0].type).toBe('text');
    });

    it('should throw error on Unity failure', async () => {
      registerAssetsFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({ success: false, message: 'Search failed' });
      const handler = getToolHandler();
      await expect(handler({ filter: 'test' })).rejects.toThrow();
    });
  });

  describe('assets_find_built_in', () => {
    it('should register with correct name', () => {
      registerAssetsFindBuiltInTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_find_built_in');
    });

    it('should send request and return results', async () => {
      registerAssetsFindBuiltInTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Found 3 built-in asset(s).',
        assets: [],
        count: 3
      });

      const handler = getToolHandler();
      const result = await handler({ query: 'Standard', assetType: 'Shader' });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_find_built_in',
        params: expect.objectContaining({ query: 'Standard', assetType: 'Shader' })
      });
      expect(result.content[0].text).toContain('Found');
    });
  });

  describe('assets_get_data', () => {
    it('should register with correct name', () => {
      registerAssetsGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_get_data');
    });

    it('should throw when neither assetPath nor guid provided', async () => {
      registerAssetsGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = getToolHandler();
      await expect(handler({})).rejects.toThrow();
    });

    it('should send request with assetPath', async () => {
      registerAssetsGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Retrieved data',
        asset: { path: 'Assets/test.prefab' }
      });

      const handler = getToolHandler();
      await handler({ assetPath: 'Assets/test.prefab' });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_get_data',
        params: expect.objectContaining({ assetPath: 'Assets/test.prefab' })
      });
    });
  });

  describe('assets_create_folder', () => {
    it('should register with correct name', () => {
      registerAssetsCreateFolderTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_create_folder');
    });

    it('should send request for single folder creation', async () => {
      registerAssetsCreateFolderTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Created 1/1 folder(s).'
      });

      const handler = getToolHandler();
      await handler({ parentPath: 'Assets', newName: 'MyFolder' });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_create_folder',
        params: expect.objectContaining({ parentPath: 'Assets', newName: 'MyFolder' })
      });
    });
  });

  describe('assets_copy', () => {
    it('should register with correct name', () => {
      registerAssetsCopyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_copy');
    });

    it('should send request for single copy', async () => {
      registerAssetsCopyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Copied 1/1 asset(s).'
      });

      const handler = getToolHandler();
      await handler({ srcPath: 'Assets/A.prefab', destPath: 'Assets/B.prefab' });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_copy',
        params: expect.objectContaining({ srcPath: 'Assets/A.prefab', destPath: 'Assets/B.prefab' })
      });
    });
  });

  describe('assets_refresh', () => {
    it('should register with correct name', () => {
      registerAssetsRefreshTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_refresh');
    });

    it('should send request with default option', async () => {
      registerAssetsRefreshTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Refresh completed.'
      });

      const handler = getToolHandler();
      await handler({});

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_refresh',
        params: expect.objectContaining({ option: 'Default' })
      });
    });

    it('should throw error on Unity failure', async () => {
      registerAssetsRefreshTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({ success: false, message: 'Refresh failed' });
      const handler = getToolHandler();
      await expect(handler({})).rejects.toThrow();
    });
  });

  describe('assets_move', () => {
    it('should register with correct name', () => {
      registerAssetsMoveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_move');
    });

    it('should send request for single move', async () => {
      registerAssetsMoveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Moved 1/1 asset(s).'
      });

      const handler = getToolHandler();
      await handler({ srcPath: 'Assets/A.prefab', destPath: 'Assets/Sub/A.prefab' });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_move',
        params: expect.objectContaining({ srcPath: 'Assets/A.prefab', destPath: 'Assets/Sub/A.prefab' })
      });
    });
  });

  describe('assets_delete', () => {
    it('should register with correct name', () => {
      registerAssetsDeleteTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_delete');
    });

    it('should throw when confirmDelete is false', async () => {
      registerAssetsDeleteTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = getToolHandler();
      await expect(handler({ paths: ['Assets/temp.prefab'] })).rejects.toThrow();
    });

    it('should send request when confirmDelete is true', async () => {
      registerAssetsDeleteTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Deleted 1/1 asset(s).'
      });

      const handler = getToolHandler();
      await handler({ paths: ['Assets/temp.prefab'], confirmDelete: true });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_delete',
        params: expect.objectContaining({ confirmDelete: true })
      });
    });
  });

  describe('assets_modify', () => {
    it('should register with correct name', () => {
      registerAssetsModifyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('assets_modify');
    });

    it('should throw when neither assetPath nor guid provided', async () => {
      registerAssetsModifyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = getToolHandler();
      await expect(handler({ properties: { m_Name: 'NewName' } })).rejects.toThrow();
    });

    it('should send request with properties', async () => {
      registerAssetsModifyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Modified 1 property(ies).'
      });

      const handler = getToolHandler();
      await handler({ assetPath: 'Assets/mat.mat', properties: { m_Name: 'NewMat' } });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assets_modify',
        params: expect.objectContaining({ assetPath: 'Assets/mat.mat' })
      });
    });
  });

  describe('all tools registered', () => {
    it('should have registered all 9 asset tools', () => {
      registerAssetsFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerAssetsFindBuiltInTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerAssetsGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerAssetsCreateFolderTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerAssetsCopyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerAssetsRefreshTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerAssetsMoveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerAssetsDeleteTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerAssetsModifyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(9);

      const toolNames = mockServerTool.mock.calls.map((call: any[]) => call[0]);
      expect(toolNames).toContain('assets_find');
      expect(toolNames).toContain('assets_find_built_in');
      expect(toolNames).toContain('assets_get_data');
      expect(toolNames).toContain('assets_create_folder');
      expect(toolNames).toContain('assets_copy');
      expect(toolNames).toContain('assets_refresh');
      expect(toolNames).toContain('assets_move');
      expect(toolNames).toContain('assets_delete');
      expect(toolNames).toContain('assets_modify');
    });
  });
});
