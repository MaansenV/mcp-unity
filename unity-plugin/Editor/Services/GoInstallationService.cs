using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UnityMCP.Editor.Services
{
    public class GoCheckResult
    {
        public bool IsInstalled;
        public string Version;
        public string ExecutablePath;
        public string Error;
    }
    
    public static class GoInstallationService
    {
        public static string ResolveGoExecutable()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Go", "bin", "go.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Go", "bin", "go.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "go", "bin", "go.exe"),
                "go"
            };

            return candidates.FirstOrDefault(path => string.Equals(path, "go", StringComparison.OrdinalIgnoreCase) || File.Exists(path)) ?? "go";
        }

        public static async Task<GoCheckResult> CheckGoAsync(CancellationToken ct = default)
        {
            var result = new GoCheckResult();
            var goExecutable = ResolveGoExecutable();
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = goExecutable,
                    Arguments = "version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null)
                {
                    result.Error = "Failed to start go process";
                    return result;
                }
                
                var outputTask = process.StandardOutput.ReadToEndAsync();
                await Task.Run(() =>
                {
                    while (!process.WaitForExit(100))
                    {
                        ct.ThrowIfCancellationRequested();
                    }
                }, ct);
                var output = await outputTask;
                
                // Parse "go version go1.23.4 windows/amd64"
                var parts = output.Split(' ');
                if (parts.Length >= 3 && parts[0] == "go")
                {
                    result.IsInstalled = true;
                    result.Version = parts[2];
                    result.ExecutablePath = goExecutable;
                }
                else
                {
                    result.Error = $"Unexpected go version output: {output}";
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                result.Error = "Go is not installed or not in PATH. Checked PATH and common install locations.";
            }
            catch (TaskCanceledException)
            {
                result.Error = "Go version check timed out";
            }
            catch (System.Exception ex)
            {
                result.Error = $"Error checking Go: {ex.Message}";
            }
            
            return result;
        }
    }
}
