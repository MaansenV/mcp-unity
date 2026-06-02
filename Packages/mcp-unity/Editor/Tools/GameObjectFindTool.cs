using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for finding GameObjects by name, tag, or component type across loaded scenes.
    /// </summary>
    public class GameObjectFindTool : McpToolBase
    {
        private const int MaxAllowedResults = 100;

        public GameObjectFindTool()
        {
            Name = "gameobject_find";
            Description = "Finds GameObjects across all loaded scenes by partial name match, exact tag match, or component type (e.g. Rigidbody).";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string name = parameters?["name"]?.ToObject<string>();
                string tag = parameters?["tag"]?.ToObject<string>();
                string componentType = parameters?["componentType"]?.ToObject<string>();
                int maxResults = parameters?["maxResults"]?.ToObject<int?>() ?? 20;

                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(tag) && string.IsNullOrWhiteSpace(componentType))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "At least one of 'name', 'tag', or 'componentType' must be provided.",
                        "validation_error"
                    );
                }

                if (maxResults < 1)
                {
                    maxResults = 1;
                }
                else if (maxResults > MaxAllowedResults)
                {
                    maxResults = MaxAllowedResults;
                }

                Type resolvedComponentType = ResolveComponentType(componentType);
                if (!string.IsNullOrWhiteSpace(componentType) && resolvedComponentType == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Could not resolve component type '{componentType}'.",
                        "not_found_error"
                    );
                }

                List<JObject> results = new List<JObject>();
                int matchCount = 0;

                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                {
                    Scene scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject rootObject in rootObjects)
                    {
                        TraverseGameObject(rootObject, scene.name, name, tag, resolvedComponentType, results, ref matchCount, maxResults);
                    }
                }

                bool truncated = matchCount > results.Count;

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Found {results.Count} GameObject(s) matching the provided criteria.",
                    ["results"] = new JArray(results),
                    ["count"] = results.Count,
                    ["truncated"] = truncated
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error finding GameObjects: {ex.Message}",
                    "gameobject_search_error"
                );
            }
        }

        private static void TraverseGameObject(
            GameObject gameObject,
            string sceneName,
            string nameFilter,
            string tagFilter,
            Type componentType,
            List<JObject> results,
            ref int matchCount,
            int maxResults)
        {
            if (gameObject == null)
            {
                return;
            }

            if (MatchesCriteria(gameObject, nameFilter, tagFilter, componentType))
            {
                matchCount++;

                if (results.Count < maxResults)
                {
                    results.Add(new JObject
                    {
                        ["instanceId"] = gameObject.GetInstanceID(),
                        ["name"] = gameObject.name,
                        ["path"] = GetHierarchyPath(gameObject),
                        ["tag"] = gameObject.tag,
                        ["activeSelf"] = gameObject.activeSelf,
                        ["sceneName"] = sceneName
                    });
                }
            }

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                TraverseGameObject(transform.GetChild(i).gameObject, sceneName, nameFilter, tagFilter, componentType, results, ref matchCount, maxResults);
            }
        }

        private static bool MatchesCriteria(GameObject gameObject, string nameFilter, string tagFilter, Type componentType)
        {
            bool matches = false;

            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                matches |= gameObject.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (!string.IsNullOrWhiteSpace(tagFilter))
            {
                matches |= string.Equals(gameObject.tag, tagFilter, StringComparison.Ordinal);
            }

            if (componentType != null)
            {
                matches |= gameObject.GetComponent(componentType) != null;
            }

            return matches;
        }

        private static Type ResolveComponentType(string componentTypeName)
        {
            if (string.IsNullOrWhiteSpace(componentTypeName))
            {
                return null;
            }

            Type type = Type.GetType(componentTypeName, false);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(componentTypeName, false, true);
                if (type != null)
                {
                    return type;
                }

                type = assembly.GetTypes().FirstOrDefault(t => string.Equals(t.Name, componentTypeName, StringComparison.OrdinalIgnoreCase));
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            List<string> segments = new List<string>();
            Transform current = gameObject.transform;

            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
