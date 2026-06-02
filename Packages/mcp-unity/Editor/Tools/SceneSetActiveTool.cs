using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for setting the active scene.
    /// </summary>
    public class SceneSetActiveTool : McpToolBase
    {
        public SceneSetActiveTool()
        {
            Name = "scene_set_active";
            Description = "Finds a scene by name or path and sets it as the active scene.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string sceneName = parameters?["sceneName"]?.ToObject<string>();
                string scenePath = parameters?["scenePath"]?.ToObject<string>();

                if (string.IsNullOrWhiteSpace(sceneName) && string.IsNullOrWhiteSpace(scenePath))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Either 'sceneName' or 'scenePath' must be provided",
                        "validation_error"
                    );
                }

                Scene scene = FindScene(sceneName, scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Scene not found or not loaded",
                        "not_found_error"
                    );
                }

                if (scene.name == SceneManager.GetActiveScene().name)
                {
                    return new JObject
                    {
                        ["success"] = true,
                        ["type"] = "text",
                        ["message"] = $"Scene '{scene.name}' is already the active scene",
                        ["scene"] = new JObject
                        {
                            ["name"] = scene.name,
                            ["path"] = scene.path
                        },
                        ["alreadyActive"] = true
                    };
                }

                if (!SceneManager.SetActiveScene(scene))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to set scene '{scene.name}' as active",
                        "scene_error"
                    );
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Set active scene to '{scene.name}'",
                    ["scene"] = new JObject
                    {
                        ["name"] = scene.name,
                        ["path"] = scene.path
                    }
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error setting active scene: {ex.Message}",
                    "scene_error"
                );
            }
        }

        private static Scene FindScene(string sceneName, string scenePath)
        {
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                Scene sceneByPath = SceneManager.GetSceneByPath(scenePath);
                if (sceneByPath.IsValid())
                {
                    return sceneByPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                Scene sceneByName = SceneManager.GetSceneByName(sceneName);
                if (sceneByName.IsValid())
                {
                    return sceneByName;
                }

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        return scene;
                    }
                }
            }

            return default(Scene);
        }
    }
}
