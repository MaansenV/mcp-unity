using System;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for refreshing the Unity AssetDatabase.
    /// </summary>
    public class AssetsRefreshTool : McpToolBase
    {
        public AssetsRefreshTool()
        {
            Name = "assets_refresh";
            Description = "Refreshes the Unity AssetDatabase with optional import options";
        }

        /// <summary>
        /// Execute the AssetsRefresh tool with the provided parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                string option = parameters?["option"]?.ToObject<string>() ?? "Default";

                ImportAssetOptions importOptions;
                switch (option)
                {
                    case "ForceUpdate":
                        importOptions = ImportAssetOptions.ForceUpdate;
                        break;
                    case "ForceSynchronousImport":
                        importOptions = ImportAssetOptions.ForceSynchronousImport;
                        break;
                    case "ImportRecursive":
                        importOptions = ImportAssetOptions.ImportRecursive;
                        break;
                    case "Default":
                        importOptions = ImportAssetOptions.Default;
                        break;
                    default:
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Invalid option '{option}'. Valid options are: Default, ForceUpdate, ForceSynchronousImport, ImportRecursive",
                            "validation_error"
                        );
                }

                AssetDatabase.Refresh(importOptions);

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Successfully refreshed AssetDatabase using option '{option}'",
                    ["option"] = option
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error refreshing AssetDatabase: {ex.Message}",
                    "asset_refresh_error"
                );
            }
        }
    }
}
