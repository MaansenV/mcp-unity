#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityMCP.Shared;

namespace UnityMCP.Editor
{
    public interface IToolHandler
    {
        Task<object?> ExecuteAsync(ToolContext context);
    }
}
#endif
