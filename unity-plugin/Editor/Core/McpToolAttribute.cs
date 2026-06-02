#if UNITY_EDITOR
using System;

namespace UnityMCP.Editor
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class McpToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }

        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}
#endif
