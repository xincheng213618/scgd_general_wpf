using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    public enum CopilotModuleToolAccess
    {
        ReadOnly,
        Write,
    }

    public enum CopilotModuleAgentMode
    {
        Chat,
        Auto,
        Explain,
        Web,
        Code,
        Review,
        Diagnose,
        Plan,
    }

    public sealed class CopilotModuleToolRequest
    {
        public string UserText { get; init; } = string.Empty;

        public CopilotModuleAgentMode Mode { get; init; } = CopilotModuleAgentMode.Auto;

        public IReadOnlyDictionary<string, object?> Arguments { get; init; } = new Dictionary<string, object?>();

        public IReadOnlyList<CopilotContextItem> ContextItems { get; init; } = Array.Empty<CopilotContextItem>();

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public bool IsApproved { get; init; }
    }

    public sealed class CopilotModuleToolResult
    {
        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public static CopilotModuleToolResult Ok(string summary, string? content = null) => new()
        {
            Success = true,
            Summary = summary ?? string.Empty,
            Content = content ?? string.Empty,
        };

        public static CopilotModuleToolResult Fail(string summary, string errorMessage) => new()
        {
            Summary = summary ?? string.Empty,
            ErrorMessage = errorMessage ?? string.Empty,
        };
    }

    /// <summary>
    /// A narrow, application-facing tool contract that business modules can implement without
    /// referencing the ColorVision executable or its Agent runtime packages. Write tools are
    /// always promoted to protected, approval-required Agent tools by the host adapter.
    /// </summary>
    public interface ICopilotModuleTool
    {
        string Name { get; }

        string Description { get; }

        CopilotModuleToolAccess Access => CopilotModuleToolAccess.ReadOnly;

        string InputJsonSchema => CopilotAgentExtensionDefaults.OptionalQueryJsonSchema;

        TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(30);

        bool IsAvailable(CopilotModuleToolRequest request) => true;

        Task<CopilotModuleToolResult> ExecuteAsync(CopilotModuleToolRequest request, CancellationToken cancellationToken);
    }

    public sealed class CopilotAgentExtensionRegistration
    {
        public string SourceId { get; init; } = string.Empty;

        public string SourceName { get; init; } = string.Empty;

        public string SourceVersion { get; init; } = string.Empty;

        public IReadOnlyList<ICopilotContextProvider> ContextProviders { get; init; } = Array.Empty<ICopilotContextProvider>();

        public IReadOnlyList<ICopilotModuleTool> Tools { get; init; } = Array.Empty<ICopilotModuleTool>();

        public IReadOnlyList<ICopilotModuleToolExecutionHook> ToolExecutionHooks { get; init; } =
            Array.Empty<ICopilotModuleToolExecutionHook>();
    }

    public sealed class CopilotAgentExtensionDescriptor
    {
        internal CopilotAgentExtensionDescriptor(
            string sourceId,
            string sourceName,
            string sourceVersion,
            IReadOnlyList<ICopilotContextProvider> contextProviders,
            IReadOnlyList<ICopilotModuleTool> tools,
            IReadOnlyList<ICopilotModuleToolExecutionHook> toolExecutionHooks,
            string registrationToken)
        {
            SourceId = sourceId;
            SourceName = sourceName;
            SourceVersion = sourceVersion;
            ContextProviders = contextProviders;
            Tools = tools;
            ToolExecutionHooks = toolExecutionHooks;
            RegistrationToken = registrationToken;
        }

        public string SourceId { get; }

        public string SourceName { get; }

        public string SourceVersion { get; }

        public IReadOnlyList<ICopilotContextProvider> ContextProviders { get; }

        public IReadOnlyList<ICopilotModuleTool> Tools { get; }

        public IReadOnlyList<ICopilotModuleToolExecutionHook> ToolExecutionHooks { get; }

        internal string RegistrationToken { get; }
    }

    public sealed class CopilotAgentExtensionRegistrySnapshot
    {
        public long Revision { get; init; }

        public IReadOnlyList<CopilotAgentExtensionDescriptor> Extensions { get; init; } = Array.Empty<CopilotAgentExtensionDescriptor>();
    }

    public sealed class CopilotAgentExtensionRegistryChangedEventArgs : EventArgs
    {
        public long PreviousRevision { get; init; }

        public long Revision { get; init; }

        public int ExtensionCount { get; init; }

        public int ContextProviderCount { get; init; }

        public int ToolCount { get; init; }

        public int ToolExecutionHookCount { get; init; }
    }

    public sealed class CopilotAgentExtensionRegistry
    {
        private const int MaximumExtensions = 64;
        private const int MaximumSourceIdLength = 80;
        private const int MaximumSourceNameLength = 120;
        private const int MaximumSourceVersionLength = 64;
        private const int MaximumToolNameLength = 64;
        private const int MaximumToolDescriptionLength = 800;
        private const int MaximumInputSchemaLength = 32_768;
        private const int MaximumToolExecutionHooks = 128;
        private const int MaximumHooksPerExtension = 16;
        private const int MaximumHookNameLength = 64;
        private const int MaximumHookPatternLength = 512;
        private readonly Dictionary<string, CopilotAgentExtensionDescriptor> _extensions = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncRoot = new();
        private long _revision;

        public static CopilotAgentExtensionRegistry Shared { get; } = new();

        public event EventHandler<CopilotAgentExtensionRegistryChangedEventArgs>? Changed;

        public IDisposable Register(CopilotAgentExtensionRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(registration);
            var descriptor = CreateDescriptor(registration);
            CopilotAgentExtensionRegistryChangedEventArgs change;
            lock (_syncRoot)
            {
                if (_extensions.TryGetValue(descriptor.SourceId, out var existing))
                    throw new InvalidOperationException($"Copilot Agent extension '{descriptor.SourceId}' is already registered as '{existing.SourceName}'.");
                if (_extensions.Count >= MaximumExtensions)
                    throw new InvalidOperationException($"The Copilot Agent extension registry reached its {MaximumExtensions}-extension limit.");

                var existingToolNames = _extensions.Values
                    .SelectMany(extension => extension.Tools)
                    .Select(tool => tool.Name.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var conflictingToolName = descriptor.Tools.Select(tool => tool.Name.Trim()).FirstOrDefault(existingToolNames.Contains);
                if (!string.IsNullOrWhiteSpace(conflictingToolName))
                    throw new InvalidOperationException($"Copilot module tool '{conflictingToolName}' is already registered by another extension.");
                var prospectiveHookCount = _extensions.Values.Sum(extension => extension.ToolExecutionHooks.Count)
                    + descriptor.ToolExecutionHooks.Count;
                if (prospectiveHookCount > MaximumToolExecutionHooks)
                {
                    throw new InvalidOperationException(
                        $"The Copilot Agent extension registry reached its {MaximumToolExecutionHooks}-hook limit.");
                }

                var previousRevision = _revision;
                _extensions.Add(descriptor.SourceId, descriptor);
                _revision++;
                change = CreateChange(previousRevision);
            }

            PublishChanged(change);
            return new Registration(this, descriptor.SourceId, descriptor.RegistrationToken);
        }

        public CopilotAgentExtensionRegistrySnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                return new CopilotAgentExtensionRegistrySnapshot
                {
                    Revision = _revision,
                    Extensions = Array.AsReadOnly(_extensions.Values
                        .OrderBy(extension => extension.SourceId, StringComparer.OrdinalIgnoreCase)
                        .ToArray()),
                };
            }
        }

        public bool IsRegistered(CopilotAgentExtensionDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            lock (_syncRoot)
            {
                return _extensions.TryGetValue(descriptor.SourceId, out var current)
                    && string.Equals(current.RegistrationToken, descriptor.RegistrationToken, StringComparison.Ordinal);
            }
        }

        private void Unregister(string sourceId, string registrationToken)
        {
            CopilotAgentExtensionRegistryChangedEventArgs? change = null;
            lock (_syncRoot)
            {
                if (!_extensions.TryGetValue(sourceId, out var current)
                    || !string.Equals(current.RegistrationToken, registrationToken, StringComparison.Ordinal))
                {
                    return;
                }

                var previousRevision = _revision;
                _extensions.Remove(sourceId);
                _revision++;
                change = CreateChange(previousRevision);
            }

            PublishChanged(change);
        }

        private CopilotAgentExtensionRegistryChangedEventArgs CreateChange(long previousRevision)
        {
            return new CopilotAgentExtensionRegistryChangedEventArgs
            {
                PreviousRevision = previousRevision,
                Revision = _revision,
                ExtensionCount = _extensions.Count,
                ContextProviderCount = _extensions.Values.Sum(extension => extension.ContextProviders.Count),
                ToolCount = _extensions.Values.Sum(extension => extension.Tools.Count),
                ToolExecutionHookCount = _extensions.Values.Sum(extension => extension.ToolExecutionHooks.Count),
            };
        }

        private static CopilotAgentExtensionDescriptor CreateDescriptor(CopilotAgentExtensionRegistration registration)
        {
            var sourceId = NormalizeSourceId(registration.SourceId);
            var sourceName = NormalizeRequiredText(registration.SourceName, MaximumSourceNameLength, "An Agent extension source name is required.");
            var sourceVersion = NormalizeOptionalText(registration.SourceVersion, MaximumSourceVersionLength);
            var contextProviders = (registration.ContextProviders ?? Array.Empty<ICopilotContextProvider>()).ToArray();
            var tools = (registration.Tools ?? Array.Empty<ICopilotModuleTool>()).ToArray();
            var toolExecutionHooks = (registration.ToolExecutionHooks ?? Array.Empty<ICopilotModuleToolExecutionHook>()).ToArray();
            if (contextProviders.Any(provider => provider == null))
                throw new ArgumentException("An Agent extension context provider cannot be null.", nameof(registration));
            if (tools.Any(tool => tool == null))
                throw new ArgumentException("An Agent extension tool cannot be null.", nameof(registration));
            if (toolExecutionHooks.Any(hook => hook == null))
                throw new ArgumentException("An Agent extension tool execution hook cannot be null.", nameof(registration));
            if (toolExecutionHooks.Length > MaximumHooksPerExtension)
            {
                throw new ArgumentException(
                    $"An Agent extension may declare at most {MaximumHooksPerExtension} tool execution hooks.",
                    nameof(registration));
            }
            if (contextProviders.Length == 0 && tools.Length == 0 && toolExecutionHooks.Length == 0)
            {
                throw new ArgumentException(
                    "An Agent extension must provide at least one context provider, module tool, or tool execution hook.",
                    nameof(registration));
            }

            foreach (var provider in contextProviders)
                _ = provider.Order;
            var registeredTools = tools.Select(CreateRegisteredTool).ToArray();
            var registeredHooks = toolExecutionHooks.Select(CreateRegisteredHook).ToArray();
            var duplicateToolName = registeredTools.GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateToolName))
                throw new ArgumentException($"Agent extension '{sourceId}' declares module tool '{duplicateToolName}' more than once.", nameof(registration));
            var duplicateHookName = registeredHooks
                .GroupBy(hook => hook.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateHookName))
            {
                throw new ArgumentException(
                    $"Agent extension '{sourceId}' declares tool execution hook '{duplicateHookName}' more than once.",
                    nameof(registration));
            }

            return new CopilotAgentExtensionDescriptor(
                sourceId,
                sourceName,
                sourceVersion,
                Array.AsReadOnly(contextProviders),
                Array.AsReadOnly<ICopilotModuleTool>(registeredTools),
                Array.AsReadOnly<ICopilotModuleToolExecutionHook>(registeredHooks),
                Guid.NewGuid().ToString("N"));
        }

        private static RegisteredModuleTool CreateRegisteredTool(ICopilotModuleTool tool)
        {
            var name = tool.Name?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.Length > MaximumToolNameLength)
                throw new ArgumentException($"A module tool name must contain 1-{MaximumToolNameLength} characters.");
            if (name.Any(character => !(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-')))
                throw new ArgumentException($"Module tool '{name}' may contain only ASCII letters, digits, '_' and '-'.");
            var description = NormalizeRequiredText(tool.Description, MaximumToolDescriptionLength, $"Module tool '{name}' requires a description.");
            var access = tool.Access;
            if (!Enum.IsDefined(access))
                throw new ArgumentException($"Module tool '{name}' has an invalid access mode.");
            var executionTimeout = tool.ExecutionTimeout;
            if (executionTimeout <= TimeSpan.Zero || executionTimeout > TimeSpan.FromMinutes(10))
                throw new ArgumentException($"Module tool '{name}' must use an execution timeout between zero and ten minutes.");

            var schemaText = tool.InputJsonSchema?.Trim() ?? string.Empty;
            if (schemaText.Length == 0 || schemaText.Length > MaximumInputSchemaLength)
                throw new ArgumentException($"Module tool '{name}' must provide a JSON input schema no longer than {MaximumInputSchemaLength} characters.");
            try
            {
                using var document = JsonDocument.Parse(schemaText);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException($"Module tool '{name}' input schema must be a JSON object.");
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Module tool '{name}' input schema is not valid JSON: {ex.Message}", ex);
            }

            return new RegisteredModuleTool(
                tool,
                name,
                description,
                access,
                schemaText,
                executionTimeout);
        }

        private static RegisteredModuleToolExecutionHook CreateRegisteredHook(
            ICopilotModuleToolExecutionHook hook)
        {
            var name = hook.Name?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.Length > MaximumHookNameLength)
            {
                throw new ArgumentException(
                    $"A module tool execution hook name must contain 1-{MaximumHookNameLength} characters.");
            }
            if (name.Any(character => !(character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '_'
                or '-'
                or '.')))
            {
                throw new ArgumentException(
                    $"Module tool execution hook '{name}' may contain only ASCII letters, digits, '_', '-' and '.'.");
            }

            var rawPattern = hook.ToolNamePattern;
            var pattern = string.IsNullOrWhiteSpace(rawPattern)
                ? "*"
                : rawPattern.Trim();
            if (pattern.Length > MaximumHookPatternLength || pattern.Any(char.IsControl))
            {
                throw new ArgumentException(
                    $"Module tool execution hook '{name}' matcher must be at most {MaximumHookPatternLength} visible characters.");
            }
            try
            {
                _ = new Regex(
                    pattern == "*" ? ".*" : pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                throw new ArgumentException(
                    $"Module tool execution hook '{name}' matcher must be a valid non-backtracking regular expression.",
                    ex);
            }

            var order = hook.Order;
            var executionMode = hook.ExecutionMode;
            if (!Enum.IsDefined(executionMode))
            {
                throw new ArgumentException(
                    $"Module tool execution hook '{name}' has an invalid execution mode.");
            }

            return hook is ICopilotModuleToolPermissionRequestHook permissionRequestHook
                ? new RegisteredModuleToolPermissionRequestHook(
                    hook,
                    permissionRequestHook,
                    name,
                    pattern,
                    order,
                    executionMode)
                : new RegisteredModuleToolExecutionHook(
                    hook,
                    name,
                    pattern,
                    order,
                    executionMode);
        }

        private static string NormalizeSourceId(string sourceId)
        {
            var normalized = sourceId?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalized.Length == 0 || normalized.Length > MaximumSourceIdLength)
                throw new ArgumentException($"An Agent extension source id must contain 1-{MaximumSourceIdLength} characters.", nameof(sourceId));
            if (normalized.Any(character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or ':' or '.' or '_' or '-')))
                throw new ArgumentException("An Agent extension source id may contain only ASCII letters, digits, ':', '.', '_' and '-'.", nameof(sourceId));
            return normalized;
        }

        private static string NormalizeRequiredText(string? value, int maximumLength, string errorMessage)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                throw new ArgumentException(errorMessage);
            if (normalized.Length > maximumLength)
                throw new ArgumentException($"{errorMessage} Maximum length is {maximumLength} characters.");
            return normalized;
        }

        private static string NormalizeOptionalText(string? value, int maximumLength)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length > maximumLength)
                throw new ArgumentException($"An Agent extension source version cannot exceed {maximumLength} characters.");
            return normalized;
        }

        private void PublishChanged(CopilotAgentExtensionRegistryChangedEventArgs? change)
        {
            if (change == null || Changed is not { } handlers)
                return;
            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<CopilotAgentExtensionRegistryChangedEventArgs>>())
            {
                try
                {
                    handler(this, change);
                }
                catch
                {
                }
            }
        }

        private sealed class RegisteredModuleTool : ICopilotModuleTool
        {
            private readonly ICopilotModuleTool _implementation;

            public RegisteredModuleTool(
                ICopilotModuleTool implementation,
                string name,
                string description,
                CopilotModuleToolAccess access,
                string inputJsonSchema,
                TimeSpan executionTimeout)
            {
                _implementation = implementation;
                Name = name;
                Description = description;
                Access = access;
                InputJsonSchema = inputJsonSchema;
                ExecutionTimeout = executionTimeout;
            }

            public string Name { get; }

            public string Description { get; }

            public CopilotModuleToolAccess Access { get; }

            public string InputJsonSchema { get; }

            public TimeSpan ExecutionTimeout { get; }

            public bool IsAvailable(CopilotModuleToolRequest request) =>
                _implementation.IsAvailable(request);

            public Task<CopilotModuleToolResult> ExecuteAsync(
                CopilotModuleToolRequest request,
                CancellationToken cancellationToken) =>
                _implementation.ExecuteAsync(request, cancellationToken);
        }

        private class RegisteredModuleToolExecutionHook : ICopilotModuleToolExecutionHook
        {
            private readonly ICopilotModuleToolExecutionHook _implementation;

            public RegisteredModuleToolExecutionHook(
                ICopilotModuleToolExecutionHook implementation,
                string name,
                string toolNamePattern,
                int order,
                CopilotModuleToolExecutionHookMode executionMode)
            {
                _implementation = implementation;
                Name = name;
                ToolNamePattern = toolNamePattern;
                Order = order;
                ExecutionMode = executionMode;
            }

            public string Name { get; }

            public string ToolNamePattern { get; }

            public int Order { get; }

            public CopilotModuleToolExecutionHookMode ExecutionMode { get; }

            public Task<CopilotModuleToolExecutionHookDecision> BeforeExecuteAsync(
                CopilotModuleToolExecutionHookContext context,
                CancellationToken cancellationToken) =>
                _implementation.BeforeExecuteAsync(context, cancellationToken);

            public Task AfterExecuteAsync(
                CopilotModuleToolExecutionHookOutcome outcome,
                CancellationToken cancellationToken) =>
                _implementation.AfterExecuteAsync(outcome, cancellationToken);
        }

        private sealed class RegisteredModuleToolPermissionRequestHook
            : RegisteredModuleToolExecutionHook, ICopilotModuleToolPermissionRequestHook
        {
            private readonly ICopilotModuleToolPermissionRequestHook _implementation;

            public RegisteredModuleToolPermissionRequestHook(
                ICopilotModuleToolExecutionHook hook,
                ICopilotModuleToolPermissionRequestHook implementation,
                string name,
                string toolNamePattern,
                int order,
                CopilotModuleToolExecutionHookMode executionMode)
                : base(hook, name, toolNamePattern, order, executionMode)
            {
                _implementation = implementation;
            }

            public Task<CopilotModuleToolPermissionRequestDecision> OnPermissionRequestAsync(
                CopilotModuleToolExecutionHookContext context,
                CancellationToken cancellationToken) =>
                _implementation.OnPermissionRequestAsync(context, cancellationToken);
        }

        private sealed class Registration : IDisposable
        {
            private CopilotAgentExtensionRegistry? _owner;
            private readonly string _sourceId;
            private readonly string _registrationToken;

            public Registration(CopilotAgentExtensionRegistry owner, string sourceId, string registrationToken)
            {
                _owner = owner;
                _sourceId = sourceId;
                _registrationToken = registrationToken;
            }

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                owner?.Unregister(_sourceId, _registrationToken);
            }
        }
    }

    public static class CopilotAgentExtensionDefaults
    {
        public const string OptionalQueryJsonSchema = "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\",\"description\":\"Focused request or target for this module capability.\"}},\"additionalProperties\":false}";
    }
}
