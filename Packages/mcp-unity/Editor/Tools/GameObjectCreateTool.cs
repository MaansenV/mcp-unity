using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for creating new GameObjects in the Unity Editor.
    /// Supports empty GameObjects and primitive creation.
    /// </summary>
    public class GameObjectCreateTool : McpToolBase
    {
        public GameObjectCreateTool()
        {
            Name = "gameobject_create";
            Description = "Creates a new GameObject in the Unity scene, optionally as a primitive and optionally under a parent.";
            IsAsync = false;
        }

        /// <summary>
        /// Execute the GameObjectCreate tool with the provided parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            string name = parameters["name"]?.ToObject<string>();
            string primitiveTypeValue = parameters["primitiveType"]?.ToObject<string>();
            string parentPath = parameters["parentPath"]?.ToObject<string>();
            int? parentId = parameters["parentId"]?.ToObject<int?>();
            JObject position = parameters["position"] as JObject;
            JObject rotation = parameters["rotation"] as JObject;
            bool worldSpace = parameters["worldSpace"]?.ToObject<bool?>() ?? true;

            if (string.IsNullOrEmpty(name))
            {
                name = "New GameObject";
            }

            GameObject createdObject = null;

            try
            {
                if (!string.IsNullOrEmpty(primitiveTypeValue) && !primitiveTypeValue.Equals("Empty", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Enum.TryParse(primitiveTypeValue, true, out PrimitiveType primitiveType))
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Invalid primitiveType '{primitiveTypeValue}'. Expected Empty, Cube, Sphere, Capsule, Cylinder, Plane, or Quad.",
                            "validation_error"
                        );
                    }

                    createdObject = GameObject.CreatePrimitive(primitiveType);
                }
                else
                {
                    createdObject = new GameObject(name);
                }

                if (createdObject == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Failed to create the GameObject.",
                        "creation_error"
                    );
                }

                Undo.RegisterCreatedObjectUndo(createdObject, "Create GameObject");

                GameObject parentObject = null;
                if (parentId.HasValue)
                {
                    parentObject = EditorUtility.InstanceIDToObject(parentId.Value) as GameObject;
                    if (parentObject == null)
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Parent GameObject not found with instance ID {parentId.Value}.",
                            "not_found_error"
                        );
                    }
                }
                else if (!string.IsNullOrEmpty(parentPath))
                {
                    parentObject = GameObject.Find(parentPath);
                    if (parentObject == null)
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Parent GameObject not found at path '{parentPath}'.",
                            "not_found_error"
                        );
                    }
                }

                if (parentObject != null)
                {
                    createdObject.transform.SetParent(parentObject.transform, worldSpace);
                }

                if (position != null)
                {
                    Vector3 pos = ReadVector3(position);
                    if (worldSpace || parentObject == null)
                    {
                        createdObject.transform.position = pos;
                    }
                    else
                    {
                        createdObject.transform.localPosition = pos;
                    }
                }

                if (rotation != null)
                {
                    Vector3 euler = ReadVector3(rotation);
                    Quaternion quat = Quaternion.Euler(euler);
                    if (worldSpace || parentObject == null)
                    {
                        createdObject.transform.rotation = quat;
                    }
                    else
                    {
                        createdObject.transform.localRotation = quat;
                    }
                }

                createdObject.name = name;

                Selection.activeGameObject = createdObject;
                EditorGUIUtility.PingObject(createdObject);

                McpLogger.LogInfo($"Created GameObject '{createdObject.name}' with instance ID {createdObject.GetInstanceID()}");

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Successfully created GameObject '{createdObject.name}'.",
                    ["instanceId"] = createdObject.GetInstanceID(),
                    ["name"] = createdObject.name,
                    ["path"] = GetGameObjectPath(createdObject)
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error creating GameObject: {ex.Message}",
                    "creation_error"
                );
            }
        }

        private static Vector3 ReadVector3(JObject data)
        {
            return new Vector3(
                data["x"]?.ToObject<float?>() ?? 0f,
                data["y"]?.ToObject<float?>() ?? 0f,
                data["z"]?.ToObject<float?>() ?? 0f
            );
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return null;
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }
    }
}
