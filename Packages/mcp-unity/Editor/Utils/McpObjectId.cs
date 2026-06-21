using UnityEngine;
using UnityEditor;

namespace McpUnity.Utils
{
    /// <summary>
    /// Compatibility bridge between Unity 2022.3's int-based InstanceID
    /// and Unity 6's EntityId-based API.
    ///
    /// The MCP wire format exchanges "instanceId" as an int. Unity 6 has
    /// introduced the <see cref="UnityEngine.EntityId"/> struct as the
    /// canonical handle and marked the legacy int APIs
    /// (<c>EditorUtility.InstanceIDToObject(int)</c> and
    /// <c>Object.GetInstanceID()</c>) as obsolete-with-error. The legacy
    /// APIs still exist and work in Unity 6.x but are slated for removal
    /// in a future version.
    ///
    /// Because <see cref="UnityEngine.EntityId"/> has no public constructor
    /// accepting an int and its implicit cast to int is itself
    /// obsolete-with-error, the only viable bridge today is to keep calling
    /// the legacy int APIs and suppress CS0619 explicitly. When Unity
    /// actually removes these APIs, the MCP wire format will need to switch
    /// to a string-based EntityId encoding.
    /// </summary>
    internal static class McpObjectId
    {
        /// <summary>
        /// Resolves a numeric InstanceID to a <see cref="UnityEngine.Object"/>.
        /// </summary>
        /// <param name="instanceId">Legacy int InstanceID as exposed by the MCP API.</param>
        /// <returns>The matching <see cref="UnityEngine.Object"/>, or null if not found / id is 0.</returns>
        public static UnityEngine.Object ToObject(int instanceId)
        {
#pragma warning disable CS0619 // EditorUtility.InstanceIDToObject(int) is obsolete in Unity 6; still works until removal.
            return EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0619
        }

        /// <summary>
        /// Returns the numeric InstanceID for a <see cref="UnityEngine.Object"/>.
        /// </summary>
        /// <param name="obj">Object whose ID should be returned. null yields 0.</param>
        public static int FromObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return 0;
            }
#pragma warning disable CS0619 // Object.GetInstanceID() is obsolete in Unity 6; still works until removal.
            return obj.GetInstanceID();
#pragma warning restore CS0619
        }
    }
}
