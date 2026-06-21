using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for finding Unity built-in resources like shaders and materials
    /// </summary>
    public class AssetsFindBuiltInTool : McpToolBase
    {
        private static readonly string[] BuiltInShaderNames = new[]
        {
            "Standard",
            "Sprites/Default",
            "UI/Default",
            "Unlit/Color",
            "Unlit/Texture",
            "Legacy Shaders/Diffuse"
        };

        private static readonly string[] BuiltInMaterialPaths = new[]
        {
            "Default-Material.mat",
            "Default-Diffuse.mat",
            "Default-Line.mat",
            "Default-Particle.mat",
            "Default-Skybox.mat"
        };

        public AssetsFindBuiltInTool()
        {
            Name = "assets_find_built_in";
            Description = "Finds Unity built-in resources such as shaders and materials";
        }

        /// <summary>
        /// Execute the built-in asset search with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            string query = parameters["query"]?.ToObject<string>() ?? string.Empty;
            string assetType = parameters["assetType"]?.ToObject<string>()?.Trim();
            int maxResults = parameters["maxResults"]?.ToObject<int?>() ?? 10;
            maxResults = Mathf.Clamp(maxResults, 1, 100);

            string normalizedQuery = query.Trim();
            string normalizedAssetType = string.IsNullOrEmpty(assetType) ? string.Empty : assetType.Trim();

            JArray assets = new JArray();

            SearchBuiltInShaders(assets, normalizedQuery, normalizedAssetType, maxResults);
            if (assets.Count < maxResults)
            {
                SearchBuiltInMaterials(assets, normalizedQuery, normalizedAssetType, maxResults);
            }

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Found {assets.Count} built-in asset(s)",
                ["assets"] = assets,
                ["count"] = assets.Count
            };
        }

        private static void SearchBuiltInShaders(JArray results, string query, string assetType, int maxResults)
        {
            if (results.Count >= maxResults || !AssetTypeMatches(assetType, "Shader"))
            {
                return;
            }

            foreach (string shaderName in BuiltInShaderNames)
            {
                if (results.Count >= maxResults)
                {
                    break;
                }

                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    continue;
                }

                if (!MatchesQuery(shader.name, "Shader", string.Empty, shaderName, query))
                {
                    continue;
                }

                results.Add(CreateAssetEntry(
                    shader.name,
                    "Shader",
                    string.Empty,
                    shaderName,
                    true,
                    McpObjectId.FromObject(shader)
                ));
            }
        }

        private static void SearchBuiltInMaterials(JArray results, string query, string assetType, int maxResults)
        {
            if (results.Count >= maxResults || !AssetTypeMatches(assetType, "Material"))
            {
                return;
            }

            foreach (string materialPath in BuiltInMaterialPaths)
            {
                if (results.Count >= maxResults)
                {
                    break;
                }

                Material material = AssetDatabase.GetBuiltinExtraResource<Material>(materialPath);
                if (material == null)
                {
                    continue;
                }

                if (!MatchesQuery(material.name, "Material", string.Empty, materialPath, query))
                {
                    continue;
                }

                results.Add(CreateAssetEntry(
                    material.name,
                    "Material",
                    string.Empty,
                    materialPath,
                    true,
                    McpObjectId.FromObject(material)
                ));
            }
        }

        private static bool AssetTypeMatches(string requestedType, string actualType)
        {
            if (string.IsNullOrEmpty(requestedType))
            {
                return true;
            }

            return string.Equals(requestedType.Trim(), actualType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesQuery(string name, string type, string path, string resourcePath, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return ContainsIgnoreCase(name, query)
                || ContainsIgnoreCase(type, query)
                || ContainsIgnoreCase(path, query)
                || ContainsIgnoreCase(resourcePath, query);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(query))
            {
                return false;
            }

            return value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static JObject CreateAssetEntry(string name, string type, string path, string resourcePath, bool isBuiltIn, int instanceId)
        {
            return new JObject
            {
                ["name"] = name,
                ["type"] = type,
                ["path"] = path,
                ["resourcePath"] = resourcePath,
                ["isBuiltIn"] = isBuiltIn,
                ["instanceId"] = instanceId
            };
        }
    }
}
