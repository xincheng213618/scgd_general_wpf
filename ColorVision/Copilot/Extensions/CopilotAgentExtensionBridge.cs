using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentExtensionIssue
    {
        public string SourceId { get; init; } = string.Empty;

        public string CapabilityName { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }

    public sealed class CopilotAgentExtensionBridgeSnapshot
    {
        public long Revision { get; init; }

        public IReadOnlyList<CopilotAgentExtensionSourceSnapshot> Sources { get; init; } = Array.Empty<CopilotAgentExtensionSourceSnapshot>();

        public IReadOnlyList<ICopilotContextProvider> ContextProviders { get; init; } = Array.Empty<ICopilotContextProvider>();

        public IReadOnlyList<ICopilotTool> Tools { get; init; } = Array.Empty<ICopilotTool>();

        public IReadOnlyList<CopilotAgentExtensionIssue> Issues { get; init; } = Array.Empty<CopilotAgentExtensionIssue>();
    }

    public sealed class CopilotAgentExtensionSourceSnapshot
    {
        public string SourceId { get; init; } = string.Empty;

        public string SourceName { get; init; } = string.Empty;

        public string SourceVersion { get; init; } = string.Empty;

        public int ContextProviderCount { get; init; }

        public int DeclaredToolCount { get; init; }

        public int ActiveToolCount { get; init; }
    }

    /// <summary>
    /// Adapts the dependency-light UI module extension contract to the full Copilot Agent
    /// runtime and keeps the capability checkpoint catalog synchronized with module lifetime.
    /// </summary>
    public sealed class CopilotAgentExtensionBridge : IDisposable
    {
        private static readonly Lazy<CopilotAgentExtensionBridge> SharedBridge = new(
            () => new CopilotAgentExtensionBridge(
                CopilotAgentExtensionRegistry.Shared,
                CopilotCapabilityCatalog.Shared,
                CopilotToolRegistry.CreateCoreDefaultTools().Select(tool => tool.Name)),
            LazyThreadSafetyMode.ExecutionAndPublication);
        private readonly CopilotAgentExtensionRegistry _registry;
        private readonly CopilotCapabilityCatalog _capabilityCatalog;
        private readonly HashSet<string> _reservedToolNames;
        private readonly HashSet<string> _publishedCatalogSourceIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncRoot = new();
        private CopilotAgentExtensionBridgeSnapshot _snapshot = new();
        private bool _disposed;

        public CopilotAgentExtensionBridge(
            CopilotAgentExtensionRegistry registry,
            CopilotCapabilityCatalog capabilityCatalog,
            IEnumerable<string>? reservedToolNames = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _capabilityCatalog = capabilityCatalog ?? throw new ArgumentNullException(nameof(capabilityCatalog));
            _reservedToolNames = new HashSet<string>(
                (reservedToolNames ?? Array.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()),
                StringComparer.OrdinalIgnoreCase);
            _registry.Changed += Registry_Changed;
            Refresh(_registry.GetSnapshot());
        }

        public static CopilotAgentExtensionBridge Shared => SharedBridge.Value;

        public CopilotAgentExtensionBridgeSnapshot GetSnapshot()
        {
            lock (_syncRoot)
                return _snapshot;
        }

        public void Dispose()
        {
            string[] sourceIds;
            lock (_syncRoot)
            {
                if (_disposed)
                    return;
                _disposed = true;
                sourceIds = _publishedCatalogSourceIds.ToArray();
                _publishedCatalogSourceIds.Clear();
                _snapshot = new CopilotAgentExtensionBridgeSnapshot { Revision = _snapshot.Revision };
            }

            _registry.Changed -= Registry_Changed;
            foreach (var sourceId in sourceIds)
                _capabilityCatalog.PublishSource(CopilotCapabilitySourceKind.Plugin, sourceId, sourceId, Array.Empty<ICopilotTool>());
        }

        private void Registry_Changed(object? sender, CopilotAgentExtensionRegistryChangedEventArgs e)
        {
            Refresh(_registry.GetSnapshot());
        }

        private void Refresh(CopilotAgentExtensionRegistrySnapshot registrySnapshot)
        {
            lock (_syncRoot)
            {
                if (_disposed || registrySnapshot.Revision < _snapshot.Revision)
                    return;

                var contextProviders = registrySnapshot.Extensions
                    .SelectMany(extension => extension.ContextProviders.Select(provider =>
                        (ICopilotContextProvider)new CopilotModuleContextProviderAdapter(
                            provider,
                            () => _registry.IsRegistered(extension))))
                    .OrderBy(provider => provider.Order)
                    .ToArray();
                var issues = new List<CopilotAgentExtensionIssue>();
                var toolsBySource = new Dictionary<string, List<ICopilotTool>>(StringComparer.OrdinalIgnoreCase);
                var activeToolNames = new HashSet<string>(_reservedToolNames, StringComparer.OrdinalIgnoreCase);
                foreach (var extension in registrySnapshot.Extensions)
                {
                    foreach (var moduleTool in extension.Tools)
                    {
                        var toolName = moduleTool.Name.Trim();
                        if (!activeToolNames.Add(toolName))
                        {
                            issues.Add(new CopilotAgentExtensionIssue
                            {
                                SourceId = extension.SourceId,
                                CapabilityName = toolName,
                                Message = $"Module tool '{toolName}' conflicts with a reserved or already active Agent tool name and was not activated.",
                            });
                            continue;
                        }

                        var catalogSourceId = BuildCatalogSourceId(extension.SourceId);
                        if (!toolsBySource.TryGetValue(catalogSourceId, out var sourceTools))
                        {
                            sourceTools = new List<ICopilotTool>();
                            toolsBySource.Add(catalogSourceId, sourceTools);
                        }
                        sourceTools.Add(new CopilotModuleToolAdapter(extension, moduleTool, () => _registry.IsRegistered(extension)));
                    }
                }

                var activeCatalogSourceIds = toolsBySource.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var staleSourceId in _publishedCatalogSourceIds.Where(sourceId => !activeCatalogSourceIds.Contains(sourceId)).ToArray())
                {
                    _capabilityCatalog.PublishSource(CopilotCapabilitySourceKind.Plugin, staleSourceId, staleSourceId, Array.Empty<ICopilotTool>());
                    _publishedCatalogSourceIds.Remove(staleSourceId);
                }

                var activeTools = new List<ICopilotTool>();
                var activeToolCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var extension in registrySnapshot.Extensions)
                {
                    var catalogSourceId = BuildCatalogSourceId(extension.SourceId);
                    if (!toolsBySource.TryGetValue(catalogSourceId, out var sourceTools))
                        continue;
                    try
                    {
                        _capabilityCatalog.PublishSource(CopilotCapabilitySourceKind.Plugin, catalogSourceId, extension.SourceName, sourceTools);
                        _publishedCatalogSourceIds.Add(catalogSourceId);
                        activeTools.AddRange(sourceTools);
                        activeToolCounts[extension.SourceId] = sourceTools.Count;
                    }
                    catch (Exception ex)
                    {
                        issues.Add(new CopilotAgentExtensionIssue
                        {
                            SourceId = extension.SourceId,
                            Message = $"Module tools were not activated because their capability catalog entry could not be published: {ex.Message}",
                        });
                        _capabilityCatalog.PublishSource(CopilotCapabilitySourceKind.Plugin, catalogSourceId, extension.SourceName, Array.Empty<ICopilotTool>());
                        _publishedCatalogSourceIds.Remove(catalogSourceId);
                    }
                }

                _snapshot = new CopilotAgentExtensionBridgeSnapshot
                {
                    Revision = registrySnapshot.Revision,
                    Sources = registrySnapshot.Extensions.Select(extension => new CopilotAgentExtensionSourceSnapshot
                    {
                        SourceId = extension.SourceId,
                        SourceName = extension.SourceName,
                        SourceVersion = extension.SourceVersion,
                        ContextProviderCount = extension.ContextProviders.Count,
                        DeclaredToolCount = extension.Tools.Count,
                        ActiveToolCount = activeToolCounts.GetValueOrDefault(extension.SourceId),
                    }).ToArray(),
                    ContextProviders = contextProviders,
                    Tools = activeTools,
                    Issues = issues,
                };
            }
        }

        private static string BuildCatalogSourceId(string extensionSourceId) => "extension:" + extensionSourceId;
    }

    internal sealed class CopilotModuleContextProviderAdapter : ICopilotContextProvider
    {
        private readonly ICopilotContextProvider _provider;
        private readonly Func<bool> _isRegistrationActive;
        private readonly int _order;

        public CopilotModuleContextProviderAdapter(ICopilotContextProvider provider, Func<bool> isRegistrationActive)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _isRegistrationActive = isRegistrationActive ?? throw new ArgumentNullException(nameof(isRegistrationActive));
            _order = provider.Order;
        }

        public int Order => _order;

        public bool CanProvide(CopilotContextScope scope)
        {
            return _isRegistrationActive() && _provider.CanProvide(scope);
        }

        public async Task<CopilotContextItem?> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            if (!_isRegistrationActive())
                return null;
            var item = await _provider.CaptureAsync(request, cancellationToken);
            return _isRegistrationActive() ? item : null;
        }
    }

    internal sealed class CopilotModuleToolAdapter : ICopilotAgentDrivenTool, ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation, ICopilotCapabilityCatalogIdentity, ICopilotCapabilityCatalogVersionIdentity
    {
        private readonly CopilotAgentExtensionDescriptor _extension;
        private readonly ICopilotModuleTool _moduleTool;
        private readonly Func<bool> _isRegistrationActive;

        public CopilotModuleToolAdapter(CopilotAgentExtensionDescriptor extension, ICopilotModuleTool moduleTool, Func<bool> isRegistrationActive)
        {
            _extension = extension ?? throw new ArgumentNullException(nameof(extension));
            _moduleTool = moduleTool ?? throw new ArgumentNullException(nameof(moduleTool));
            _isRegistrationActive = isRegistrationActive ?? throw new ArgumentNullException(nameof(isRegistrationActive));
            using var schemaDocument = JsonDocument.Parse(_moduleTool.InputJsonSchema);
            InputSchema = CopilotToolInputSchema.FromJsonSchema(schemaDocument.RootElement);
            Capability = _moduleTool.Access == CopilotModuleToolAccess.ReadOnly
                ? CopilotToolCapabilityDescriptor.ReadOnly(_moduleTool.ExecutionTimeout)
                : CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent, _moduleTool.ExecutionTimeout);
        }

        public string Name => _moduleTool.Name.Trim();

        public string Description => _moduleTool.Description.Trim();

        public CopilotToolCapabilityDescriptor Capability { get; }

        public CopilotToolInputSchema InputSchema { get; }

        public string CatalogCapabilityKey => Name;

        public string CatalogVersionFingerprint => string.Join("\n", new[]
        {
            _extension.SourceVersion,
            _moduleTool.GetType().AssemblyQualifiedName ?? _moduleTool.GetType().FullName ?? _moduleTool.GetType().Name,
        });

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!_isRegistrationActive())
                return false;
            try
            {
                return _moduleTool.IsAvailable(CreateRequest(request, CopilotAgentToolInput.Empty, isApproved: false));
            }
            catch
            {
                return false;
            }
        }

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            if (_moduleTool.Access == CopilotModuleToolAccess.Write)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Summary = $"{Name} execution was denied.",
                    ErrorMessage = "Module write tools require approval for the exact Agent function call.",
                    FailureKind = CopilotToolFailureKind.Authorization,
                });
            }

            return ExecuteCoreAsync(request, toolInput, isApproved: false, cancellationToken);
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, isApproved: true, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            return new CopilotToolApprovalPresentation(
                $"Allow {Name} from {_extension.SourceName}?",
                $"This module capability can modify ColorVision state. Arguments: {CopilotToolApprovalArgumentFormatter.Create(toolInput)}");
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            bool isApproved,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            toolInput ??= CopilotAgentToolInput.Empty;
            if (!_isRegistrationActive())
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Summary = $"{Name} is no longer available.",
                    ErrorMessage = $"Agent extension '{_extension.SourceName}' was unloaded before this call could execute.",
                    FailureKind = CopilotToolFailureKind.Conflict,
                };
            }
            var result = await _moduleTool.ExecuteAsync(CreateRequest(request, toolInput, isApproved), cancellationToken);
            if (result == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Summary = $"{Name} returned no result.",
                    ErrorMessage = "The module tool returned null.",
                    FailureKind = CopilotToolFailureKind.Internal,
                };
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success,
                Summary = result.Summary,
                Content = result.Content,
                ErrorMessage = result.Success ? string.Empty : result.ErrorMessage,
                FailureKind = result.Success ? CopilotToolFailureKind.None : CopilotToolFailureKind.Unspecified,
            };
        }

        private static CopilotModuleToolRequest CreateRequest(CopilotAgentRequest request, CopilotAgentToolInput toolInput, bool isApproved)
        {
            return new CopilotModuleToolRequest
            {
                UserText = request.UserText,
                Mode = request.Mode switch
                {
                    CopilotAgentMode.Chat => CopilotModuleAgentMode.Chat,
                    CopilotAgentMode.Explain => CopilotModuleAgentMode.Explain,
                    CopilotAgentMode.Web => CopilotModuleAgentMode.Web,
                    CopilotAgentMode.Code => CopilotModuleAgentMode.Code,
                    CopilotAgentMode.Review => CopilotModuleAgentMode.Review,
                    CopilotAgentMode.Diagnose => CopilotModuleAgentMode.Diagnose,
                    _ => CopilotModuleAgentMode.Auto,
                },
                Arguments = new Dictionary<string, object?>(toolInput.Arguments, StringComparer.OrdinalIgnoreCase),
                ContextItems = request.ContextItems.ToArray(),
                SearchRootPaths = request.SearchRootPaths.ToArray(),
                ActiveDocumentPath = request.ActiveDocumentPath,
                IsApproved = isApproved,
            };
        }
    }
}
