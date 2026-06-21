using System;
using System.Reflection;
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
    /// <c>Object.GetInstanceID()</c>) as obsolete-with-error (CS0619). The
    /// legacy APIs still exist and work in Unity 6.x but are slated for
    /// removal in a future version.
    ///
    /// The CS0619 obsolete-with-error cannot be suppressed via
    /// <c>#pragma warning disable CS0619</c> on Unity 6's compiler. We
    /// therefore call the legacy APIs through reflection at runtime, which
    /// bypasses the compile-time obsolete check entirely. If Unity ever
    /// removes these methods, the reflection call returns null / 0
    /// gracefully and the caller can report a clear error.
    /// </summary>
    internal static class McpObjectId
    {
#if UNITY_6000_0_OR_NEWER
        private static readonly MethodInfo s_instanceIdToObjectMethod =
            typeof(EditorUtility).GetMethod(
                "InstanceIDToObject",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null);

        private static readonly MethodInfo s_getInstanceIdMethod =
            typeof(UnityEngine.Object).GetMethod(
                "GetInstanceID",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
#endif

        /// <summary>
        /// Resolves a numeric InstanceID to a <see cref="UnityEngine.Object"/>.
        /// </summary>
        /// <param name="instanceId">Legacy int InstanceID as exposed by the MCP API.</param>
        /// <returns>The matching <see cref="UnityEngine.Object"/>, or null if not found / id is 0 / Unity removed the API.</returns>
        public static UnityEngine.Object ToObject(int instanceId)
        {
#if UNITY_6000_0_OR_NEWER
            if (s_instanceIdToObjectMethod == null)
            {
                return null;
            }
            try
            {
                return s_instanceIdToObjectMethod.Invoke(null, new object[] { instanceId }) as UnityEngine.Object;
            }
            catch (TargetInvocationException)
            {
                return null;
            }
#else
            return EditorUtility.InstanceIDToObject(instanceId);
#endif
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
#if UNITY_6000_0_OR_NEWER
            if (s_getInstanceIdMethod == null)
            {
                return 0;
            }
            try
            {
                return (int)s_getInstanceIdMethod.Invoke(obj, null);
            }
            catch (TargetInvocationException)
            {
                return 0;
            }
#else
            return obj.GetInstanceID();
#endif
        }
    }
}
