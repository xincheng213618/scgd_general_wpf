using ColorVision.UI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine
{
    public sealed class CopilotMeasurementResultContextProvider : ICopilotContextProvider
    {
        private static readonly string[] MeasurementResultIntentTerms =
        [
            "measurement result", "inspection result", "test result", "batch result", "result history", "algorithm result", "image result",
            "检测结果", "测量结果", "测试结果", "批次结果", "历史结果", "算法结果", "取图结果", "批次记录",
        ];
        private readonly Func<CancellationToken, Task<CopilotMeasurementResultContextSnapshot?>> _snapshotProvider;
        private readonly Func<bool> _isActive;
        private readonly Func<bool> _isCurrentSurface;

        public CopilotMeasurementResultContextProvider(
            Func<CancellationToken, Task<CopilotMeasurementResultContextSnapshot?>> snapshotProvider,
            Func<bool>? isActive = null,
            Func<bool>? isCurrentSurface = null)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _isActive = isActive ?? (() => true);
            _isCurrentSurface = isCurrentSurface ?? (() => false);
        }

        public int Order => 32;

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
            if (!_isActive() || snapshot == null)
                return null;

            return CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(snapshot);
        }

        private bool ShouldCapture(CopilotContextRequest request)
        {
            if (request.Scope == CopilotContextScope.Diagnose || _isCurrentSurface())
                return true;

            var userText = request.UserText ?? string.Empty;
            return MeasurementResultIntentTerms.Any(term => userText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class CopilotMeasurementResultAgentExtension
    {
        public const string SourceId = "measurement-results";

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
                SourceName = "Measurement Results",
                SourceVersion = sourceVersion ?? string.Empty,
                ContextProviders = [contextProvider],
            });
        }
    }

    internal static class CopilotMeasurementResultContextHub
    {
        public static CopilotDynamicContextCoordinator<CopilotMeasurementResultContextSnapshot> Shared { get; } = new(
            CopilotAgentExtensionRegistry.Shared,
            (snapshotProvider, isActive) => new CopilotMeasurementResultContextProvider(
                snapshotProvider,
                isActive,
                IsCurrentSurface),
            CopilotMeasurementResultAgentExtension.Register);

        private static bool IsCurrentSurface()
        {
            return string.Equals(
                CopilotLiveContextRegistry.Current?.SourceId,
                CopilotMeasurementResultAgentExtension.SourceId,
                StringComparison.Ordinal);
        }
    }
}
