using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    }

    public sealed class CopilotAgentExtensionDescriptor
    {
        internal CopilotAgentExtensionDescriptor(
            string sourceId,
            string sourceName,
            string sourceVersion,
            IReadOnlyList<ICopilotContextProvider> contextProviders,
            IReadOnlyList<ICopilotModuleTool> tools,
            string registrationToken)
        {
            SourceId = sourceId;
            SourceName = sourceName;
            SourceVersion = sourceVersion;
            ContextProviders = contextProviders;
            Tools = tools;
            RegistrationToken = registrationToken;
        }

        public string SourceId { get; }

        public string SourceName { get; }

        public string SourceVersion { get; }

        public IReadOnlyList<ICopilotContextProvider> ContextProviders { get; }

        public IReadOnlyList<ICopilotModuleTool> Tools { get; }

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
                    Extensions = _extensions.Values.OrderBy(extension => extension.SourceId, StringComparer.OrdinalIgnoreCase).ToArray(),
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
            };
        }

        private static CopilotAgentExtensionDescriptor CreateDescriptor(CopilotAgentExtensionRegistration registration)
        {
            var sourceId = NormalizeSourceId(registration.SourceId);
            var sourceName = NormalizeRequiredText(registration.SourceName, MaximumSourceNameLength, "An Agent extension source name is required.");
            var sourceVersion = NormalizeOptionalText(registration.SourceVersion, MaximumSourceVersionLength);
            var contextProviders = (registration.ContextProviders ?? Array.Empty<ICopilotContextProvider>()).ToArray();
            var tools = (registration.Tools ?? Array.Empty<ICopilotModuleTool>()).ToArray();
            if (contextProviders.Any(provider => provider == null))
                throw new ArgumentException("An Agent extension context provider cannot be null.", nameof(registration));
            if (tools.Any(tool => tool == null))
                throw new ArgumentException("An Agent extension tool cannot be null.", nameof(registration));
            if (contextProviders.Length == 0 && tools.Length == 0)
                throw new ArgumentException("An Agent extension must provide at least one context provider or module tool.", nameof(registration));

            foreach (var provider in contextProviders)
                _ = provider.Order;
            foreach (var tool in tools)
                ValidateTool(tool);
            var duplicateToolName = tools.GroupBy(tool => tool.Name.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateToolName))
                throw new ArgumentException($"Agent extension '{sourceId}' declares module tool '{duplicateToolName}' more than once.", nameof(registration));

            return new CopilotAgentExtensionDescriptor(
                sourceId,
                sourceName,
                sourceVersion,
                contextProviders,
                tools,
                Guid.NewGuid().ToString("N"));
        }

        private static void ValidateTool(ICopilotModuleTool tool)
        {
            var name = tool.Name?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.Length > MaximumToolNameLength)
                throw new ArgumentException($"A module tool name must contain 1-{MaximumToolNameLength} characters.");
            if (name.Any(character => !(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-')))
                throw new ArgumentException($"Module tool '{name}' may contain only ASCII letters, digits, '_' and '-'.");
            _ = NormalizeRequiredText(tool.Description, MaximumToolDescriptionLength, $"Module tool '{name}' requires a description.");
            if (!Enum.IsDefined(tool.Access))
                throw new ArgumentException($"Module tool '{name}' has an invalid access mode.");
            if (tool.ExecutionTimeout <= TimeSpan.Zero || tool.ExecutionTimeout > TimeSpan.FromMinutes(10))
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
