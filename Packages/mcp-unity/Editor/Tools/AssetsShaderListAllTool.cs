using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for listing shaders in the project.
    /// </summary>
    public class AssetsShaderListAllTool : McpToolBase
    {
        private const int DefaultMaxResults = 50;
        private const int HardMaxResults = 500;

        public AssetsShaderListAllTool()
        {
            Name = "assets_shader_list_all";
            Description = "Lists shaders in the project using the AssetDatabase.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string search = parameters?["search"]?.ToObject<string>();
                int maxResults = parameters?["maxResults"]?.ToObject<int?>() ?? DefaultMaxResults;

                if (maxResults < 1)
                {
                    maxResults = DefaultMaxResults;
                }
                else if (maxResults > HardMaxResults)
                {
                    maxResults = HardMaxResults;
                }

                string[] guids = AssetDatabase.FindAssets("t:Shader");
                JArray shaders = new JArray();
                int matchedCount = 0;

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (shader == null)
                    {
                        continue;
                    }

                    if (!MatchesSearch(shader.name, path, guid, search))
                    {
                        continue;
                    }

                    matchedCount++;
                    if (shaders.Count < maxResults)
                    {
                        shaders.Add(new JObject
                        {
                            ["name"] = shader.name,
                            ["path"] = path,
                            ["guid"] = guid,
                            ["isSupported"] = shader.isSupported
                        });
                    }
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Found {shaders.Count} shader(s)",
                    ["shaders"] = shaders,
                    ["count"] = shaders.Count,
                    ["totalMatches"] = matchedCount,
                    ["truncated"] = matchedCount > shaders.Count
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error listing shaders: {ex.Message}",
                    "shader_list_error"
                );
            }
        }

        private static bool MatchesSearch(string name, string path, string guid, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return ContainsIgnoreCase(name, search)
                || ContainsIgnoreCase(path, search)
                || ContainsIgnoreCase(guid, search);
        }

        private static bool ContainsIgnoreCase(string value, string search)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(search))
            {
                return false;
            }

            return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
