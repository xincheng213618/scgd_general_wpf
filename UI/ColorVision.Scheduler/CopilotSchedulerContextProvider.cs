using ColorVision.UI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Scheduler
{
    public sealed class CopilotSchedulerContextProvider : ICopilotContextProvider
    {
        private static readonly string[] SchedulerIntentTerms =
        [
            "scheduler", "scheduled task", "scheduled job", "quartz job", "cron job", "execution history", "job history",
            "调度器", "计划任务", "定时任务", "调度任务", "任务调度", "执行历史", "任务历史", "任务失败", "任务重试",
        ];
        private readonly Func<CancellationToken, Task<CopilotSchedulerContextSnapshot?>> _snapshotProvider;
        private readonly Func<bool> _isActive;
        private readonly Func<bool> _isCurrentSurface;

        public CopilotSchedulerContextProvider(
            Func<CancellationToken, Task<CopilotSchedulerContextSnapshot?>> snapshotProvider,
            Func<bool>? isActive = null,
            Func<bool>? isCurrentSurface = null)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _isActive = isActive ?? (() => true);
            _isCurrentSurface = isCurrentSurface ?? (() => false);
        }

        public int Order => 34;

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

            return CopilotBusinessContextBuilder.BuildSchedulerContextItem(snapshot);
        }

        private bool ShouldCapture(CopilotContextRequest request)
        {
            if (request.Scope == CopilotContextScope.Diagnose || _isCurrentSurface())
                return true;

            var userText = request.UserText ?? string.Empty;
            return SchedulerIntentTerms.Any(term => userText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class CopilotSchedulerAgentExtension
    {
        public const string SourceId = "scheduler";

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
                SourceName = "Task Scheduler",
                SourceVersion = sourceVersion ?? string.Empty,
                ContextProviders = [contextProvider],
            });
        }
    }

    internal static class CopilotSchedulerContextHub
    {
        public static CopilotDynamicContextCoordinator<CopilotSchedulerContextSnapshot> Shared { get; } = new(
            CopilotAgentExtensionRegistry.Shared,
            (snapshotProvider, isActive) => new CopilotSchedulerContextProvider(
                snapshotProvider,
                isActive,
                IsCurrentSurface),
            CopilotSchedulerAgentExtension.Register);

        private static bool IsCurrentSurface()
        {
            return string.Equals(
                CopilotLiveContextRegistry.Current?.SourceId,
                CopilotSchedulerAgentExtension.SourceId,
                StringComparison.Ordinal);
        }
    }
}
