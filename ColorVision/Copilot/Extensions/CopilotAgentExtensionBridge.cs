using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public int DeclaredHookCount { get; init; }

        public int ActiveHookCount { get; init; }

        public IReadOnlyList<CopilotAgentExtensionHookSnapshot> Hooks { get; init; } =
            Array.Empty<CopilotAgentExtensionHookSnapshot>();
    }

    public sealed class CopilotAgentExtensionHookSnapshot
    {
        public string SourceId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string ToolNamePattern { get; init; } = "*";

        public int Order { get; init; }

        public bool IsActive { get; init; }
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
                CopilotToolRegistry.CreateCoreDefaultTools().Select(tool => tool.Name),
                CopilotToolExecutionHookRegistry.Shared),
            LazyThreadSafetyMode.ExecutionAndPublication);
        private readonly CopilotAgentExtensionRegistry _registry;
        private readonly CopilotCapabilityCatalog _capabilityCatalog;
        private readonly CopilotToolExecutionHookRegistry _hookRegistry;
        private readonly HashSet<string> _reservedToolNames;
        private readonly HashSet<string> _publishedCatalogSourceIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IDisposable> _publishedHookRegistrations = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncRoot = new();
        private CopilotAgentExtensionBridgeSnapshot _snapshot = new();
        private bool _disposed;

        public CopilotAgentExtensionBridge(
            CopilotAgentExtensionRegistry registry,
            CopilotCapabilityCatalog capabilityCatalog,
            IEnumerable<string>? reservedToolNames = null,
            CopilotToolExecutionHookRegistry? hookRegistry = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _capabilityCatalog = capabilityCatalog ?? throw new ArgumentNullException(nameof(capabilityCatalog));
            _hookRegistry = hookRegistry ?? CopilotToolExecutionHookRegistry.Shared;
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
            IDisposable[] hookRegistrations;
            lock (_syncRoot)
            {
                if (_disposed)
                    return;
                _disposed = true;
                sourceIds = _publishedCatalogSourceIds.ToArray();
                _publishedCatalogSourceIds.Clear();
                hookRegistrations = _publishedHookRegistrations.Values.ToArray();
                _publishedHookRegistrations.Clear();
                _snapshot = new CopilotAgentExtensionBridgeSnapshot { Revision = _snapshot.Revision };
            }

            _registry.Changed -= Registry_Changed;
            foreach (var registration in hookRegistrations)
                registration.Dispose();
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

                var activeHookExtensionIds = registrySnapshot.Extensions
                    .Where(extension => extension.ToolExecutionHooks.Count > 0)
                    .Select(extension => extension.SourceId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var staleSourceId in _publishedHookRegistrations.Keys
                    .Where(sourceId => !activeHookExtensionIds.Contains(sourceId))
                    .ToArray())
                {
                    _publishedHookRegistrations.Remove(staleSourceId, out var staleRegistration);
                    staleRegistration?.Dispose();
                }

                var activeHookCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var extension in registrySnapshot.Extensions.Where(extension => extension.ToolExecutionHooks.Count > 0))
                {
                    if (_publishedHookRegistrations.ContainsKey(extension.SourceId))
                    {
                        activeHookCounts[extension.SourceId] = extension.ToolExecutionHooks.Count;
                        continue;
                    }

                    try
                    {
                        var definitions = extension.ToolExecutionHooks.Select(hook =>
                            CreateHookRegistrationDefinition(
                                extension,
                                hook,
                                () => _registry.IsRegistered(extension)));
                        _publishedHookRegistrations.Add(
                            extension.SourceId,
                            _hookRegistry.RegisterBatch(definitions));
                        activeHookCounts[extension.SourceId] = extension.ToolExecutionHooks.Count;
                    }
                    catch (Exception ex)
                    {
                        issues.Add(new CopilotAgentExtensionIssue
                        {
                            SourceId = extension.SourceId,
                            Message = $"Module tool execution hooks were not activated: {ex.Message}",
                        });
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
                        DeclaredHookCount = extension.ToolExecutionHooks.Count,
                        ActiveHookCount = activeHookCounts.GetValueOrDefault(extension.SourceId),
                        Hooks = extension.ToolExecutionHooks.Select(hook => new CopilotAgentExtensionHookSnapshot
                        {
                            SourceId = BuildHookSourceId(extension.SourceId, hook.Name),
                            Name = hook.Name.Trim(),
                            ToolNamePattern = NormalizeHookPattern(hook.ToolNamePattern),
                            Order = hook.Order,
                            IsActive = activeHookCounts.ContainsKey(extension.SourceId),
                        }).ToArray(),
                    }).ToArray(),
                    ContextProviders = contextProviders,
                    Tools = activeTools,
                    Issues = issues,
                };
            }
        }

        private static string BuildCatalogSourceId(string extensionSourceId) => "extension:" + extensionSourceId;

        private static CopilotToolExecutionHookRegistrationDefinition CreateHookRegistrationDefinition(
            CopilotAgentExtensionDescriptor extension,
            ICopilotModuleToolExecutionHook hook,
            Func<bool> isRegistrationActive)
        {
            var adapter = hook is ICopilotModuleToolPermissionRequestHook permissionRequestHook
                ? new CopilotModuleToolPermissionRequestHookAdapter(
                    hook,
                    permissionRequestHook,
                    isRegistrationActive)
                : new CopilotModuleToolExecutionHookAdapter(hook, isRegistrationActive);
            return new CopilotToolExecutionHookRegistrationDefinition(
                BuildHookSourceId(extension.SourceId, hook.Name),
                adapter,
                NormalizeHookPattern(hook.ToolNamePattern),
                hook.Order,
                ComputeHookConfigurationFingerprint(extension, hook));
        }

        private static string BuildHookSourceId(string extensionSourceId, string hookName) =>
            $"extension:{extensionSourceId}:hook:{hookName.Trim().ToLowerInvariant()}";

        private static string NormalizeHookPattern(string? pattern) =>
            string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();

        private static string ComputeHookConfigurationFingerprint(
            CopilotAgentExtensionDescriptor extension,
            ICopilotModuleToolExecutionHook hook)
        {
            var hookType = hook.GetType();
            var assemblyName = hookType.Assembly.GetName();
            var identity = string.Join("|", new[]
            {
                extension.SourceVersion,
                assemblyName.Name ?? string.Empty,
                assemblyName.Version?.ToString() ?? string.Empty,
                hookType.Module.ModuleVersionId.ToString("N"),
                hookType.FullName ?? hookType.Name,
                hook.Name.Trim(),
                NormalizeHookPattern(hook.ToolNamePattern),
                hook.Order.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
            return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        }
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
            if (!request.IncludeExtensionProviders || !_isRegistrationActive())
                return null;
            var item = await _provider.CaptureAsync(request, cancellationToken);
            return _isRegistrationActive() ? item : null;
        }
    }

    internal class CopilotModuleToolExecutionHookAdapter : ICopilotToolExecutionHook
    {
        private readonly ICopilotModuleToolExecutionHook _hook;
        private readonly Func<bool> _isRegistrationActive;

        public CopilotModuleToolExecutionHookAdapter(
            ICopilotModuleToolExecutionHook hook,
            Func<bool> isRegistrationActive)
        {
            _hook = hook ?? throw new ArgumentNullException(nameof(hook));
            _isRegistrationActive = isRegistrationActive ?? throw new ArgumentNullException(nameof(isRegistrationActive));
        }

        public async Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            if (!IsRegistrationActive)
            {
                return CopilotToolExecutionHookDecision.Deny(
                    "The business-module hook was unloaded before it could inspect this tool call.",
                    "extension_hook_unloaded",
                    CopilotToolFailureKind.Conflict);
            }

            var decision = await _hook.BeforeExecuteAsync(
                CreateContext(context.Invocation),
                cancellationToken);
            if (decision?.ShouldProceed != false)
                return CopilotToolExecutionHookDecision.Proceed;

            return CopilotToolExecutionHookDecision.Deny(
                string.IsNullOrWhiteSpace(decision.Reason)
                    ? "A business-module hook denied this tool call."
                    : decision.Reason,
                string.IsNullOrWhiteSpace(decision.FailureCode)
                    ? "extension_hook_denied"
                    : decision.FailureCode);
        }

        public async Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            if (!IsRegistrationActive)
            {
                throw new CopilotToolExecutionHookSkippedException(
                    "extension_hook_unloaded",
                    "The business-module hook was unloaded before its post-execution callback.");
            }

            await _hook.AfterExecuteAsync(
                new CopilotModuleToolExecutionHookOutcome
                {
                    Context = CreateContext(outcome.Invocation),
                    State = MapState(outcome.Execution.State),
                    Success = outcome.Result.Success,
                    Summary = outcome.Result.Summary,
                    ErrorMessage = outcome.Result.Success ? string.Empty : outcome.Result.ErrorMessage,
                    FailureCode = outcome.Result.Success
                        ? string.Empty
                        : CopilotToolFailureCode.Normalize(outcome.Result.FailureCode),
                    DurationMs = Math.Max(0, outcome.Execution.DurationMs),
                },
                cancellationToken);
        }

        protected bool IsRegistrationActive => _isRegistrationActive();

        protected static CopilotModuleToolExecutionHookContext CreateContext(
            CopilotToolInvocation invocation)
        {
            var arguments = new Dictionary<string, object?>(
                invocation.ToolInput.Arguments ?? new Dictionary<string, object?>(),
                StringComparer.OrdinalIgnoreCase);
            return new CopilotModuleToolExecutionHookContext
            {
                CallId = invocation.CallId,
                ToolName = invocation.Tool.Name,
                Access = invocation.Tool.Capability.Access == CopilotToolAccess.Write
                    ? CopilotModuleToolAccess.Write
                    : CopilotModuleToolAccess.ReadOnly,
                Mode = invocation.AgentRequest.Mode switch
                {
                    CopilotAgentMode.Chat => CopilotModuleAgentMode.Chat,
                    CopilotAgentMode.Explain => CopilotModuleAgentMode.Explain,
                    CopilotAgentMode.Web => CopilotModuleAgentMode.Web,
                    CopilotAgentMode.Code => CopilotModuleAgentMode.Code,
                    CopilotAgentMode.Review => CopilotModuleAgentMode.Review,
                    CopilotAgentMode.Diagnose => CopilotModuleAgentMode.Diagnose,
                    CopilotAgentMode.Plan => CopilotModuleAgentMode.Plan,
                    _ => CopilotModuleAgentMode.Auto,
                },
                Arguments = new ReadOnlyDictionary<string, object?>(arguments),
                FrameworkApprovalGranted = invocation.FrameworkApprovalGranted,
            };
        }

        private static CopilotModuleToolExecutionState MapState(
            CopilotToolExecutionState state) => state switch
        {
            CopilotToolExecutionState.Completed => CopilotModuleToolExecutionState.Completed,
            CopilotToolExecutionState.TimedOut => CopilotModuleToolExecutionState.TimedOut,
            CopilotToolExecutionState.Denied => CopilotModuleToolExecutionState.Denied,
            CopilotToolExecutionState.Cancelled or CopilotToolExecutionState.Interrupted =>
                CopilotModuleToolExecutionState.Cancelled,
            CopilotToolExecutionState.AwaitingApproval => CopilotModuleToolExecutionState.AwaitingApproval,
            _ => CopilotModuleToolExecutionState.Failed,
        };
    }

    internal sealed class CopilotModuleToolPermissionRequestHookAdapter
        : CopilotModuleToolExecutionHookAdapter, ICopilotToolPermissionRequestHook
    {
        private readonly ICopilotModuleToolPermissionRequestHook _permissionRequestHook;

        public CopilotModuleToolPermissionRequestHookAdapter(
            ICopilotModuleToolExecutionHook hook,
            ICopilotModuleToolPermissionRequestHook permissionRequestHook,
            Func<bool> isRegistrationActive)
            : base(hook, isRegistrationActive)
        {
            _permissionRequestHook = permissionRequestHook
                ?? throw new ArgumentNullException(nameof(permissionRequestHook));
        }

        public async Task<CopilotToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken)
        {
            if (!IsRegistrationActive)
            {
                return CopilotToolPermissionRequestDecision.Deny(
                    "The business-module hook was unloaded before it could inspect this permission request.",
                    "extension_hook_unloaded");
            }

            var decision = await _permissionRequestHook.OnPermissionRequestAsync(
                CreateContext(context.Invocation),
                cancellationToken);
            if (decision?.ShouldPrompt != false)
                return CopilotToolPermissionRequestDecision.Prompt;

            return CopilotToolPermissionRequestDecision.Deny(
                string.IsNullOrWhiteSpace(decision.Reason)
                    ? "A business-module hook denied this permission request."
                    : decision.Reason,
                string.IsNullOrWhiteSpace(decision.FailureCode)
                    ? "extension_permission_hook_denied"
                    : decision.FailureCode);
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
            if (!request.CodexPluginsEnabled || !_isRegistrationActive())
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
            if (!request.CodexPluginsEnabled)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Summary = $"{Name} is unavailable for this submitted turn.",
                    ErrorMessage = "Codex features.plugins=false excludes Copilot extension tools from this submitted turn.",
                    FailureKind = CopilotToolFailureKind.Authorization,
                };
            }
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
                    CopilotAgentMode.Plan => CopilotModuleAgentMode.Plan,
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
