using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for capturing a screenshot of the Unity Game View.
    /// Reads the Game View's internal render texture via reflection.
    /// Returns the image as a base64-encoded PNG.
    /// </summary>
    public class ScreenshotGameViewTool : McpToolBase
    {
        private const int MaxDimension = 3840;

        public ScreenshotGameViewTool()
        {
            Name = "screenshot_game_view";
            Description = "Captures a screenshot of the Unity Game View and returns it as a base64-encoded PNG";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, System.Threading.Tasks.TaskCompletionSource<JObject> tcs)
        {
            try
            {
                // GameView is an internal UnityEditor type — use reflection
                var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType == null)
                {
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                        ToolErrors.NotFound("GameView type"), "not_found"));
                    return;
                }

                var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                if (gameView == null)
                {
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                        ToolErrors.NotFound("GameView window"), "not_found"));
                    return;
                }

                gameView.Repaint();

                // Reflect private m_RenderTexture field
                var rtField = gameViewType.GetField("m_RenderTexture",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (rtField == null)
                {
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                        ToolErrors.NotFound("GameView m_RenderTexture field"), "not_found"));
                    return;
                }

                var sourceRt = rtField.GetValue(gameView) as RenderTexture;
                if (sourceRt == null || sourceRt.width == 0 || sourceRt.height == 0)
                {
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                        ToolErrors.NotFound("GameView render texture (is Play Mode active?)"), "not_found"));
                    return;
                }

                int srcWidth = sourceRt.width;
                int srcHeight = sourceRt.height;

                // Downscale if exceeds max dimension
                float scale = Mathf.Min(1f, (float)MaxDimension / Mathf.Max(srcWidth, srcHeight));
                int width = Mathf.Max(1, Mathf.RoundToInt(srcWidth * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(srcHeight * scale));

                RenderTexture readSource = sourceRt;
                RenderTexture scaledRt = null;

                if (scale < 1f)
                {
                    scaledRt = RenderTexture.GetTemporary(width, height, 0, sourceRt.format);
                    Graphics.Blit(sourceRt, scaledRt);
                    readSource = scaledRt;
                }

                var prevActive = RenderTexture.active;
                RenderTexture.active = readSource;

                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
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

                // Cleanup
                UnityEngine.Object.DestroyImmediate(tex);
                if (scaledRt != null) RenderTexture.ReleaseTemporary(scaledRt);
                RenderTexture.active = prevActive;

                tcs.SetResult(new JObject
                {
                    ["success"] = true,
                    ["type"] = "image",
                    ["mimeType"] = "image/png",
                    ["data"] = base64,
                    ["width"] = width,
                    ["height"] = height,
                    ["originalWidth"] = srcWidth,
                    ["originalHeight"] = srcHeight,
                    ["message"] = $"Captured Game View screenshot ({width}x{height})"
                });
            }
            catch (Exception ex)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.ExecutionError("capture Game View screenshot", ex.Message), "execution_error"));
            }
        }
    }
}
