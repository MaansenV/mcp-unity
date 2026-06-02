import { jest, describe, it, expect, beforeEach, afterEach } from '@jest/globals';
import { registerGetGameObjectResource } from '../resources/getGameObjectResource.js';

const mockSendRequest = jest.fn();
const mockMcpUnity = {
  sendRequest: mockSendRequest
};

const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

const mockServerResource = jest.fn();
const mockServer = {
  resource: mockServerResource
};

function registerResource() {
  registerGetGameObjectResource(mockServer as any, mockMcpUnity as any, mockLogger as any);

  const call = mockServerResource.mock.calls[0] as any[];
  return {
    resourceName: call[0],
    resourceTemplate: call[1] as any,
    metadata: call[2],
    readHandler: call[3] as (uri: URL, variables: Record<string, string>) => Promise<any>
  };
}

function hierarchyResponse(name = 'Parent') {
  return {
    success: true,
    hierarchy: [
      {
        name: 'SampleScene',
        path: 'Assets/SampleScene.unity',
        buildIndex: 0,
        isDirty: false,
        rootObjects: [
          {
            instanceId: 1,
            name,
            activeSelf: true,
            children: [
              {
                instanceId: 2,
                name: 'Child',
                activeSelf: true,
                children: []
              }
            ]
          }
        ]
      }
    ]
  };
}

describe('Get GameObject Resource', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useRealTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('registers the resource with a list callback', () => {
    const { resourceName, resourceTemplate, metadata } = registerResource();

    expect(mockServerResource).toHaveBeenCalledTimes(1);
    expect(resourceName).toBe('get_gameobject');
    expect(resourceTemplate.listCallback).toEqual(expect.any(Function));
    expect(metadata).toEqual(expect.objectContaining({
      description: expect.stringContaining('Retrieve a GameObject'),
      mimeType: 'application/json'
    }));
    expect(mockLogger.info).toHaveBeenCalledWith('Registering resource: get_gameobject');
  });

  it('fetches hierarchy and returns GameObject resources on first list request', async () => {
    mockSendRequest.mockResolvedValue(hierarchyResponse() as never);
    const { resourceTemplate } = registerResource();

    const result = await resourceTemplate.listCallback();

    expect(mockSendRequest).toHaveBeenCalledWith({
      method: 'get_scenes_hierarchy',
      params: {}
    }, {
      queueIfDisconnected: false,
      timeout: 2000
    });
    expect(result.resources).toEqual(expect.arrayContaining([
      expect.objectContaining({ uri: 'unity://gameobject/1', name: 'Parent' }),
      expect.objectContaining({ uri: 'unity://gameobject/Parent', name: 'Parent' }),
      expect.objectContaining({ uri: 'unity://gameobject/2', name: 'Child' }),
      expect.objectContaining({ uri: 'unity://gameobject/Parent%2FChild', name: 'Child' })
    ]));
    expect(result.resources).not.toEqual(expect.arrayContaining([
      expect.objectContaining({ uri: 'unity://gameobject/undefined' })
    ]));
  });

  it('uses the cached list within the TTL', async () => {
    jest.useFakeTimers().setSystemTime(new Date('2026-01-01T00:00:00Z'));
    mockSendRequest.mockResolvedValue(hierarchyResponse() as never);
    const { resourceTemplate } = registerResource();

    await resourceTemplate.listCallback();
    await resourceTemplate.listCallback();

    expect(mockSendRequest).toHaveBeenCalledTimes(1);
  });

  it('fetches a fresh hierarchy after the TTL expires', async () => {
    jest.useFakeTimers().setSystemTime(new Date('2026-01-01T00:00:00Z'));
    mockSendRequest
      .mockResolvedValueOnce(hierarchyResponse('Parent') as never)
      .mockResolvedValueOnce(hierarchyResponse('UpdatedParent') as never);
    const { resourceTemplate } = registerResource();

    const firstResult = await resourceTemplate.listCallback();
    jest.advanceTimersByTime(5001);
    const secondResult = await resourceTemplate.listCallback();

    expect(mockSendRequest).toHaveBeenCalledTimes(2);
    expect(firstResult.resources).toEqual(expect.arrayContaining([
      expect.objectContaining({ uri: 'unity://gameobject/Parent' })
    ]));
    expect(secondResult.resources).toEqual(expect.arrayContaining([
      expect.objectContaining({ uri: 'unity://gameobject/UpdatedParent' })
    ]));
  });

  it('shares one in-flight hierarchy request across concurrent list calls', async () => {
    let resolveRequest!: (value: unknown) => void;
    const requestPromise = new Promise((resolve) => {
      resolveRequest = resolve;
    });
    mockSendRequest.mockReturnValue(requestPromise as never);
    const { resourceTemplate } = registerResource();

    const firstPromise = resourceTemplate.listCallback();
    const secondPromise = resourceTemplate.listCallback();
    const thirdPromise = resourceTemplate.listCallback();

    expect(mockSendRequest).toHaveBeenCalledTimes(1);

    resolveRequest(hierarchyResponse());
    const results = await Promise.all([firstPromise, secondPromise, thirdPromise]);

    expect(results[0]).toEqual(results[1]);
    expect(results[1]).toEqual(results[2]);
  });

  it('returns an empty list for failed hierarchy requests and throttles repeated failures briefly', async () => {
    jest.useFakeTimers().setSystemTime(new Date('2026-01-01T00:00:00Z'));
    mockSendRequest.mockResolvedValue({
      success: false,
      message: 'Unity unavailable'
    } as never);
    const { resourceTemplate } = registerResource();

    await expect(resourceTemplate.listCallback()).resolves.toEqual({ resources: [] });
    await expect(resourceTemplate.listCallback()).resolves.toEqual({ resources: [] });
    expect(mockSendRequest).toHaveBeenCalledTimes(1);

    jest.advanceTimersByTime(1001);
    await expect(resourceTemplate.listCallback()).resolves.toEqual({ resources: [] });
    expect(mockSendRequest).toHaveBeenCalledTimes(2);
  });

  it('returns the last successful cached list when refresh fails', async () => {
    jest.useFakeTimers().setSystemTime(new Date('2026-01-01T00:00:00Z'));
    mockSendRequest
      .mockResolvedValueOnce(hierarchyResponse() as never)
      .mockResolvedValueOnce({ success: false, message: 'Unity unavailable' } as never);
    const { resourceTemplate } = registerResource();

    const successfulResult = await resourceTemplate.listCallback();
    jest.advanceTimersByTime(5001);
    const failedRefreshResult = await resourceTemplate.listCallback();

    expect(mockSendRequest).toHaveBeenCalledTimes(2);
    expect(failedRefreshResult).toEqual(successfulResult);
  });

  it('preserves direct GameObject read behavior', async () => {
    mockSendRequest.mockResolvedValue({
      success: true,
      id: 2,
      name: 'Child'
    } as never);
    const { readHandler } = registerResource();

    const result = await readHandler(
      new URL('unity://gameobject/Parent%2FChild'),
      { idOrName: 'Parent%2FChild' }
    );

    expect(mockSendRequest).toHaveBeenCalledWith({
      method: 'get_gameobject',
      params: {
        idOrName: 'Parent/Child'
      }
    });
    expect(result.contents[0]).toEqual(expect.objectContaining({
      uri: 'unity://gameobject/Parent%2FChild',
      mimeType: 'application/json',
      text: expect.stringContaining('Child')
    }));
  });
});
