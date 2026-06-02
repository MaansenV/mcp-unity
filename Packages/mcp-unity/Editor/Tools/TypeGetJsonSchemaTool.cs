using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for generating a simple JSON schema for a type.
    /// </summary>
    public class TypeGetJsonSchemaTool : McpToolBase
    {
        public TypeGetJsonSchemaTool()
        {
            Name = "type_get_json_schema";
            Description = "Builds a simple JSON schema describing a type's public properties and methods.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string typeName = parameters?["typeName"]?.ToObject<string>();
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Required parameter 'typeName' not provided",
                        "validation_error"
                    );
                }

                Type type = FindType(typeName);
                if (type == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Type '{typeName}' not found",
                        "not_found_error"
                    );
                }

                JObject schema = BuildSchema(type);
                string schemaJson = schema.ToString();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Generated JSON schema for '{type.FullName}'",
                    ["schema"] = schemaJson
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error generating JSON schema: {ex.Message}",
                    "type_schema_error"
                );
            }
        }

        private static Type FindType(string typeName)
        {
            Type type = Type.GetType(typeName, false);
            if (type != null)
            {
                return type;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName, false, true);
                    if (type != null)
                    {
                        return type;
                    }

                    type = assembly.GetTypes().FirstOrDefault(t => string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static JObject BuildSchema(Type type)
        {
            var properties = new JObject();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                properties[property.Name] = BuildTypeSchema(property.PropertyType);
            }

            var methods = new JArray();
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                methods.Add(new JObject
                {
                    ["name"] = method.Name,
                    ["returnType"] = method.ReturnType.FullName ?? method.ReturnType.Name,
                    ["isStatic"] = method.IsStatic,
                    ["parameters"] = new JArray(method.GetParameters().Select(p => new JObject
                    {
                        ["name"] = p.Name,
                        ["type"] = p.ParameterType.FullName ?? p.ParameterType.Name,
                        ["isOptional"] = p.IsOptional
                    }))
                });
            }

            return new JObject
            {
                ["title"] = type.FullName,
                ["type"] = "object",
                ["properties"] = properties,
                ["methods"] = methods
            };
        }

        private static JObject BuildTypeSchema(Type type)
        {
            Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            if (underlyingType == typeof(string))
            {
                return new JObject { ["type"] = "string" };
            }

            if (underlyingType == typeof(bool))
            {
                return new JObject { ["type"] = "boolean" };
            }

            if (underlyingType == typeof(int) || underlyingType == typeof(long) || underlyingType == typeof(short))
            {
                return new JObject { ["type"] = "integer" };
            }

            if (underlyingType == typeof(float) || underlyingType == typeof(double) || underlyingType == typeof(decimal))
            {
                return new JObject { ["type"] = "number" };
            }

            if (underlyingType.IsEnum)
            {
                return new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray(underlyingType.GetEnumNames())
                };
            }

            if (underlyingType.IsArray)
            {
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = BuildTypeSchema(underlyingType.GetElementType())
                };
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(underlyingType) && underlyingType != typeof(JToken))
            {
                return new JObject { ["type"] = "array" };
            }

            return new JObject
            {
                ["type"] = "object",
                ["$refType"] = underlyingType.FullName ?? underlyingType.Name
            };
        }
    }
}
