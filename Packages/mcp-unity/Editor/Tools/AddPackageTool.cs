using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for adding new packages into the Unity Package Manager
    /// </summary>
    public class AddPackageTool : McpToolBase
    {
        // Class to track each package operation
        private class PackageOperation
        {
            public AddRequest Request { get; set; }
            public TaskCompletionSource<JObject> CompletionSource { get; set; }
        }
        
        // Queue of active package operations
        private readonly List<PackageOperation> _activeOperations = new List<PackageOperation>();
        
        // Flag to track if the update callback is registered
        private bool _updateCallbackRegistered = false;
        
        public AddPackageTool()
        {
            Name = "add_package";
            Description = "Adds a new packages into the Unity Package Manager";
            IsAsync = true; // Package Manager operations are asynchronous
        }
        
        /// <summary>
        /// Execute the AddPackage tool asynchronously
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <param name="tcs">TaskCompletionSource to set the result or exception</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Extract source parameter
            string source = parameters["source"]?.ToObject<string>();
            if (string.IsNullOrEmpty(source))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'source' not provided", 
                    "validation_error"
                ));
                return;
            }
            
            // Create and register the operation
            var operation = new PackageOperation
            {
                CompletionSource = tcs
            };
            
            switch (source.ToLowerInvariant())
            {
                case "registry":
                    operation.Request = AddFromRegistry(parameters, tcs);
                    break;
                case "github":
                    operation.Request = AddFromGitHub(parameters, tcs);
                    break;
                case "disk":
                    operation.Request = AddFromDisk(parameters, tcs);
                    break;
                default:
                    tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                        $"Unknown method '{source}'. Valid methods are: registry, github, disk",
                        "validation_error"
                    ));
                    return;
            }
            
            // If request creation failed, the error has already been set on the tcs
            if (operation.Request == null)
            {
                return;
            }
            
            lock (_activeOperations)
            {
                _activeOperations.Add(operation);
                
                // Register update callback if not already registered
                if (!_updateCallbackRegistered)
                {
                    EditorApplication.update += CheckOperationsCompletion;
                    _updateCallbackRegistered = true;
                }
            }
        }
        
        /// <summary>
        /// Add a package from the Unity registry
        /// </summary>
        private AddRequest AddFromRegistry(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Extract parameters
            string packageName = parameters["packageName"]?.ToObject<string>();
            if (string.IsNullOrEmpty(packageName))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'packageName' not provided for registry method", 
                    "validation_error"
                ));
                return null;
            }
            
            string version = parameters["version"]?.ToObject<string>();
            string packageIdentifier = packageName;
            
            // Add version if specified
            if (!string.IsNullOrEmpty(version))
            {
                packageIdentifier = $"{packageName}@{version}";
            }
            
            McpLogger.LogInfo($"Adding package from registry: {packageIdentifier}");
            
            try
            {
                // Add the package
                return Client.Add(packageIdentifier);
            }
            catch (Exception ex)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Exception adding package: {ex.Message}",
                    "package_manager_error"
                ));
                return null;
            }
        }
        
        /// <summary>
        /// Add a package from GitHub
        /// </summary>
        private AddRequest AddFromGitHub(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Extract parameters
            string packageUrl = parameters["repositoryUrl"]?.ToObject<string>();
            
            if (string.IsNullOrEmpty(packageUrl))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'repositoryUrl' not provided for github method", 
                    "validation_error"
                ));
                return null;
            }
            
            string branch = parameters["branch"]?.ToObject<string>();
            string path = parameters["path"]?.ToObject<string>();
            
            // Remove any .git suffix if present
            if (packageUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                packageUrl = packageUrl.Substring(0, packageUrl.Length - 4);
            }
            
            // Add branch if specified
            if (!string.IsNullOrEmpty(branch))
            {
                packageUrl += "#" + branch;
            }
            
            // Add path if specified
            if (!string.IsNullOrEmpty(path))
            {
                if (!string.IsNullOrEmpty(branch))
                {
                    // Branch is already added, append path with slash
                    packageUrl += "/" + path;
                }
                else
                {
                    // No branch, use hash followed by path
                    packageUrl += "#" + path;
                }
            }
            
            McpLogger.LogInfo($"Adding package from GitHub: {packageUrl}");
            
            try
            {
                // Add the package
                return Client.Add(packageUrl);
            }
            catch (Exception ex)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Exception adding package: {ex.Message}",
                    "package_manager_error"
                ));
                return null;
            }
        }
        
        /// <summary>
        /// Add a package from disk
        /// </summary>
        private AddRequest AddFromDisk(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Extract parameters
            string path = parameters["path"]?.ToObject<string>();
            
            if (string.IsNullOrEmpty(path))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'path' not provided for disk method", 
                    "validation_error"
                ));
                return null;
            }
            
            // Format as file URL with proper encoding for paths containing spaces
            string encodedPath = McpUtils.EncodePathForFileUrl(path);
            string packageUrl = $"file:{encodedPath}";
            
            McpLogger.LogInfo($"Adding package from disk: {packageUrl}");
            
            try
            {
                // Add the package
                return Client.Add(packageUrl);
            }
            catch (Exception ex)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Exception adding package: {ex.Message}",
                    "package_manager_error"
                ));
                return null;
            }
        }
        
        /// <summary>
        /// Check all active operations for completion
        /// </summary>
        private void CheckOperationsCompletion()
        {
            // Store initial count
            int initialCount = _activeOperations.Count;
            
            lock (_activeOperations)
            {
                // Process operations in reverse order to safely remove completed ones
                for (int i = _activeOperations.Count - 1; i >= 0; i--)
                {
                    var operation = _activeOperations[i];
                    
                    if (operation.Request != null && operation.Request.IsCompleted)
                    {
                        // Process the completed operation
                        ProcessCompletedOperation(operation);
                        
                        // Remove it from the active operations list
                        _activeOperations.RemoveAt(i);
                    }
                }
                
                // If all operations are completed, unregister the update callback
                if (_activeOperations.Count == 0 && _updateCallbackRegistered)
                {
                    EditorApplication.update -= CheckOperationsCompletion;
                    _updateCallbackRegistered = false;
                }
            }
            
            // If any operations completed, force a GC collection to clean up UPM request objects
            if (initialCount != _activeOperations.Count)
            {
                GC.Collect();
            }
        }
        
        /// <summary>
        /// Process a completed package operation
        /// </summary>
        private void ProcessCompletedOperation(PackageOperation operation)
        {
            if (operation.CompletionSource == null)
            {
                McpLogger.LogError("TaskCompletionSource is null when processing completed operation");
                return;
            }
            
            // Check request status
            if (operation.Request.Status == StatusCode.Success)
            {
                var result = operation.Request.Result;
                if (result != null)
                {
                    operation.CompletionSource.SetResult(new JObject
                    {
                        ["success"] = true,
                        ["type"] = "text",
                        ["message"] = $"Successfully added package: {result.displayName} ({result.name}) version {result.version}",
                        ["packageInfo"] = JObject.FromObject(new
                        {
                            name = result.name,
                            displayName = result.displayName,
                            version = result.version
                        })
                    });
                }
                else
                {
                    operation.CompletionSource.SetResult(new JObject
                    {
                        ["success"] = true,
                        ["type"] = "text",
                        ["message"] = $"Package operation completed successfully, but no package information was returned."
                    });
                }
                
                McpLogger.LogInfo($"Added package {result.displayName} ({result.name}) version {result.version}");
            }
            else if (operation.Request.Status == StatusCode.Failure)
            {
                operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to add package: {operation.Request.Error.message}",
                    "package_manager_error"
                ));
            }
            else
            {
                operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Unknown package manager status: {operation.Request.Status}",
                    "package_manager_error"
                ));
            }
        }
    }

    public static class PackageManagerToolHelpers
    {
        private static readonly object OperationLock = new object();
        private static bool _operationInProgress;

        public static JObject PackageToJObject(UnityEditor.PackageManager.PackageInfo package, string state = null, int? searchPriority = null)
        {
            if (package == null)
            {
                return new JObject();
            }

            string resolvedState = string.IsNullOrEmpty(state) ? InferStateFromSource(package.source) : state;
            bool isInstalled = string.Equals(resolvedState, "installed", StringComparison.OrdinalIgnoreCase);

            return new JObject
            {
                ["name"] = package.name,
                ["displayName"] = package.displayName,
                ["version"] = package.version,
                ["description"] = package.description,
                ["category"] = package.category,
                ["source"] = package.source.ToString(),
                ["state"] = resolvedState,
                ["installed"] = isInstalled,
                ["isInstalled"] = isInstalled,
                ["searchPriority"] = searchPriority,
                ["author"] = new JObject
                {
                    ["name"] = package.author?.name,
                    ["email"] = package.author?.email,
                    ["url"] = package.author?.url
                }
            };
        }

        public static bool TryParseSourceFilter(string sourceFilter, out HashSet<string> parsed, out string error)
        {
            parsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            error = null;

            if (string.IsNullOrWhiteSpace(sourceFilter))
            {
                return true;
            }

            string[] parts = sourceFilter.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string normalized = NormalizeSourceName(part);
                if (normalized == "all" || normalized == "any")
                {
                    continue;
                }

                if (!IsKnownSourceName(normalized))
                {
                    error = $"Unknown source filter '{part}'. Valid values are: all, registry, built_in, builtin, embedded, local, git, cache";
                    return false;
                }

                parsed.Add(normalized);
            }

            return true;
        }

        public static bool MatchesSourceFilter(UnityEditor.PackageManager.PackageInfo package, ISet<string> sourceFilter)
        {
            if (package == null || sourceFilter == null || sourceFilter.Count == 0)
            {
                return true;
            }

            string packageSource = NormalizeSourceName(package.source.ToString());
            return sourceFilter.Contains(packageSource);
        }

        public static int GetSearchPriority(UnityEditor.PackageManager.PackageInfo package, string query)
        {
            if (package == null)
            {
                return int.MaxValue;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return 0;
            }

            string normalizedQuery = query.Trim().ToLowerInvariant();
            string name = (package.name ?? string.Empty).Trim().ToLowerInvariant();
            string displayName = (package.displayName ?? string.Empty).Trim().ToLowerInvariant();
            string description = (package.description ?? string.Empty).Trim().ToLowerInvariant();

            if (name == normalizedQuery) return 0;
            if (displayName == normalizedQuery) return 1;
            if (name.StartsWith(normalizedQuery, StringComparison.Ordinal)) return 2;
            if (displayName.StartsWith(normalizedQuery, StringComparison.Ordinal)) return 3;
            if (name.Contains(normalizedQuery)) return 4;
            if (displayName.Contains(normalizedQuery)) return 5;
            if (description.Contains(normalizedQuery)) return 6;
            return 100 + GetSourcePriority(package.source);
        }

        public static bool TryBeginPackageManagerOperation(out string error)
        {
            lock (OperationLock)
            {
                if (_operationInProgress)
                {
                    error = "Another Unity Package Manager operation is already in progress. Try again after it completes.";
                    return false;
                }

                _operationInProgress = true;
                error = null;
                return true;
            }
        }

        public static void EndPackageManagerOperation()
        {
            lock (OperationLock)
            {
                _operationInProgress = false;
            }
        }

        private static string NormalizeSourceName(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName)) return string.Empty;
            string normalized = sourceName.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            switch (normalized)
            {
                case "builtin":
                case "builtins":
                case "built_in": return "built_in";
                case "registry": return "registry";
                case "embedded": return "embedded";
                case "local": return "local";
                case "git":
                case "git_url": return "git";
                case "cache": return "cache";
                case "all": return "all";
                case "any": return "any";
                default: return normalized;
            }
        }

        private static bool IsKnownSourceName(string sourceName)
        {
            return sourceName == "registry" || sourceName == "built_in" || sourceName == "embedded" || sourceName == "local" || sourceName == "git" || sourceName == "cache";
        }

        private static string InferStateFromSource(PackageSource source)
        {
            return source == PackageSource.Embedded || source == PackageSource.Local || source == PackageSource.BuiltIn ? "installed" : "available";
        }

        private static int GetSourcePriority(PackageSource source)
        {
            switch (source)
            {
                case PackageSource.Embedded: return 0;
                case PackageSource.Local: return 1;
                case PackageSource.BuiltIn: return 2;
                case PackageSource.Registry: return 3;
                case PackageSource.Git: return 4;
                default: return 5;
            }
        }
    }

    public class PackageListTool : McpToolBase
    {
        private class PackageOperation
        {
            public ListRequest Request { get; set; }
            public TaskCompletionSource<JObject> CompletionSource { get; set; }
            public HashSet<string> SourceFilter { get; set; }
        }

        private readonly List<PackageOperation> _activeOperations = new List<PackageOperation>();
        private bool _updateCallbackRegistered;

        public PackageListTool()
        {
            Name = "package_list";
            Description = "Lists packages in the Unity Package Manager";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            bool includeIndirect = parameters?["includeIndirect"]?.ToObject<bool?>() ?? true;
            bool offlineMode = parameters?["offlineMode"]?.ToObject<bool?>() ?? false;
            string sourceFilterText = parameters?["source"]?.ToObject<string>() ?? parameters?["sourceFilter"]?.ToObject<string>();

            if (!PackageManagerToolHelpers.TryParseSourceFilter(sourceFilterText, out HashSet<string> sourceFilter, out string sourceError))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(sourceError, "validation_error"));
                return;
            }

            if (!PackageManagerToolHelpers.TryBeginPackageManagerOperation(out string operationError))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(operationError, "package_manager_busy"));
                return;
            }

            var operation = new PackageOperation { CompletionSource = tcs, SourceFilter = sourceFilter };

            try
            {
                operation.Request = Client.List(offlineMode, includeIndirect);
            }
            catch (Exception ex)
            {
                PackageManagerToolHelpers.EndPackageManagerOperation();
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Exception listing packages: {ex.Message}", "package_manager_error"));
                return;
            }

            lock (_activeOperations)
            {
                _activeOperations.Add(operation);
                if (!_updateCallbackRegistered)
                {
                    EditorApplication.update += CheckOperationsCompletion;
                    _updateCallbackRegistered = true;
                }
            }
        }

        private void CheckOperationsCompletion()
        {
            lock (_activeOperations)
            {
                for (int i = _activeOperations.Count - 1; i >= 0; i--)
                {
                    var operation = _activeOperations[i];
                    if (operation.Request == null || !operation.Request.IsCompleted) continue;

                    try { ProcessCompletedOperation(operation); }
                    catch (Exception ex) { operation.CompletionSource?.TrySetResult(McpUnitySocketHandler.CreateErrorResponse($"Exception processing package list result: {ex.Message}", "package_manager_error")); }
                    finally { PackageManagerToolHelpers.EndPackageManagerOperation(); }

                    _activeOperations.RemoveAt(i);
                }

                if (_activeOperations.Count == 0 && _updateCallbackRegistered)
                {
                    EditorApplication.update -= CheckOperationsCompletion;
                    _updateCallbackRegistered = false;
                }
            }
        }

        private void ProcessCompletedOperation(PackageOperation operation)
        {
            if (operation.Request.Status == StatusCode.Success)
            {
                var packages = new JArray();
                foreach (var package in operation.Request.Result)
                {
                    if (PackageManagerToolHelpers.MatchesSourceFilter(package, operation.SourceFilter))
                    {
                        packages.Add(PackageManagerToolHelpers.PackageToJObject(package, "installed"));
                    }
                }

                operation.CompletionSource.SetResult(new JObject { ["success"] = true, ["type"] = "text", ["message"] = $"Successfully listed {packages.Count} package(s)", ["count"] = packages.Count, ["packages"] = packages });
            }
            else if (operation.Request.Status == StatusCode.Failure)
            {
                operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Failed to list packages: {operation.Request.Error.message}", "package_manager_error"));
            }
            else
            {
                operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Unknown package manager status: {operation.Request.Status}", "package_manager_error"));
            }
        }
    }

    public class PackageRemoveTool : McpToolBase
    {
        private class PackageOperation
        {
            public RemoveRequest Request { get; set; }
            public TaskCompletionSource<JObject> CompletionSource { get; set; }
            public string PackageName { get; set; }
        }

        private readonly List<PackageOperation> _activeOperations = new List<PackageOperation>();
        private bool _updateCallbackRegistered;

        public PackageRemoveTool()
        {
            Name = "package_remove";
            Description = "Removes a package from the Unity Package Manager";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            string packageName = parameters?["packageName"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(packageName))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse("Required parameter 'packageName' not provided", "validation_error"));
                return;
            }

            if (!PackageManagerToolHelpers.TryBeginPackageManagerOperation(out string operationError))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(operationError, "package_manager_busy"));
                return;
            }

            var operation = new PackageOperation { CompletionSource = tcs, PackageName = packageName.Trim() };

            try { operation.Request = Client.Remove(operation.PackageName); }
            catch (Exception ex)
            {
                PackageManagerToolHelpers.EndPackageManagerOperation();
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Exception removing package: {ex.Message}", "package_manager_error"));
                return;
            }

            lock (_activeOperations)
            {
                _activeOperations.Add(operation);
                if (!_updateCallbackRegistered)
                {
                    EditorApplication.update += CheckOperationsCompletion;
                    _updateCallbackRegistered = true;
                }
            }
        }

        private void CheckOperationsCompletion()
        {
            lock (_activeOperations)
            {
                for (int i = _activeOperations.Count - 1; i >= 0; i--)
                {
                    var operation = _activeOperations[i];
                    if (operation.Request == null || !operation.Request.IsCompleted) continue;

                    try { ProcessCompletedOperation(operation); }
                    catch (Exception ex) { operation.CompletionSource?.TrySetResult(McpUnitySocketHandler.CreateErrorResponse($"Exception processing package remove result: {ex.Message}", "package_manager_error")); }
                    finally { PackageManagerToolHelpers.EndPackageManagerOperation(); }

                    _activeOperations.RemoveAt(i);
                }

                if (_activeOperations.Count == 0 && _updateCallbackRegistered)
                {
                    EditorApplication.update -= CheckOperationsCompletion;
                    _updateCallbackRegistered = false;
                }
            }
        }

        private void ProcessCompletedOperation(PackageOperation operation)
        {
            if (operation.Request.Status == StatusCode.Success)
            {
                operation.CompletionSource.SetResult(new JObject { ["success"] = true, ["type"] = "text", ["message"] = $"Successfully removed package: {operation.PackageName}", ["packageName"] = operation.PackageName });
            }
            else if (operation.Request.Status == StatusCode.Failure)
            {
                operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Failed to remove package '{operation.PackageName}': {operation.Request.Error.message}", "package_manager_error"));
            }
            else
            {
                operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Unknown package manager status: {operation.Request.Status}", "package_manager_error"));
            }
        }
    }

    public class PackageSearchTool : McpToolBase
    {
        private class PackageOperation
        {
            public SearchRequest SearchRequest { get; set; }
            public ListRequest ListRequest { get; set; }
            public TaskCompletionSource<JObject> CompletionSource { get; set; }
            public string Query { get; set; }
            public bool SearchAll { get; set; }
            public int Limit { get; set; }
            public HashSet<string> SourceFilter { get; set; }
            public bool IncludeInstalledState { get; set; }
            public bool ListStarted { get; set; }
            public bool SearchStarted { get; set; }
        }

        private readonly List<PackageOperation> _activeOperations = new List<PackageOperation>();
        private bool _updateCallbackRegistered;

        public PackageSearchTool()
        {
            Name = "package_search";
            Description = "Searches for packages in the Unity Package Manager";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            string query = parameters?["query"]?.ToObject<string>();
            bool searchAll = parameters?["searchAll"]?.ToObject<bool?>() ?? false;
            int limit = parameters?["limit"]?.ToObject<int?>() ?? 50;
            string sourceFilterText = parameters?["source"]?.ToObject<string>() ?? parameters?["sourceFilter"]?.ToObject<string>();
            bool includeInstalledState = parameters?["includeInstalledState"]?.ToObject<bool?>() ?? true;

            if (!PackageManagerToolHelpers.TryParseSourceFilter(sourceFilterText, out HashSet<string> sourceFilter, out string sourceError))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(sourceError, "validation_error"));
                return;
            }

            if (limit < 1 || limit > 250)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse("Parameter 'limit' must be between 1 and 250", "validation_error"));
                return;
            }

            if (!searchAll && string.IsNullOrWhiteSpace(query))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse("Required parameter 'query' not provided when 'searchAll' is false", "validation_error"));
                return;
            }

            if (!PackageManagerToolHelpers.TryBeginPackageManagerOperation(out string operationError))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(operationError, "package_manager_busy"));
                return;
            }

            var operation = new PackageOperation { CompletionSource = tcs, Query = query?.Trim(), SearchAll = searchAll, Limit = limit, SourceFilter = sourceFilter, IncludeInstalledState = includeInstalledState };

            try
            {
                if (includeInstalledState)
                {
                    operation.ListRequest = Client.List(false, true);
                    operation.ListStarted = true;
                }
                else
                {
                    operation.SearchRequest = searchAll ? Client.SearchAll() : Client.Search(operation.Query);
                    operation.SearchStarted = true;
                }
            }
            catch (Exception ex)
            {
                PackageManagerToolHelpers.EndPackageManagerOperation();
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Exception searching packages: {ex.Message}", "package_manager_error"));
                return;
            }

            lock (_activeOperations)
            {
                _activeOperations.Add(operation);
                if (!_updateCallbackRegistered)
                {
                    EditorApplication.update += CheckOperationsCompletion;
                    _updateCallbackRegistered = true;
                }
            }
        }

        private void CheckOperationsCompletion()
        {
            lock (_activeOperations)
            {
                for (int i = _activeOperations.Count - 1; i >= 0; i--)
                {
                    var operation = _activeOperations[i];
                    bool hasCompletedRequest = (operation.ListRequest != null && operation.ListRequest.IsCompleted) || (operation.SearchRequest != null && operation.SearchRequest.IsCompleted);
                    if (!hasCompletedRequest) continue;

                    bool completed;
                    try { completed = ProcessCompletedOperation(operation); }
                    catch (Exception ex)
                    {
                        operation.CompletionSource?.TrySetResult(McpUnitySocketHandler.CreateErrorResponse($"Exception processing package search result: {ex.Message}", "package_manager_error"));
                        completed = true;
                    }

                    if (completed)
                    {
                        PackageManagerToolHelpers.EndPackageManagerOperation();
                        _activeOperations.RemoveAt(i);
                    }
                }

                if (_activeOperations.Count == 0 && _updateCallbackRegistered)
                {
                    EditorApplication.update -= CheckOperationsCompletion;
                    _updateCallbackRegistered = false;
                }
            }
        }

        private bool ProcessCompletedOperation(PackageOperation operation)
        {
            if (operation.IncludeInstalledState && operation.ListStarted && !operation.SearchStarted)
            {
                if (!operation.ListRequest.IsCompleted) return false;
                if (operation.ListRequest.Status != StatusCode.Success)
                {
                    operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Failed to list installed packages for search state: {operation.ListRequest.Error.message}", "package_manager_error"));
                    return true;
                }

                try
                {
                    operation.SearchRequest = operation.SearchAll ? Client.SearchAll() : Client.Search(operation.Query);
                    operation.SearchStarted = true;
                }
                catch (Exception ex)
                {
                    operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Exception searching packages: {ex.Message}", "package_manager_error"));
                    return true;
                }

                return false;
            }

            if (operation.SearchRequest == null || !operation.SearchRequest.IsCompleted) return false;
            if (operation.SearchRequest.Status != StatusCode.Success)
            {
                operation.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse($"Failed to search packages: {operation.SearchRequest.Error.message}", "package_manager_error"));
                return true;
            }

            var installedState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (operation.ListRequest != null && operation.ListRequest.Status == StatusCode.Success)
            {
                foreach (var package in operation.ListRequest.Result) installedState[package.name] = "installed";
            }

            var results = new List<JObject>();
            foreach (var package in operation.SearchRequest.Result)
            {
                if (!PackageManagerToolHelpers.MatchesSourceFilter(package, operation.SourceFilter)) continue;
                string state = operation.IncludeInstalledState ? (installedState.ContainsKey(package.name) ? "installed" : "not_installed") : "unknown";
                int priority = PackageManagerToolHelpers.GetSearchPriority(package, operation.Query);
                results.Add(PackageManagerToolHelpers.PackageToJObject(package, state, priority));
            }

            var finalResults = new JArray();
            foreach (var result in results
                .OrderBy(result => result["searchPriority"]?.ToObject<int?>() ?? int.MaxValue)
                .ThenBy(result => result["displayName"]?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result["name"]?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Take(operation.Limit))
            {
                finalResults.Add(result);
            }

            operation.CompletionSource.SetResult(new JObject { ["success"] = true, ["type"] = "text", ["message"] = $"Found {finalResults.Count} package(s) matching search criteria", ["query"] = operation.SearchAll ? null : operation.Query, ["count"] = finalResults.Count, ["results"] = finalResults });
            return true;
        }
    }
}
