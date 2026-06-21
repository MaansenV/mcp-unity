using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving data about a scene.
    /// </summary>
    public class SceneGetDataTool : McpToolBase
    {
        public SceneGetDataTool()
        {
            Name = "scene_get_data";
            Description = "Gets scene data including root objects and basic state.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string sceneName = parameters?["sceneName"]?.ToObject<string>();
                string scenePath = parameters?["scenePath"]?.ToObject<string>();

                Scene scene = ResolveScene(sceneName, scenePath);
                if (!scene.IsValid())
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Scene not found",
                        "not_found_error"
                    );
                }

                JArray rootObjects = new JArray();
                if (scene.isLoaded)
                {
                    foreach (GameObject rootObject in scene.GetRootGameObjects())
                    {
                        rootObjects.Add(new JObject
                        {
                            ["instanceId"] = McpObjectId.FromObject(rootObject),
                            ["name"] = rootObject.name,
                            ["activeSelf"] = rootObject.activeSelf,
                            ["childCount"] = rootObject.transform.childCount
                        });
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Retrieved data for scene '{scene.name}'",
                    ["scene"] = new JObject
                    {
                        ["name"] = scene.name,
                        ["path"] = scene.path,
                        ["rootObjects"] = rootObjects,
                        ["isLoaded"] = scene.isLoaded,
                        ["isDirty"] = scene.isDirty,
                        ["buildIndex"] = scene.buildIndex
                    }
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error getting scene data: {ex.Message}",
                    "scene_data_error"
                );
            }
        }

        private static Scene ResolveScene(string sceneName, string scenePath)
        {
            if (string.IsNullOrWhiteSpace(sceneName) && string.IsNullOrWhiteSpace(scenePath))
            {
                return SceneManager.GetActiveScene();
            }

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
