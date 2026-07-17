using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using System.Globalization;

namespace ProjectARVRPro
{
    public sealed class ProjectARVRCopilotContextProvider : ICopilotContextProvider
    {
        private static readonly string[] IntentTerms =
        [
            "arvr", "arvrpro", "project arvr", "arvr result", "objective test", "module inspection", "project result",
            "模组检测", "模组结果", "ARVR结果", "客观测试", "项目结果", "测试结果", "检测结果",
        ];
        private readonly Func<CancellationToken, Task<CopilotProjectResultContextSnapshot?>> _snapshotProvider;
        private readonly Func<bool> _isActive;
        private readonly Func<bool> _isCurrentSurface;

        public ProjectARVRCopilotContextProvider(
            Func<CancellationToken, Task<CopilotProjectResultContextSnapshot?>> snapshotProvider,
            Func<bool>? isActive = null,
            Func<bool>? isCurrentSurface = null)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _isActive = isActive ?? (() => true);
            _isCurrentSurface = isCurrentSurface ?? (() => false);
        }

        public int Order => 35;

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

            return CopilotBusinessContextBuilder.BuildProjectResultContextItem(snapshot);
        }

        private bool ShouldCapture(CopilotContextRequest request)
        {
            if (request.Scope == CopilotContextScope.Diagnose || _isCurrentSurface())
                return true;

            var userText = request.UserText ?? string.Empty;
            return IntentTerms.Any(term => userText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class ProjectARVRCopilotAgentExtension
    {
        public const string SourceId = "project-arvr-pro-results";

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
                SourceName = "Project ARVRPro Results",
                SourceVersion = sourceVersion ?? string.Empty,
                ContextProviders = [contextProvider],
            });
        }
    }

    internal static class ProjectARVRCopilotContextHub
    {
        public static CopilotDynamicContextCoordinator<CopilotProjectResultContextSnapshot> Shared { get; } = new(
            CopilotAgentExtensionRegistry.Shared,
            (snapshotProvider, isActive) => new ProjectARVRCopilotContextProvider(
                snapshotProvider,
                isActive,
                IsCurrentSurface),
            ProjectARVRCopilotAgentExtension.Register);

        private static bool IsCurrentSurface()
        {
            return string.Equals(
                CopilotLiveContextRegistry.Current?.SourceId,
                ProjectARVRCopilotAgentExtension.SourceId,
                StringComparison.Ordinal);
        }
    }

    public static class ProjectARVRCopilotSnapshotFactory
    {
        private const int MaxFailedItemNames = 20;

        public static CopilotProjectResultContextSnapshot CreateForResultList(
            string surface,
            IEnumerable<ProjectARVRReuslt> results,
            ProjectARVRReuslt? selectedResult)
        {
            ArgumentNullException.ThrowIfNull(results);
            var loaded = results.Where(result => result != null).ToArray();
            var testItems = TryCollectTestItems(selectedResult?.ViewResultJson);
            return CreateSnapshot(surface, loaded, selectedResult, testItems);
        }

        public static CopilotProjectResultContextSnapshot CreateForTestItems(
            string surface,
            IEnumerable<ObjectiveTestItem> testItems)
        {
            ArgumentNullException.ThrowIfNull(testItems);
            return CreateSnapshot(surface, Array.Empty<ProjectARVRReuslt>(), null, testItems.Where(item => item != null).ToArray());
        }

        public static CopilotProjectResultContextSnapshot CreateForObjectiveResultRecords(
            string surface,
            IEnumerable<ObjectiveTestResultRecord> records,
            ObjectiveTestResultRecord? selectedRecord)
        {
            ArgumentNullException.ThrowIfNull(records);
            var loaded = records.Where(record => record != null).ToArray();
            var testItems = TryCollectTestItems(selectedRecord?.ObjectiveTestResultJson);
            var failedItems = testItems.Where(item => !item.TestResult).ToArray();
            var failedNames = BuildFailedItemNames(failedItems);

            return new CopilotProjectResultContextSnapshot
            {
                SourceId = ProjectARVRCopilotAgentExtension.SourceId,
                ProjectName = "ARVRPro",
                Surface = surface,
                LoadedResultCount = loaded.Length,
                CompletedResultCount = loaded.Length,
                PassedResultCount = loaded.Count(record => record.TotalResult),
                FailedResultCount = loaded.Count(record => !record.TotalResult),
                HasSelectedResult = selectedRecord != null,
                SelectedResultId = selectedRecord?.ResultId > 0 ? selectedRecord.ResultId : null,
                SelectedBatchId = selectedRecord?.BatchId > 0 ? selectedRecord.BatchId : null,
                SelectedProcessName = selectedRecord?.LastModel ?? string.Empty,
                SelectedStatus = selectedRecord?.LastFlowStatus ?? string.Empty,
                SelectedPassed = selectedRecord?.TotalResult,
                SelectedCreatedAt = selectedRecord?.UpdateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                SelectedHasMessage = !string.IsNullOrWhiteSpace(selectedRecord?.Msg),
                SelectedHasStructuredPayload = !string.IsNullOrWhiteSpace(selectedRecord?.ObjectiveTestResultJson),
                HasTestDetails = testItems.Count > 0,
                TestItemCount = testItems.Count,
                PassedTestItemCount = testItems.Count - failedItems.Length,
                FailedTestItemCount = failedItems.Length,
                FailedTestItemNames = failedNames,
                IsFailedTestItemListTruncated = failedItems.Length > failedNames.Length,
            };
        }

        private static CopilotProjectResultContextSnapshot CreateSnapshot(
            string surface,
            ProjectARVRReuslt[] loaded,
            ProjectARVRReuslt? selectedResult,
            IReadOnlyList<ObjectiveTestItem> testItems)
        {
            var failedItems = testItems.Where(item => !item.TestResult).ToArray();
            var failedNames = BuildFailedItemNames(failedItems);
            var completed = loaded.Count(result => result.FlowStatus == FlowStatus.Completed);
            var passed = loaded.Count(result => result.FlowStatus == FlowStatus.Completed && result.Result);
            var running = loaded.Count(result => result.FlowStatus == FlowStatus.Runing);

            return new CopilotProjectResultContextSnapshot
            {
                SourceId = ProjectARVRCopilotAgentExtension.SourceId,
                ProjectName = "ARVRPro",
                Surface = surface,
                LoadedResultCount = loaded.Length,
                RunningResultCount = running,
                CompletedResultCount = completed,
                PassedResultCount = passed,
                FailedResultCount = loaded.Length - running - passed,
                HasSelectedResult = selectedResult != null,
                SelectedResultId = selectedResult?.Id > 0 ? selectedResult.Id : null,
                SelectedBatchId = selectedResult?.BatchId > 0 ? selectedResult.BatchId : null,
                SelectedProcessName = selectedResult?.Model ?? string.Empty,
                SelectedStatus = selectedResult?.FlowStatus.ToString() ?? string.Empty,
                SelectedPassed = selectedResult?.FlowStatus == FlowStatus.Completed ? selectedResult.Result : null,
                SelectedDurationMilliseconds = selectedResult?.RunTime ?? 0,
                SelectedCreatedAt = selectedResult?.CreateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                SelectedHasImageReference = !string.IsNullOrWhiteSpace(selectedResult?.FileName),
                SelectedHasMessage = !string.IsNullOrWhiteSpace(selectedResult?.Msg),
                SelectedHasStructuredPayload = !string.IsNullOrWhiteSpace(selectedResult?.ViewResultJson),
                HasTestDetails = testItems.Count > 0,
                TestItemCount = testItems.Count,
                PassedTestItemCount = testItems.Count - failedItems.Length,
                FailedTestItemCount = failedItems.Length,
                FailedTestItemNames = failedNames,
                IsFailedTestItemListTruncated = failedItems.Length > failedNames.Length,
            };
        }

        private static string[] BuildFailedItemNames(IEnumerable<ObjectiveTestItem> failedItems)
        {
            return failedItems
                .Select(item => item.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Take(MaxFailedItemNames)
                .ToArray();
        }

        private static IReadOnlyList<ObjectiveTestItem> TryCollectTestItems(string? json)
        {
            try
            {
                return ObjectiveTestItemCollector.CollectFromJson(json);
            }
            catch
            {
                return Array.Empty<ObjectiveTestItem>();
            }
        }
    }
}
