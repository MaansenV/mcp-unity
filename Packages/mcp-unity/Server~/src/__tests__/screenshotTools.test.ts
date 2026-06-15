import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerScreenshotSceneViewTool } from '../tools/screenshotSceneViewTool.js';
import { registerScreenshotGameViewTool } from '../tools/screenshotGameViewTool.js';

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

describe('Screenshot Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('screenshot_scene_view', () => {
    it('should register with correct name', () => {
      registerScreenshotSceneViewTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('screenshot_scene_view');
    });

    it('should send request with default dimensions', async () => {
      registerScreenshotSceneViewTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'image',
        mimeType: 'image/png',
        data: 'base64data==',
        width: 1920,
        height: 1080,
        message: 'Captured Scene View screenshot (1920x1080)'
      });

      const handler = getToolHandler();
      const result = await handler({});

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'screenshot_scene_view',
        params: { width: 1920, height: 1080 }
      });
      expect(result.content[0].type).toBe('image');
      expect(result.content[0].mimeType).toBe('image/png');
      expect(result.content[0].data).toBe('base64data==');
    });

    it('should send request with custom dimensions', async () => {
      registerScreenshotSceneViewTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'image',
        mimeType: 'image/png',
        data: 'base64data==',
        width: 1280,
        height: 720,
        message: 'Captured Scene View screenshot (1280x720)'
      });

      const handler = getToolHandler();
      const result = await handler({ width: 1280, height: 720 });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'screenshot_scene_view',
        params: { width: 1280, height: 720 }
      });
      expect(result.content[0].type).toBe('image');
    });

    it('should handle error response', async () => {
      registerScreenshotSceneViewTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: false,
        message: 'SceneView not found'
      });

      const handler = getToolHandler();
      await expect(handler({})).rejects.toThrow();
    });
  });

  describe('screenshot_game_view', () => {
    it('should register with correct name', () => {
      registerScreenshotGameViewTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('screenshot_game_view');
    });

    it('should send request and return image', async () => {
      registerScreenshotGameViewTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'image',
        mimeType: 'image/png',
        data: 'gameviewbase64==',
        width: 1920,
        height: 1080,
        message: 'Captured Game View screenshot (1920x1080)'
      });

      const handler = getToolHandler();
      const result = await handler({});

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'screenshot_game_view',
        params: {}
      });
      expect(result.content[0].type).toBe('image');
      expect(result.content[0].data).toBe('gameviewbase64==');
    });

    it('should handle error response', async () => {
      registerScreenshotGameViewTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: false,
        message: 'GameView render texture not found'
      });

      const handler = getToolHandler();
      await expect(handler({})).rejects.toThrow();
    });
  });
});
