import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerObjectGetDataTool } from '../tools/objectGetDataTool.js';
import { registerObjectModifyTool } from '../tools/objectModifyTool.js';

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

describe('Object Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('object_get_data', () => {
    it('should register with correct name', () => {
      registerObjectGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('object_get_data');
    });

    it('should send request with instanceId', async () => {
      registerObjectGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Object data', object: { name: 'Test' }
      });
      const handler = getToolHandler();
      await handler({ instanceId: 12345 });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'object_get_data',
        params: expect.objectContaining({ instanceId: 12345 })
      });
    });

    it('should throw error on Unity failure', async () => {
      registerObjectGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({ success: false, message: 'Object not found' });
      const handler = getToolHandler();
      await expect(handler({ instanceId: 999 })).rejects.toThrow();
    });
  });

  describe('object_modify', () => {
    it('should register with correct name', () => {
      registerObjectModifyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      expect(getToolName()).toBe('object_modify');
    });

    it('should send request with instanceId and properties', async () => {
      registerObjectModifyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({
        success: true, type: 'text', message: 'Modified 1 property', modifiedCount: 1
      });
      const handler = getToolHandler();
      await handler({ instanceId: 12345, properties: { m_Name: 'NewName' } });
      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'object_modify',
        params: expect.objectContaining({ instanceId: 12345, properties: { m_Name: 'NewName' } })
      });
    });

    it('should throw error on Unity failure', async () => {
      registerObjectModifyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      mockSendRequest.mockResolvedValue({ success: false, message: 'No changes' });
      const handler = getToolHandler();
      await expect(handler({ instanceId: 999, properties: { m_Name: 'X' } })).rejects.toThrow();
    });
  });

  describe('all tools registered', () => {
    it('should register both object tools', () => {
      registerObjectGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      registerObjectModifyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(2);
      const toolNames = mockServerTool.mock.calls.map((call: any[]) => call[0]);
      expect(toolNames).toContain('object_get_data');
      expect(toolNames).toContain('object_modify');
    });
  });
});
