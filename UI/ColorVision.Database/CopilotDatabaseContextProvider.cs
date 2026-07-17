using ColorVision.UI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Database
{
    public sealed class CopilotDatabaseContextProvider : ICopilotContextProvider
    {
        private static readonly string[] DatabaseIntentTerms =
        [
            "database", "mysql", "sqlite", "sql", "table", "schema", "column", "query result", "row count",
            "数据库", "数据源", "数据表", "表结构", "字段", "列", "查询结果", "记录数", "行数",
        ];
        private readonly Func<CancellationToken, Task<CopilotDatabaseContextSnapshot?>> _snapshotProvider;
        private readonly Func<bool> _isActive;
        private readonly Func<bool> _isCurrentSurface;

        public CopilotDatabaseContextProvider(
            Func<CancellationToken, Task<CopilotDatabaseContextSnapshot?>> snapshotProvider,
            Func<bool>? isActive = null,
            Func<bool>? isCurrentSurface = null)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _isActive = isActive ?? (() => true);
            _isCurrentSurface = isCurrentSurface ?? (() => false);
        }

        public int Order => 30;

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

            return CopilotBusinessContextBuilder.BuildDatabaseContextItem(snapshot);
        }

        private bool ShouldCapture(CopilotContextRequest request)
        {
            if (request.Scope == CopilotContextScope.Diagnose || _isCurrentSurface())
                return true;

            var userText = request.UserText ?? string.Empty;
            return DatabaseIntentTerms.Any(term => userText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class CopilotDatabaseBrowserAgentExtension
    {
        public const string SourceId = "database-browser";

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
                SourceName = "Database Browser",
                SourceVersion = sourceVersion ?? string.Empty,
                ContextProviders = [contextProvider],
            });
        }
    }

    public sealed class CopilotDatabaseContextCoordinator
    {
        private readonly CopilotDynamicContextCoordinator<CopilotDatabaseContextSnapshot> _coordinator;

        public CopilotDatabaseContextCoordinator(CopilotAgentExtensionRegistry registry)
        {
            _coordinator = new CopilotDynamicContextCoordinator<CopilotDatabaseContextSnapshot>(
                registry,
                (snapshotProvider, isActive) => new CopilotDatabaseContextProvider(
                    snapshotProvider,
                    isActive,
                    IsCurrentSurface),
                CopilotDatabaseBrowserAgentExtension.Register);
        }

        public CopilotDynamicContextSession Register(
            Func<CancellationToken, Task<CopilotDatabaseContextSnapshot?>> snapshotProvider,
            string? sourceVersion = null)
        {
            return _coordinator.Register(snapshotProvider, sourceVersion);
        }

        private static bool IsCurrentSurface()
        {
            return string.Equals(
                CopilotLiveContextRegistry.Current?.SourceId,
                CopilotDatabaseBrowserAgentExtension.SourceId,
                StringComparison.Ordinal);
        }

    }

    internal static class CopilotDatabaseContextHub
    {
        public static CopilotDatabaseContextCoordinator Shared { get; } = new(CopilotAgentExtensionRegistry.Shared);
    }
}
