using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for closing the current prefab stage.
    /// </summary>
    public class PrefabCloseTool : McpToolBase
    {
        public PrefabCloseTool()
        {
            Name = "prefab_close";
            Description = "Closes the current Prefab Stage, optionally saving changes first.";
        }

        public override JObject Execute(JObject parameters)
        {
            bool saveChanges = parameters?["saveChanges"]?.ToObject<bool?>() ?? true;

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "No Prefab Stage is currently open",
                    "validation_error"
                );
            }

            if (saveChanges)
            {
                if (stage.prefabContentsRoot == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Current Prefab Stage does not have a valid prefab contents root to save",
                        "validation_error"
                    );
                }

                try
                {
                    PrefabUtility.SavePrefabAsset(stage.prefabContentsRoot);
                }
                catch (Exception ex)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to save prefab asset '{stage.assetPath}': {ex.Message}",
                        "save_error"
                    );
                }
            }

            StageUtility.GoToMainStage();

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = saveChanges
                    ? $"Closed Prefab Stage and saved changes for '{stage.assetPath}'"
                    : $"Closed Prefab Stage without saving changes for '{stage.assetPath}'"
            };
        }
    }
}
