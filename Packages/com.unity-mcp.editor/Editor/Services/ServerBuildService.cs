using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityMCP.Editor.Settings;

namespace UnityMCP.Editor.Services
{
    public class ServerBuildResult
    {
        public bool Success;
        public string OutputPath;
        public string Error;
        public string BuildOutput;
    }
    
    public static class ServerBuildService
    {
        public static async Task<ServerBuildResult> BuildAsync(McpSettings settings, IProgress<string> progress, CancellationToken ct = default)
        {
            var result = new ServerBuildResult();
            
            // Validate paths
            if (string.IsNullOrEmpty(settings.ServerRootPath) || !Directory.Exists(settings.ServerRootPath))
            {
                result.Error = $"Server root path does not exist: {settings.ServerRootPath}";
                return result;
            }
            
            var goModPath = Path.Combine(settings.ServerRootPath, "go.mod");
            if (!File.Exists(goModPath))
            {
                result.Error = $"go.mod not found at: {goModPath}";
                return result;
            }
            
            var mainGoPath = Path.Combine(settings.ServerRootPath, "cmd", "unity-mcp", "main.go");
            if (!File.Exists(mainGoPath))
            {
                result.Error = $"main.go not found at: {mainGoPath}";
                return result;
            }
            
            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(settings.ServerBuildOutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            
            progress?.Report("Building Go server...");
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = GoInstallationService.ResolveGoExecutable(),
                    Arguments = $"build -o \"{settings.ServerBuildOutputPath}\" ./cmd/unity-mcp",
                    WorkingDirectory = settings.ServerRootPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null)
                {
                    result.Error = "Failed to start build process";
                    return result;
                }
                
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await Task.Run(() =>
                {
                    while (!process.WaitForExit(100))
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                }, ct);

                var output = await outputTask;
                var error = await errorTask;
                
                result.BuildOutput = output;
                
                if (process.ExitCode == 0)
                {
                    result.Success = true;
                    result.OutputPath = settings.ServerBuildOutputPath;
                    progress?.Report($"Build succeeded: {settings.ServerBuildOutputPath}");
                }
                else
                {
                    result.Error = $"Build failed (exit code {process.ExitCode}):\n{error}\n{output}";
                    progress?.Report("Build failed");
                }
            }
            catch (System.Exception ex)
            {
                result.Error = $"Build error: {ex.Message}";
            }
            
            return result;
        }
    }
}
