using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityMCP.Editor.Settings;
using UnityMCP.Editor.Logging;

namespace UnityMCP.Editor.Services
{
    public class SetupResult
    {
        public bool Success;
        public string Error;
        public List<string> Steps = new();
    }
    
    public static class UnityMcpSetupService
    {
        public static async Task<SetupResult> RunOneClickSetupAsync(
            McpSettings settings,
            McpLogBuffer logBuffer,
            Action<float, string> progressCallback,
            CancellationToken ct = default)
        {
            var result = new SetupResult();
            
            void LogStep(string step)
            {
                result.Steps.Add(step);
                logBuffer?.Add(McpLogLevel.Info, McpLogCategory.Setup, step);
            }
            
            try
            {
                // Step 1: Validate Settings
                progressCallback?.Invoke(0.05f, "Validating settings...");
                LogStep("Validating settings...");
                
                var validation = McpSettingsValidator.Validate(settings);
                if (!validation.IsValid)
                {
                    result.Error = string.Join("\n", validation.Errors);
                    logBuffer?.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
                    return result;
                }
                
                foreach (var warning in validation.Warnings)
                    logBuffer?.Add(McpLogLevel.Warning, McpLogCategory.Setup, warning);
                
                // Step 2: Check Go
                progressCallback?.Invoke(0.15f, "Checking Go installation...");
                LogStep("Checking Go installation...");
                
                var goCheck = await GoInstallationService.CheckGoAsync(ct);
                if (!goCheck.IsInstalled)
                {
                    result.Error = $"Go is not installed: {goCheck.Error}\nPlease install Go 1.23+ from https://go.dev/dl/";
                    logBuffer?.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
                    return result;
                }
                
                LogStep($"Go found: {goCheck.Version}");
                
                // Step 3: Build Server
                progressCallback?.Invoke(0.35f, "Building Go server...");
                LogStep("Building Go server...");
                
                var buildResult = await ServerBuildService.BuildAsync(settings,
                    new Progress<string>(msg => logBuffer?.Add(McpLogLevel.Info, McpLogCategory.Setup, msg)), ct);
                
                if (!buildResult.Success)
                {
                    result.Error = $"Build failed: {buildResult.Error}";
                    logBuffer?.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
                    return result;
                }
                
                LogStep($"Server built successfully: {buildResult.OutputPath}");
                
                // Step 4: Export opencode.json
                progressCallback?.Invoke(0.55f, "Exporting opencode.json...");
                LogStep("Exporting opencode.json...");
                
                var exportResult = await OpencodeConfigService.ExportAsync(settings, ct);
                if (!exportResult.Success)
                {
                    result.Error = $"Config export failed: {exportResult.Error}";
                    logBuffer?.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
                    return result;
                }
                
                LogStep($"Config exported to: {exportResult.Path}");
                
                // Step 4b: Configure OpenCode global config (MCP+)
                progressCallback?.Invoke(0.60f, "Configuring OpenCode MCP+...");
                LogStep("Configuring OpenCode global config (MCP+)...");
                
                var globalConfigResult = await OpencodeConfigService.ConfigureGlobalAsync(settings, ct);
                if (!globalConfigResult.Success)
                {
                    // Non-fatal: log warning but continue
                    LogStep($"Warning: OpenCode global config failed: {globalConfigResult.Error}");
                    logBuffer?.Add(McpLogLevel.Warning, McpLogCategory.Setup, $"OpenCode global config skipped: {globalConfigResult.Error}");
                }
                else
                {
                    LogStep($"OpenCode global config updated: {globalConfigResult.Path}");
                }
                
                // Step 5: Start Server
                progressCallback?.Invoke(0.72f, "Starting server...");
                LogStep("Starting server...");
                
                ServerProcessService.SetLogBuffer(logBuffer);
                var startResult = await ServerProcessService.StartAsync(settings, ct);
                
                if (!startResult.Success)
                {
                    result.Error = $"Server start failed: {startResult.Error}";
                    logBuffer?.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
                    return result;
                }
                
                LogStep($"Server started with PID {startResult.ProcessId}");
                
                // Step 6: Health Check
                progressCallback?.Invoke(0.82f, "Checking server health...");
                LogStep("Checking server health...");
                
                var healthResult = await ServerHealthService.CheckAsync(settings, ct);
                if (!healthResult.IsHealthy)
                {
                    result.Error = $"Health check failed: {healthResult.Error}";
                    logBuffer?.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
                    return result;
                }
                
                LogStep($"Server healthy (response: {healthResult.ResponseMs}ms)");
                
                // Step 7: Connect WebSocket (handled by UnityMcpPlugin)
                progressCallback?.Invoke(0.92f, "Connecting Unity WebSocket...");
                LogStep("Unity WebSocket will connect automatically...");
                
                // Done
                progressCallback?.Invoke(1.0f, "Setup complete!");
                LogStep("One-click setup completed successfully!");
                
                result.Success = true;
            }
            catch (OperationCanceledException)
            {
                result.Error = "Setup was cancelled";
                LogStep("Setup cancelled by user");
            }
            catch (Exception ex)
            {
                result.Error = $"Unexpected error: {ex.Message}";
                logBuffer?.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
            }
            
            return result;
        }
    }
}
