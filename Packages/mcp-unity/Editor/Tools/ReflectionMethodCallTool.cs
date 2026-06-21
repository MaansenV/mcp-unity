using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for invoking methods via reflection.
    /// </summary>
    public class ReflectionMethodCallTool : McpToolBase
    {
        public ReflectionMethodCallTool()
        {
            Name = "reflection_method_call";
            Description = "Finds a type and method by name, then invokes it with optional parameters.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string typeName = parameters?["typeName"]?.ToObject<string>();
                string methodName = parameters?["methodName"]?.ToObject<string>();
                JArray parameterTokens = parameters?["parameters"] as JArray;
                int? instanceId = parameters?["instanceId"]?.ToObject<int?>();

                if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(methodName))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Required parameters 'typeName' and 'methodName' must be provided",
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

                object target = null;
                if (!TryGetMethodTarget(type, instanceId, out target, out JObject targetError))
                {
                    return targetError;
                }

                object[] args = parameterTokens != null
                    ? parameterTokens.Select(token => JTokenToObject(token)).ToArray()
                    : Array.Empty<object>();

                MethodInfo method = FindMethod(type, methodName, args.Length, target == null);
                if (method == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Method '{methodName}' not found on type '{type.FullName}' for {args.Length} parameter(s)",
                        "not_found_error"
                    );
                }

                ParameterInfo[] parameterInfos = method.GetParameters();
                object[] convertedArgs = ConvertArguments(args, parameterInfos, out string conversionError);
                if (conversionError != null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(conversionError, "validation_error");
                }

                object result = null;
                try
                {
                    result = method.Invoke(target, convertedArgs);
                }
                catch (TargetInvocationException ex)
                {
                    Exception inner = ex.InnerException ?? ex;
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Method invocation failed: {inner.Message}",
                        "reflection_method_call_error"
                    );
                }
                catch (Exception ex)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Invocation error: {ex.Message}",
                        "reflection_method_call_error"
                    );
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Invoked {type.FullName}.{method.Name}",
                    ["result"] = result != null ? JToken.FromObject(result) : JValue.CreateNull()
                };
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error invoking method: {inner.Message}",
                    "reflection_method_call_error"
                );
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error invoking method: {ex.Message}",
                    "reflection_method_call_error"
                );
            }
        }

        private static object JTokenToObject(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            switch (token.Type)
            {
                case JTokenType.String: return token.ToObject<string>();
                case JTokenType.Integer: return token.ToObject<int>();
                case JTokenType.Float: return token.ToObject<float>();
                case JTokenType.Boolean: return token.ToObject<bool>();
                default: return token.ToString();
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
                catch (ReflectionTypeLoadException)
                {
                    // Some assemblies may fail to load types; skip them
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool TryGetMethodTarget(Type type, int? instanceId, out object target, out JObject error)
        {
            target = null;
            error = null;

            bool requiresInstance = !type.IsAbstract && !type.IsSealed;
            bool canInvokeStatic = true;

            if (instanceId.HasValue)
            {
                target = McpObjectId.ToObject(instanceId.Value);
                if (target == null)
                {
                    error = McpUnitySocketHandler.CreateErrorResponse(
                        $"Object with instance ID {instanceId.Value} not found",
                        "not_found_error"
                    );
                    return false;
                }

                if (!type.IsAssignableFrom(target.GetType()))
                {
                    error = McpUnitySocketHandler.CreateErrorResponse(
                        $"Instance ID {instanceId.Value} is not assignable to '{type.FullName}'",
                        "validation_error"
                    );
                    return false;
                }

                return true;
            }

            if (requiresInstance)
            {
                // We don't know whether the selected method is static until after lookup; allow lookup to continue.
                canInvokeStatic = false;
            }

            return canInvokeStatic || !requiresInstance;
        }

        private static MethodInfo FindMethod(Type type, string methodName, int parameterCount, bool staticOnly)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            return type.GetMethods(flags)
                .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
                .Where(m => m.GetParameters().Length == parameterCount)
                .Where(m => !staticOnly || m.IsStatic)
                .FirstOrDefault();
        }

        private static object[] ConvertArguments(object[] args, ParameterInfo[] parameters, out string error)
        {
            error = null;
            if (parameters.Length == 0)
            {
                return Array.Empty<object>();
            }

            object[] converted = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                object value = i < args.Length ? args[i] : Type.Missing;
                if (value == null || value == Type.Missing)
                {
                    if (parameters[i].IsOptional)
                    {
                        converted[i] = Type.Missing;
                        continue;
                    }

                    if (parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null)
                    {
                        error = $"Parameter '{parameters[i].Name}' cannot be null";
                        return null;
                    }

                    converted[i] = null;
                    continue;
                }

                if (!TryConvertValue(value, parameters[i].ParameterType, out object convertedValue, out error))
                {
                    return null;
                }

                converted[i] = convertedValue;
            }

            return converted;
        }

        private static bool TryConvertValue(object value, Type targetType, out object convertedValue, out string error)
        {
            error = null;
            convertedValue = null;

            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                if (value is JObject obj && typeof(UnityEngine.Object).IsAssignableFrom(underlyingType))
                {
                    int? instanceId = obj["instanceId"]?.ToObject<int?>();
                    if (instanceId.HasValue)
                    {
                        convertedValue = McpObjectId.ToObject(instanceId.Value);
                        return true;
                    }
                }

                if (underlyingType == typeof(string))
                {
                    convertedValue = value.ToString();
                    return true;
                }

                if (underlyingType == typeof(object))
                {
                    if (value is JToken jtoken)
                    {
                        switch (jtoken.Type)
                        {
                            case JTokenType.String:
                                convertedValue = jtoken.ToObject<string>();
                                break;
                            case JTokenType.Integer:
                                convertedValue = jtoken.ToObject<int>();
                                break;
                            case JTokenType.Float:
                                convertedValue = jtoken.ToObject<float>();
                                break;
                            case JTokenType.Boolean:
                                convertedValue = jtoken.ToObject<bool>();
                                break;
                            default:
                                convertedValue = jtoken.ToString();
                                break;
                        }
                    }
                    else
                    {
                        convertedValue = value;
                    }
                    return true;
                }

                if (underlyingType.IsEnum)
                {
                    if (value is string enumName)
                    {
                        convertedValue = Enum.Parse(underlyingType, enumName, true);
                        return true;
                    }

                    convertedValue = Enum.ToObject(underlyingType, Convert.ChangeType(value, Enum.GetUnderlyingType(underlyingType)));
                    return true;
                }

                if (underlyingType == typeof(bool))
                {
                    convertedValue = Convert.ToBoolean(value);
                    return true;
                }

                if (underlyingType == typeof(int))
                {
                    convertedValue = Convert.ToInt32(value);
                    return true;
                }

                if (underlyingType == typeof(float))
                {
                    convertedValue = Convert.ToSingle(value);
                    return true;
                }

                if (underlyingType == typeof(double))
                {
                    convertedValue = Convert.ToDouble(value);
                    return true;
                }

                if (underlyingType == typeof(long))
                {
                    convertedValue = Convert.ToInt64(value);
                    return true;
                }

                if (underlyingType == typeof(JToken))
                {
                    convertedValue = value;
                    return true;
                }

                if (value != null && underlyingType.IsAssignableFrom(value.GetType()))
                {
                    convertedValue = value;
                    return true;
                }

                convertedValue = Convert.ChangeType(value, underlyingType);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to convert argument to '{underlyingType.Name}': {ex.Message}";
                return false;
            }
        }
    }
}
