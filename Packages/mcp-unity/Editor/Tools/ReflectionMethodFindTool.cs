using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for finding methods across loaded assemblies.
    /// </summary>
    public class ReflectionMethodFindTool : McpToolBase
    {
        private const int DefaultMaxResults = 20;
        private const int HardMaxResults = 100;

        public ReflectionMethodFindTool()
        {
            Name = "reflection_method_find";
            Description = "Searches loaded assemblies for methods matching optional type, method, and text filters.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string typeName = parameters?["typeName"]?.ToObject<string>();
                string methodName = parameters?["methodName"]?.ToObject<string>();
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

                List<JObject> methods = new List<JObject>();

                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (Type type in types)
                    {
                        if (!MatchesTypeFilter(type, typeName))
                        {
                            continue;
                        }

                        MethodInfo[] methodInfos;
                        try
                        {
                            methodInfos = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                        }
                        catch
                        {
                            continue;
                        }

                        foreach (MethodInfo method in methodInfos)
                        {
                            if (!MatchesMethodFilter(method, methodName, search, type))
                            {
                                continue;
                            }

                            methods.Add(CreateMethodEntry(type, method));
                            if (methods.Count >= maxResults)
                            {
                                return CreateResult(methods, true);
                            }
                        }
                    }
                }

                return CreateResult(methods, false);
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error finding reflection methods: {ex.Message}",
                    "reflection_method_find_error"
                );
            }
        }

        private static bool MatchesTypeFilter(Type type, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return true;
            }

            return type.FullName.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0
                || type.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesMethodFilter(MethodInfo method, string methodName, string search, Type type)
        {
            if (!string.IsNullOrWhiteSpace(methodName)
                && method.Name.IndexOf(methodName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            string parameters = string.Join(",", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            string haystack = string.Join(" ", new[]
            {
                type.FullName,
                type.Name,
                method.Name,
                method.ReturnType.Name,
                parameters
            }.Where(s => !string.IsNullOrEmpty(s)));

            return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static JObject CreateMethodEntry(Type type, MethodInfo method)
        {
            return new JObject
            {
                ["typeName"] = type.FullName,
                ["methodName"] = method.Name,
                ["returnType"] = method.ReturnType.FullName ?? method.ReturnType.Name,
                ["parameters"] = new JArray(method.GetParameters().Select(p => new JObject
                {
                    ["name"] = p.Name,
                    ["typeName"] = p.ParameterType.FullName ?? p.ParameterType.Name,
                    ["isOut"] = p.IsOut,
                    ["isOptional"] = p.IsOptional
                })),
                ["isStatic"] = method.IsStatic,
                ["isPublic"] = method.IsPublic
            };
        }

        private static JObject CreateResult(List<JObject> methods, bool truncated)
        {
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Found {methods.Count} method(s)",
                ["methods"] = new JArray(methods),
                ["count"] = methods.Count,
                ["truncated"] = truncated
            };
        }
    }
}
