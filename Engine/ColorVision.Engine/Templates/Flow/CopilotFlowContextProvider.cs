using ColorVision.UI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    public sealed class CopilotFlowContextProvider : ICopilotContextProvider
    {
        private static readonly string[] FlowIntentTerms =
        [
            "flow", "workflow", "node", "graph", "batch",
            "流程", "工作流", "节点", "连线", "批次", "流程图",
        ];
        private readonly Func<CancellationToken, Task<CopilotFlowContextSnapshot?>> _snapshotProvider;
        private readonly Func<bool> _isActive;
        private readonly Func<bool> _isCurrentSurface;

        public CopilotFlowContextProvider(
            Func<CancellationToken, Task<CopilotFlowContextSnapshot?>> snapshotProvider,
            Func<bool>? isActive = null,
            Func<bool>? isCurrentSurface = null)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _isActive = isActive ?? (() => true);
            _isCurrentSurface = isCurrentSurface ?? (() => false);
        }

        public int Order => 20;

        public bool CanProvide(CopilotContextScope scope)
        {
            return _isActive() && (scope == CopilotContextScope.Agent || scope == CopilotContextScope.Diagnose);
        }

        public async Task<CopilotContextItem?> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_isActive() || !ShouldCapture(request))
                return null;

            var snapshot = await _snapshotProvider(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_isActive() || snapshot == null || !HasMeaningfulSnapshot(snapshot))
                return null;

            return CopilotBusinessContextBuilder.BuildFlowContextItem(snapshot);
        }

        public static CopilotFlowContextProvider Create(FlowEngineManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);
            return new CopilotFlowContextProvider(
                cancellationToken => CaptureManagerSnapshotAsync(manager, cancellationToken),
                () => ReferenceEquals(FlowEngineManager.Current, manager),
                () => string.Equals(CopilotLiveContextRegistry.Current?.SourceId, CopilotFlowAgentExtension.SourceId, StringComparison.Ordinal));
        }

        internal static bool HasMeaningfulSnapshot(CopilotFlowContextSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return snapshot.IsRunning
                || snapshot.Nodes.Count > 0
                || snapshot.Edges.Count > 0
                || !string.IsNullOrWhiteSpace(snapshot.FlowName)
                || !string.IsNullOrWhiteSpace(snapshot.TemplateId)
                || !string.IsNullOrWhiteSpace(snapshot.BatchSerialNumber)
                || !string.IsNullOrWhiteSpace(snapshot.RecentRunMessage);
        }

        private bool ShouldCapture(CopilotContextRequest request)
        {
            if (request.Scope == CopilotContextScope.Diagnose || _isCurrentSurface())
                return true;

            var userText = request.UserText ?? string.Empty;
            return FlowIntentTerms.Any(term => userText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<CopilotFlowContextSnapshot?> CaptureManagerSnapshotAsync(
            FlowEngineManager manager,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dispatcher = manager.View?.Dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return await dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return manager.CaptureCopilotFlowSnapshot();
                });
            }

            return manager.CaptureCopilotFlowSnapshot();
        }
    }

    public static class CopilotFlowAgentExtension
    {
        public const string SourceId = "flow-engine-manager";

        public static IDisposable Register(
            CopilotAgentExtensionRegistry registry,
            ICopilotContextProvider contextProvider,
            string? sourceVersion = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(contextProvider);
            return registry.Register(new CopilotAgentExtensionRegistration
            {
                SourceId = SourceId,
                SourceName = "Flow Engine",
                SourceVersion = sourceVersion ?? string.Empty,
                ContextProviders = [contextProvider],
            });
        }
    }
}
