using System;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for listing opened scenes.
    /// </summary>
    public class SceneListOpenedTool : McpToolBase
    {
        public SceneListOpenedTool()
        {
            Name = "scene_list_opened";
            Description = "Lists all currently open scenes in the editor.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                JArray scenes = new JArray();

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    scenes.Add(new JObject
                    {
                        ["name"] = scene.name,
                        ["path"] = scene.path,
                        ["buildIndex"] = scene.buildIndex,
                        ["isLoaded"] = scene.isLoaded,
                        ["isDirty"] = scene.isDirty,
                        ["rootObjectCount"] = scene.isLoaded ? scene.rootCount : 0
                    });
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Found {scenes.Count} open scene(s)",
                    ["scenes"] = scenes,
                    ["count"] = scenes.Count
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error listing open scenes: {ex.Message}",
                    "scene_list_error"
                );
            }
        }
    }
}
