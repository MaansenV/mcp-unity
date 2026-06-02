using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for listing all available component types.
    /// </summary>
    public class GameObjectComponentListAllTool : McpToolBase
    {
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 200;

        public GameObjectComponentListAllTool()
        {
            Name = "gameobject_component_list_all";
            Description = "Lists all available Component types across loaded assemblies, with optional search filtering and pagination.";
        }

        public override JObject Execute(JObject parameters)
        {
            string search = parameters?["search"]?.ToObject<string>();
            int page = parameters?["page"]?.ToObject<int?>() ?? 1;
            int pageSize = parameters?["pageSize"]?.ToObject<int?>() ?? DefaultPageSize;

            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = DefaultPageSize;
            }
            else if (pageSize > MaxPageSize)
            {
                pageSize = MaxPageSize;
            }

            List<Type> componentTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(t => typeof(Component).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericType)
                .Distinct()
                .OrderBy(t => t.Name)
                .ToList();

            if (!string.IsNullOrEmpty(search))
            {
                componentTypes = componentTypes
                    .Where(t => t.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            int total = componentTypes.Count;
            int totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
            int skip = (page - 1) * pageSize;

            var pagedComponents = componentTypes
                .Skip(skip)
                .Take(pageSize)
                .Select(t => new JObject
                {
                    ["name"] = t.Name,
                    ["fullName"] = t.FullName,
                    ["namespace"] = t.Namespace,
                    ["assembly"] = t.Assembly.GetName().Name
                })
                .ToList();

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Found {total} component types",
                ["components"] = new JArray(pagedComponents),
                ["count"] = pagedComponents.Count,
                ["total"] = total,
                ["page"] = page,
                ["pageSize"] = pageSize,
                ["totalPages"] = totalPages
            };
        }

        private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch (Exception)
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}
