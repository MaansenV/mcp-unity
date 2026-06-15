import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerScriptReadTool } from '../tools/scriptReadTool.js';
import { registerScriptUpdateOrCreateTool } from '../tools/scriptUpdateOrCreateTool.js';

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

describe('Script Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('script_read', () => {
    it('should register with correct name', () => {
      registerScriptReadTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('script_read');
    });

    it('should send request with filePath', async () => {
      registerScriptReadTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: "Read script file 'Assets/Scripts/MyClass.cs'",
        filePath: 'Assets/Scripts/MyClass.cs',
        content: 'public class MyClass : MonoBehaviour { }',
        sizeBytes: 42,
        lastModified: '2026-01-01T00:00:00Z'
      });

      const handler = getToolHandler();
      const result = await handler({ filePath: 'Assets/Scripts/MyClass.cs' });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'script_read',
        params: { filePath: 'Assets/Scripts/MyClass.cs' }
      });
      expect(result.content[0].type).toBe('text');
      const response = JSON.parse(result.content[0].text);
      expect(response.success).toBe(true);
      expect(response.content).toBe('public class MyClass : MonoBehaviour { }');
    });

    it('should handle error response', async () => {
      registerScriptReadTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: false,
        message: "Script file 'Assets/Scripts/Missing.cs' not found"
      });

      const handler = getToolHandler();
      await expect(handler({ filePath: 'Assets/Scripts/Missing.cs' })).rejects.toThrow();
    });
  });

  describe('script_update_or_create', () => {
    it('should register with correct name', () => {
      registerScriptUpdateOrCreateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('script_update_or_create');
    });

    it('should send request with filePath and content', async () => {
      registerScriptUpdateOrCreateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: "Created script file 'Assets/Scripts/NewClass.cs'",
        filePath: 'Assets/Scripts/NewClass.cs',
        created: true,
        sizeBytes: 100
      });

      const handler = getToolHandler();
      const result = await handler({
        filePath: 'Assets/Scripts/NewClass.cs',
        content: 'public class NewClass : MonoBehaviour { }'
      });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'script_update_or_create',
        params: {
          filePath: 'Assets/Scripts/NewClass.cs',
          content: 'public class NewClass : MonoBehaviour { }',
          recompile: false
        }
      });
      expect(result.content[0].type).toBe('text');
      const response = JSON.parse(result.content[0].text);
      expect(response.success).toBe(true);
      expect(response.created).toBe(true);
    });

    it('should handle error response', async () => {
      registerScriptUpdateOrCreateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: false,
        message: 'Failed to write script: Access denied'
      });

      const handler = getToolHandler();
      await expect(handler({
        filePath: 'Assets/Scripts/Test.cs',
        content: 'class Test {}'
      })).rejects.toThrow();
    });
  });
});
