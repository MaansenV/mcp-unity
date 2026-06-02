import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerConsoleClearLogsTool } from '../tools/consoleClearLogsTool.js';
import { registerEditorApplicationGetStateTool } from '../tools/editorApplicationGetStateTool.js';
import { registerEditorApplicationSetStateTool } from '../tools/editorApplicationSetStateTool.js';
import { registerEditorSelectionGetTool } from '../tools/editorSelectionGetTool.js';
import { registerProfilerStartTool } from '../tools/profilerStartTool.js';
import { registerProfilerStopTool } from '../tools/profilerStopTool.js';
import { registerProfilerGetStatusTool } from '../tools/profilerGetStatusTool.js';
import { registerProfilerGetMemoryStatsTool } from '../tools/profilerGetMemoryStatsTool.js';
import { registerReflectionMethodFindTool } from '../tools/reflectionMethodFindTool.js';
import { registerReflectionMethodCallTool } from '../tools/reflectionMethodCallTool.js';
import { registerTypeGetJsonSchemaTool } from '../tools/typeGetJsonSchemaTool.js';
import { registerSceneSetActiveTool } from '../tools/sceneSetActiveTool.js';
import { registerSceneGetDataTool } from '../tools/sceneGetDataTool.js';
import { registerSceneListOpenedTool } from '../tools/sceneListOpenedTool.js';
import { registerAssetsShaderListAllTool } from '../tools/assetsShaderListAllTool.js';

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

describe('Console/Editor Tools', () => {
  beforeEach(() => { jest.clearAllMocks(); });

  it('console_clear_logs registers and sends request', async () => {
    registerConsoleClearLogsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('console_clear_logs');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Cleared' });
    await getToolHandler()({});
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'console_clear_logs', params: {} });
  });

  it('editor_application_get_state registers and sends request', async () => {
    registerEditorApplicationGetStateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('editor_application_get_state');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'State', isPlaying: false });
    await getToolHandler()({});
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'editor_application_get_state', params: {} });
  });

  it('editor_application_set_state registers and sends request', async () => {
    registerEditorApplicationSetStateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('editor_application_set_state');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'State set' });
    await getToolHandler()({ isPlaying: true });
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'editor_application_set_state', params: { isPlaying: true } });
  });

  it('editor_selection_get registers and sends request', async () => {
    registerEditorSelectionGetTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('editor_selection_get');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Selection', count: 0 });
    await getToolHandler()({});
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'editor_selection_get', params: {} });
  });
});

describe('Profiler Tools', () => {
  beforeEach(() => { jest.clearAllMocks(); });

  it('profiler_start registers and sends request', async () => {
    registerProfilerStartTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('profiler_start');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Started' });
    await getToolHandler()({});
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'profiler_start', params: {} });
  });

  it('profiler_stop registers and sends request', async () => {
    registerProfilerStopTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('profiler_stop');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Stopped' });
    await getToolHandler()({});
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'profiler_stop', params: {} });
  });

  it('profiler_get_status registers and sends request', async () => {
    registerProfilerGetStatusTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('profiler_get_status');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Status', isEnabled: false });
    await getToolHandler()({});
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'profiler_get_status', params: {} });
  });

  it('profiler_get_memory_stats registers and sends request', async () => {
    registerProfilerGetMemoryStatsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('profiler_get_memory_stats');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Memory stats' });
    await getToolHandler()({});
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'profiler_get_memory_stats', params: {} });
  });
});

