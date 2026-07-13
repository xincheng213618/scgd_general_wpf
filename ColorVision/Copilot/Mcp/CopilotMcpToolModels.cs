using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot.Mcp
{
    public sealed class CopilotMcpToolDescriptor
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        [JsonPropertyName("riskLevel")]
        public string RiskLevel { get; init; } = string.Empty;

        [JsonPropertyName("usageExample")]
        public string UsageExample { get; init; } = string.Empty;

        [JsonPropertyName("annotations")]
        public IReadOnlyDictionary<string, object> Annotations { get; init; } = new Dictionary<string, object>();

        [JsonPropertyName("inputSchema")]
        public object InputSchema { get; init; } = new { type = "object" };
    }

    public sealed class CopilotMcpResourceDescriptor
    {
        [JsonPropertyName("uri")]
        public string Uri { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("mimeType")]
        public string MimeType { get; init; } = "text/plain";
    }

    public sealed class CopilotMcpToolCallResult
    {
        public bool Success { get; init; }

        public string Text { get; init; } = string.Empty;

        public string ErrorCode { get; init; } = string.Empty;

        public static CopilotMcpToolCallResult Ok(string text) => new()
        {
            Success = true,
            Text = text ?? string.Empty,
        };

        public static CopilotMcpToolCallResult Fail(string errorCode, string message) => new()
        {
            Success = false,
            ErrorCode = errorCode ?? string.Empty,
            Text = message ?? string.Empty,
        };
    }

    public sealed class CopilotMcpWorkspaceSnapshot
    {
        public string SolutionDirectoryPath { get; init; } = string.Empty;

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();
    }

    public sealed class CopilotMcpRuntimeSettings
    {
        public bool Enabled { get; init; }

        public string Host { get; init; } = "127.0.0.1";

        public int Port { get; init; } = CopilotConfig.DefaultMcpPort;

        public string BearerToken { get; init; } = string.Empty;

        public string Endpoint => $"http://{Host}:{Port}/mcp";
    }

    public sealed class CopilotTemplatePatchApplyRequest
    {
        public string PreviewId { get; init; } = string.Empty;

        public string TemplateIdentifier { get; init; } = string.Empty;

        public string SourceId { get; init; } = string.Empty;

        public string ExpectedCurrentJson { get; init; } = string.Empty;

        public string PatchedJson { get; init; } = string.Empty;
    }

    public sealed class CopilotMcpHttpRequest
    {
        public string Method { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Body { get; init; } = string.Empty;

        public string CallerSource { get; init; } = string.Empty;
    }

    public sealed class CopilotMcpHttpResponse
    {
        public int StatusCode { get; init; }

        public string ContentType { get; init; } = "application/json; charset=utf-8";

        public string Body { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class CopilotMcpToolEnvironment
    {
        public Func<CopilotMcpWorkspaceSnapshot> WorkspaceSnapshotProvider { get; init; } = CreateDefaultWorkspaceSnapshot;

        public Func<CopilotMcpRuntimeSettings> RuntimeSettingsProvider { get; init; } = CreateDefaultRuntimeSettings;

        public Func<bool> ServerRunningProvider { get; init; } = () => CopilotMcpServer.Instance.IsRunning;

        public Func<string> ServerStatusMessageProvider { get; init; } = () => CopilotMcpServer.Instance.LastStatusMessage;

        public Func<CopilotLiveContext?> LiveContextProvider { get; init; } = () => CopilotLiveContextRegistry.Current;

        public Func<CancellationToken, Task<CopilotFlowContextSnapshot?>> FlowSnapshotProvider { get; init; } = CreateDefaultFlowSnapshotAsync;

        public Func<string?, CopilotRecentLogMode, int, int, CopilotCapabilityResult> RecentLogProvider { get; init; } = CopilotRecentLogCapability.Capture;

        public Func<CopilotTemplatePatchApplyRequest, CancellationToken, Task<CopilotMcpToolCallResult>> ApplyTemplatePatchHandler { get; init; } = ApplyTemplatePatchToActiveEditorAsync;

        public Func<string, CancellationToken, Task<CopilotMcpToolCallResult>> CreateFlowHandler { get; init; } = CreateDefaultFlowAsync;

        public Func<string, CancellationToken, Task<CopilotMcpToolCallResult>>? OpenPanelHandler { get; init; }

        public Func<string, bool, CancellationToken, Task<CopilotMcpToolCallResult>>? ExecuteMenuHandler { get; init; }

        public Func<string, CancellationToken, Task<CopilotMcpToolCallResult>>? SetThemeHandler { get; init; }

        public Func<string, CancellationToken, Task<CopilotMcpToolCallResult>>? SetLanguageHandler { get; init; }

        private static async Task<CopilotMcpToolCallResult> ApplyTemplatePatchToActiveEditorAsync(CopilotTemplatePatchApplyRequest request, CancellationToken cancellationToken)
        {
            var result = await EditTemplateJson.TryApplyCopilotJsonPatchAsync(
                request.SourceId,
                request.ExpectedCurrentJson,
                request.PatchedJson,
                cancellationToken);

            return result.Success
                ? CopilotMcpToolCallResult.Ok(result.Message)
                : CopilotMcpToolCallResult.Fail(result.ErrorCode, result.Message);
        }

        private static async Task<CopilotMcpToolCallResult> CreateDefaultFlowAsync(string flowName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Application.Current == null)
                return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

            try
            {
                return await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = CopilotFlowCreationSupport.ResolveFlowName(null, flowName);
                    var templateFlow = new TemplateFlow();
                    templateFlow.Load();
                    if (templateFlow.ExitsTemplateName(name))
                        return CopilotMcpToolCallResult.Fail("flow_name_exists", $"A flow named {name} already exists.");

                    templateFlow.Create(name);
                    var created = TemplateFlow.Params.LastOrDefault(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase));
                    if (created == null)
                        return CopilotMcpToolCallResult.Fail("flow_creation_failed", $"ColorVision did not create flow {name}.");

                    FlowEngineManager.Current?.View.Refresh();
                    return CopilotMcpToolCallResult.Ok($"Created empty flow {created.Key}; flow_id={created.Id}.");
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return CopilotMcpToolCallResult.Fail("flow_creation_failed", $"Failed to create the flow: {CopilotMcpAuditLogger.RedactText(ex.Message)}");
            }
        }

        private static CopilotMcpRuntimeSettings CreateDefaultRuntimeSettings()
        {
            try
            {
                var config = CopilotConfig.Instance;
                return new CopilotMcpRuntimeSettings
                {
                    Enabled = config.McpEnabled,
                    Host = "127.0.0.1",
                    Port = config.McpPort,
                    BearerToken = config.McpBearerToken,
                };
            }
            catch
            {
                return new CopilotMcpRuntimeSettings();
            }
        }

        private static CopilotMcpWorkspaceSnapshot CreateDefaultWorkspaceSnapshot()
        {
            try
            {
                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                    return Application.Current.Dispatcher.Invoke(CreateDefaultWorkspaceSnapshotOnCurrentThread);

                return CreateDefaultWorkspaceSnapshotOnCurrentThread();
            }
            catch
            {
                return new CopilotMcpWorkspaceSnapshot();
            }
        }

        private static CopilotMcpWorkspaceSnapshot CreateDefaultWorkspaceSnapshotOnCurrentThread()
        {
            var solutionDirectory = SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty;
            var activeDocument = TryGetActiveDocumentPath();
            var roots = new List<string>();

            AddSearchRoot(roots, solutionDirectory);
            AddSearchRoot(roots, activeDocument);

            return new CopilotMcpWorkspaceSnapshot
            {
                SolutionDirectoryPath = solutionDirectory,
                ActiveDocumentPath = activeDocument,
                SearchRootPaths = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots),
            };
        }

        private static string TryGetActiveDocumentPath()
        {
            try
            {
                if (WorkspaceManager.LayoutDocumentPane == null)
                    return string.Empty;

                return WorkspaceManager.FindDocumentActive(WorkspaceManager.LayoutDocumentPane)?.ContentId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AddSearchRoot(List<string> roots, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    roots.Add(fullPath);
                    return;
                }

                if (File.Exists(fullPath))
                {
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        roots.Add(directory);
                }
            }
            catch
            {
            }
        }

        private static async Task<CopilotFlowContextSnapshot?> CreateDefaultFlowSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var manager = FlowEngineManager.Current;
                if (manager == null)
                    return null;

                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    return await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return manager.CaptureCopilotFlowSnapshot();
                    });
                }

                return manager.CaptureCopilotFlowSnapshot();
            }
            catch
            {
                return null;
            }
        }
    }
}
