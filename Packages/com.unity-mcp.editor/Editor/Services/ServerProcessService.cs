#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityMCP.Editor.Settings;
using UnityMCP.Editor.Logging;

namespace UnityMCP.Editor.Services
{
        public class ServerStartResult
        {
            public bool Success;
            public int? ProcessId;
            public string Error;
            /// <summary>True when this call actually started a new process (vs. reusing an existing one).</summary>
            public bool StartedNewProcess;
        }
    
    public static class ServerProcessService
    {
        private static Process s_ServerProcess;
        private static McpLogBuffer s_LogBuffer;
        private static readonly object s_Lock = new object();
        
        public static bool IsRunning
        {
            get
            {
                lock (s_Lock)
                {
                    return s_ServerProcess != null && !s_ServerProcess.HasExited;
                }
            }
        }
        
        public static int? ProcessId
        {
            get
            {
                lock (s_Lock)
                {
                    return s_ServerProcess?.Id;
                }
            }
        }
        
        public static void SetLogBuffer(McpLogBuffer logBuffer) => s_LogBuffer = logBuffer;
        
        /// <summary>
        /// Starts the server in websocket-only mode for validation / health-check purposes.
        /// Call <see cref="StopAsync"/> afterwards to release the port.
        /// </summary>
        public static async Task<ServerStartResult> StartForHealthCheckAsync(McpSettings settings, CancellationToken ct = default)
        {
            return await StartInternalAsync(settings, "--mode websocket-only", ct);
        }

        /// <summary>
        /// Starts the server in the default mode (stdio when invoked by an MCP client).
        /// </summary>
        public static async Task<ServerStartResult> StartAsync(McpSettings settings, CancellationToken ct = default)
        {
            return await StartInternalAsync(settings, null, ct);
        }

        private static async Task<ServerStartResult> StartInternalAsync(McpSettings settings, string arguments, CancellationToken ct)
        {
            var result = new ServerStartResult();
            
            if (IsRunning)
            {
                result.Success = true;
                result.ProcessId = ProcessId;
                result.StartedNewProcess = false;
                return result;
            }
            
            if (!File.Exists(settings.ServerBuildOutputPath))
            {
                result.Error = $"Server binary not found: {settings.ServerBuildOutputPath}";
                return result;
            }
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = settings.ServerBuildOutputPath,
                    Arguments = arguments ?? "",
                    WorkingDirectory = settings.ServerRootPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                // Set environment variables
                psi.EnvironmentVariables["UNITY_MCP_WS_HOST"] = settings.Host;
                psi.EnvironmentVariables["UNITY_MCP_WS_PORT"] = settings.Port.ToString();
                psi.EnvironmentVariables["UNITY_MCP_WS_PATH"] = settings.Path;
                psi.EnvironmentVariables["UNITY_MCP_PROJECT_PATH"] = UnityMCP.Editor.Settings.McpPathUtility.GetProjectRoot();
                
                // Generate and set auth token if configured
                if (!string.IsNullOrEmpty(settings.AuthToken))
                {
                    psi.EnvironmentVariables["UNITY_MCP_AUTH_TOKEN"] = settings.AuthToken;
                }
                
                Process process;
                lock (s_Lock)
                {
                    s_ServerProcess = Process.Start(psi);
                    process = s_ServerProcess;
                }
                
                if (process == null)
                {
                    result.Error = "Failed to start server process";
                    return result;
                }
                
                // Setup output reading
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        MainThreadDispatcher.Enqueue(() => 
                            s_LogBuffer?.Add(McpLogLevel.Info, McpLogCategory.Server, e.Data));
                    }
                };
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        MainThreadDispatcher.Enqueue(() => 
                            s_LogBuffer?.Add(McpLogLevel.Info, McpLogCategory.Server, e.Data));
                    }
                };
                
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                // Wait a bit for server to start
                await Task.Delay(1000, ct);
                
                if (process.HasExited)
                {
                    result.Error = $"Server exited immediately with code {process.ExitCode}";
                    lock (s_Lock) { s_ServerProcess = null; }
                    return result;
                }
                
                result.Success = true;
                result.ProcessId = process.Id;
                result.StartedNewProcess = true;
                
                var modeLabel = string.IsNullOrWhiteSpace(arguments) ? "default" : arguments.Replace("--mode ", "");
                MainThreadDispatcher.Enqueue(() => 
                    s_LogBuffer?.Add(McpLogLevel.Info, McpLogCategory.Server, 
                        $"Server started with PID {process.Id} in {modeLabel} mode"));
            }
            catch (System.Exception ex)
            {
                result.Error = $"Failed to start server: {ex.Message}";
            }
            
            return result;
        }
        
        public static async Task StopAsync(CancellationToken ct = default)
        {
            Process process;
            lock (s_Lock)
            {
                process = s_ServerProcess;
                s_ServerProcess = null;
            }
            
            if (process == null) return;
            
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    // Wait for exit using compatible method (not WaitForExitAsync)
                    await WaitForExitCompatAsync(process, ct);
                }
            }
            catch (System.Exception ex)
            {
                MainThreadDispatcher.Enqueue(() => 
                    s_LogBuffer?.Add(McpLogLevel.Error, McpLogCategory.Server, 
                        $"Error stopping server: {ex.Message}"));
            }
            finally
            {
                process.Dispose();
            }
        }
        
        public static async Task RestartAsync(McpSettings settings, CancellationToken ct = default)
        {
            await StopAsync(ct);
            await Task.Delay(500, ct);
            await StartAsync(settings, ct);
        }
        
        /// <summary>
        /// Wait for process to exit in a .NET Standard 2.1 compatible way.
        /// </summary>
        private static Task WaitForExitCompatAsync(Process process, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                while (!process.HasExited)
                {
                    ct.ThrowIfCancellationRequested();
                    process.WaitForExit(200);
                }
            }, CancellationToken.None);
        }
    }
}
#endif