describe('Reflection Tools', () => {
  beforeEach(() => { jest.clearAllMocks(); });

  it('reflection_method_find registers and sends request', async () => {
    registerReflectionMethodFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('reflection_method_find');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Found', methods: [], count: 0 });
    await getToolHandler()({ search: 'Update' });
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'reflection_method_find', params: { search: 'Update' } });
  });

  it('reflection_method_call registers and sends request', async () => {
    registerReflectionMethodCallTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('reflection_method_call');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Called', result: null });
    await getToolHandler()({ typeName: 'UnityEngine.Debug', methodName: 'Log', parameters: ['test'] });
    expect(mockSendRequest).toHaveBeenCalledWith({
      method: 'reflection_method_call',
      params: expect.objectContaining({ typeName: 'UnityEngine.Debug', methodName: 'Log' })
    });
  });

  it('type_get_json_schema registers and sends request', async () => {
    registerTypeGetJsonSchemaTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('type_get_json_schema');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Schema', schema: {} });
    await getToolHandler()({ typeName: 'UnityEngine.Vector3' });
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'type_get_json_schema', params: { typeName: 'UnityEngine.Vector3' } });
  });
});

describe('Scene Tools', () => {
  beforeEach(() => { jest.clearAllMocks(); });

  it('scene_set_active registers and sends request', async () => {
    registerSceneSetActiveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('scene_set_active');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Active scene set' });
    await getToolHandler()({ sceneName: 'SampleScene' });
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'scene_set_active', params: { sceneName: 'SampleScene' } });
  });

  it('scene_get_data registers and sends request', async () => {
    registerSceneGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('scene_get_data');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Scene data', scene: {} });
    await getToolHandler()({ sceneName: 'SampleScene' });
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'scene_get_data', params: { sceneName: 'SampleScene' } });
  });

  it('scene_list_opened registers and sends request', async () => {
    registerSceneListOpenedTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('scene_list_opened');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Scenes', scenes: [], count: 1 });
    await getToolHandler()({});
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'scene_list_opened', params: {} });
  });
});

describe('Shader Tools', () => {
  beforeEach(() => { jest.clearAllMocks(); });

  it('assets_shader_list_all registers and sends request', async () => {
    registerAssetsShaderListAllTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    expect(getToolName()).toBe('assets_shader_list_all');
    mockSendRequest.mockResolvedValue({ success: true, type: 'text', message: 'Shaders', shaders: [], count: 0 });
    await getToolHandler()({ search: 'Standard' });
    expect(mockSendRequest).toHaveBeenCalledWith({ method: 'assets_shader_list_all', params: { search: 'Standard' } });
  });

  it('assets_shader_list_all throws on Unity failure', async () => {
    registerAssetsShaderListAllTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    mockSendRequest.mockResolvedValue({ success: false, message: 'Failed' });
    await expect(getToolHandler()({})).rejects.toThrow();
  });
});

describe('All medium tools registered', () => {
  beforeEach(() => { jest.clearAllMocks(); });

  it('should register all 15 medium-priority tools', () => {
    registerConsoleClearLogsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerEditorApplicationGetStateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerEditorApplicationSetStateTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerEditorSelectionGetTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerProfilerStartTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerProfilerStopTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerProfilerGetStatusTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerProfilerGetMemoryStatsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerReflectionMethodFindTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerReflectionMethodCallTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerTypeGetJsonSchemaTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerSceneSetActiveTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerSceneGetDataTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerSceneListOpenedTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    registerAssetsShaderListAllTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

    expect(mockServerTool).toHaveBeenCalledTimes(15);
    const toolNames = mockServerTool.mock.calls.map((call: any[]) => call[0]);
    expect(toolNames).toContain('console_clear_logs');
    expect(toolNames).toContain('editor_application_get_state');
    expect(toolNames).toContain('editor_application_set_state');
    expect(toolNames).toContain('editor_selection_get');
    expect(toolNames).toContain('profiler_start');
    expect(toolNames).toContain('profiler_stop');
    expect(toolNames).toContain('profiler_get_status');
    expect(toolNames).toContain('profiler_get_memory_stats');
    expect(toolNames).toContain('reflection_method_find');
    expect(toolNames).toContain('reflection_method_call');
    expect(toolNames).toContain('type_get_json_schema');
    expect(toolNames).toContain('scene_set_active');
    expect(toolNames).toContain('scene_get_data');
    expect(toolNames).toContain('scene_list_opened');
    expect(toolNames).toContain('assets_shader_list_all');
  });
});
