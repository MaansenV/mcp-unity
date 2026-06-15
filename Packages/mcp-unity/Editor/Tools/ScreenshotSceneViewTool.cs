using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for capturing a screenshot of the Unity Scene View.
    /// Returns the image as a base64-encoded PNG.
    /// </summary>
    public class ScreenshotSceneViewTool : McpToolBase
    {
        private const int MaxDimension = 3840;

        public ScreenshotSceneViewTool()
        {
            Name = "screenshot_scene_view";
            Description = "Captures a screenshot of the Unity Scene View and returns it as a base64-encoded PNG";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, System.Threading.Tasks.TaskCompletionSource<JObject> tcs)
        {
            int width = parameters["width"]?.ToObject<int>() ?? 1920;
            int height = parameters["height"]?.ToObject<int>() ?? 1080;

            // Clamp to transport-safe limit
            ClampToTransportLimit(ref width, ref height);

            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prevActive = null;
            Camera sceneCamera = null;

            try
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null && SceneView.sceneViews.Count > 0)
                    sceneView = SceneView.sceneViews[0] as SceneView;

                if (sceneView == null)
                {
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                        ToolErrors.NotFound("SceneView"), "not_found"));
                    return;
                }

                sceneCamera = sceneView.camera;
                if (sceneCamera == null)
                {
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                        ToolErrors.NotFound("SceneView camera"), "not_found"));
                    return;
                }

                rt = new RenderTexture(width, height, 24);
                var prevTarget = sceneCamera.targetTexture;
                prevActive = RenderTexture.active;

                sceneCamera.targetTexture = rt;
                sceneCamera.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);

                // Y-flip correction for DirectX / Metal
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    var pixels = tex.GetPixels32();
                    var flipped = new Color32[pixels.Length];
                    for (int y = 0; y < height; y++)
                    {
                        int srcRow = y * width;
                        int dstRow = (height - 1 - y) * width;
                        Array.Copy(pixels, srcRow, flipped, dstRow, width);
                    }
                    tex.SetPixels32(flipped);
                }

                tex.Apply();

                var pngBytes = tex.EncodeToPNG();
                var base64 = Convert.ToBase64String(pngBytes);

                tcs.SetResult(new JObject
                {
                    ["success"] = true,
                    ["type"] = "image",
                    ["mimeType"] = "image/png",
                    ["data"] = base64,
                    ["width"] = width,
                    ["height"] = height,
                    ["message"] = $"Captured Scene View screenshot ({width}x{height})"
                });
            }
            catch (Exception ex)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.ExecutionError("capture Scene View screenshot", ex.Message), "execution_error"));
            }
            finally
            {
                // Cleanup resources
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (sceneCamera != null && rt != null) sceneCamera.targetTexture = null;
                if (prevActive != null) RenderTexture.active = prevActive;
            }
        }

        private static void ClampToTransportLimit(ref int width, ref int height)
        {
            int longest = Mathf.Max(width, height);
            if (longest <= MaxDimension) return;
            float scale = (float)MaxDimension / longest;
            width = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(height * scale));
        }
    }
}
