using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityMCP.Editor.Settings;

namespace UnityMCP.Editor.Services
{
    public class HealthCheckResult
    {
        public bool IsHealthy;
        public string Error;
        public long ResponseMs;
    }
    
    public static class ServerHealthService
    {
        private static readonly HttpClient s_Client = new();
        
        public static async Task<HealthCheckResult> CheckAsync(McpSettings settings, CancellationToken ct = default)
        {
            var result = new HealthCheckResult();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                using var response = await s_Client.GetAsync(settings.HealthUri, ct);
                sw.Stop();
                result.ResponseMs = sw.ElapsedMilliseconds;
                
                if (response.IsSuccessStatusCode)
                {
                    result.IsHealthy = true;
                }
                else
                {
                    result.Error = $"Health check returned {response.StatusCode}";
                }
            }
            catch (TaskCanceledException)
            {
                result.Error = "Health check timed out";
            }
            catch (HttpRequestException ex)
            {
                result.Error = $"Connection failed: {ex.Message}";
            }
            catch (System.Exception ex)
            {
                result.Error = $"Health check error: {ex.Message}";
            }
            
            return result;
        }
    }
}
