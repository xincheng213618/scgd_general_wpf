using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    public enum CopilotPromptMode
    {
        Chat,
        Agent,
        Explain,
        Web,
        Code,
        Diagnose,
    }

    public enum CopilotContextScope
    {
        Chat,
        Agent,
        Diagnose,
    }

    public sealed class CopilotPromptRequest
    {
        public string Prompt { get; init; } = string.Empty;

        public CopilotPromptMode Mode { get; init; } = CopilotPromptMode.Agent;

        public bool StartNewConversation { get; init; } = true;

        public bool SendNow { get; init; } = true;

        public bool AttachContextSnapshot { get; init; }

        public string ContextAttachmentTitle { get; init; } = string.Empty;

        public string ContextAttachmentSourceId { get; init; } = string.Empty;

        public IReadOnlyList<CopilotContextItem> ContextItems { get; init; } = Array.Empty<CopilotContextItem>();
    }

    public sealed class CopilotLiveContext
    {
        public string SourceId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string AttachmentTitle { get; init; } = string.Empty;

        public IReadOnlyList<CopilotContextItem> SnapshotItems { get; init; } = Array.Empty<CopilotContextItem>();
    }

    public static class CopilotLiveContextRegistry
    {
        private static readonly object SyncRoot = new();
        private static CopilotLiveContext? _current;

        public static event EventHandler? CurrentChanged;

        public static CopilotLiveContext? Current
        {
            get
            {
                lock (SyncRoot)
                {
                    return _current;
                }
            }
        }

        public static void Publish(CopilotLiveContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            lock (SyncRoot)
            {
                _current = context;
            }

            CurrentChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Clear(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            var changed = false;
            lock (SyncRoot)
            {
                if (_current != null && string.Equals(_current.SourceId, sourceId, StringComparison.Ordinal))
                {
                    _current = null;
                    changed = true;
                }
            }

            if (changed)
                CurrentChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public sealed class CopilotContextRequest
    {
        public CopilotContextScope Scope { get; init; } = CopilotContextScope.Agent;

        public string UserText { get; init; } = string.Empty;

        public string SolutionDirectoryPath { get; init; } = string.Empty;

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();
    }

    public sealed class CopilotContextItem
    {
        public string Id { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;
    }

    public interface ICopilotService
    {
        bool IsAvailable { get; }

        void ShowPanel();

        bool Ask(CopilotPromptRequest request);
    }

    public interface ICopilotContextProvider
    {
        int Order { get; }

        bool CanProvide(CopilotContextScope scope);

        Task<CopilotContextItem?> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken);
    }

    public static class CopilotServiceRegistry
    {
        private static ICopilotService? _current;

        public static ICopilotService? Current => _current;

        public static void Register(ICopilotService service)
        {
            ArgumentNullException.ThrowIfNull(service);
            _current = service;
        }
    }
}