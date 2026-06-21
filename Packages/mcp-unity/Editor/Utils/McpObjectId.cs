using UnityEngine;
using UnityEditor;

namespace McpUnity.Utils
{
    /// <summary>
    /// Compatibility bridge between Unity 2022.3's int-based InstanceID
    /// and Unity 6's EntityId struct. Centralises the version-conditional
    /// code so call sites stay readable and the package compiles on both
    /// Unity 2022.3+ and Unity 6000.0+.
    /// </summary>
    internal static class McpObjectId
    {
        /// <summary>
        /// Resolves a numeric InstanceID to a UnityEngine.Object, working
        /// across Unity 2022.3 and Unity 6.
        /// </summary>
        /// <param name="instanceId">Legacy int InstanceID as exposed by the MCP API.</param>
        /// <returns>The matching UnityEngine.Object, or null if not found / id is 0.</returns>
        public static UnityEngine.Object ToObject(int instanceId)
        {
#if UNITY_6000_0_OR_NEWER
            return EditorUtility.EntityIdToObject(new EntityId(instanceId));
#else
            return EditorUtility.InstanceIDToObject(instanceId);
#endif
        }

        /// <summary>
        /// Returns the numeric ID for a UnityEngine.Object in a form that is
        /// stable across Unity 2022.3 and Unity 6 (the cast on EntityId is
        /// explicit and yields the same integer as the legacy GetInstanceID).
        /// </summary>
        /// <param name="obj">Object whose ID should be returned. null yields 0.</param>
        public static int FromObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return 0;
            }
#if UNITY_6000_0_OR_NEWER
            return (int)obj.GetEntityId();
#else
            return obj.GetInstanceID();
#endif
        }
    }
}
