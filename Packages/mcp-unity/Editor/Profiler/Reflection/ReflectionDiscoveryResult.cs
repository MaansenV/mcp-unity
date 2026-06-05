using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public class ReflectionDiscoveryResult
    {
        public bool ProfilerDriverTypeFound { get; set; }
        public string ProfilerDriverTypeName { get; set; }

        public IReadOnlyDictionary<string, bool> Properties { get; set; }
        public IReadOnlyDictionary<string, bool> Methods { get; set; }

        public IReadOnlyList<string> MissingRequiredMembers { get; set; }
        public IReadOnlyList<string> Warnings { get; set; }

        public bool HasMember(string logicalName)
        {
            return (Properties?.ContainsKey(logicalName) == true && Properties[logicalName]) ||
                   (Methods?.ContainsKey(logicalName) == true && Methods[logicalName]);
        }
    }
}