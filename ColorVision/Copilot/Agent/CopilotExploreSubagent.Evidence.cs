using ColorVision.UI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSubagentRunner : ICopilotSubagentRunner
    {
        internal static bool CanUsePreselectedEvidence(
            CopilotAgentRequest? request,
            CopilotSubagentRoleDescriptor? role)
        {
            if (request == null
                || role == null
                || request.SessionCheckpoint != null
                || role.ContextScope != CopilotSubagentContextScope.WorkspaceReadOnly
                || !role.ReadCapabilities.HasFlag(CopilotSubagentReadCapabilities.ReadLocalFile)
                || !request.PreferBatchReadLocalFiles
                || request.ReadableLocalFilePaths.Count is < 2 or > MaximumPreselectedWorkspaceFiles
                || CopilotAgentRunBudget.ContainsExhaustiveScope(
                    string.IsNullOrWhiteSpace(request.TaskIntentText)
                        ? request.UserText
                        : request.TaskIntentText))
            {
                return false;
            }

            var selectedNames = request.ReadableLocalFilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var namedTaskFiles = NamedTaskFileRegex.Matches(request.UserText ?? string.Empty)
                .Select(match => match.Groups["name"].Value)
                .Where(IsLikelyNamedTaskFile)
                .Select(Path.GetFileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return selectedNames.Count == request.ReadableLocalFilePaths.Count
                && namedTaskFiles.Length == selectedNames.Count
                && namedTaskFiles.All(selectedNames.Contains);
        }

        internal static bool HasSuccessfulPreselectedEvidence(
            CopilotAgentRequest request,
            IReadOnlyList<CopilotAgentStepRecord> steps)
        {
            ArgumentNullException.ThrowIfNull(request);
            var expectedPaths = request.ReadableLocalFilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (expectedPaths.Count < 2)
                return false;

            var successfulReads = (steps ?? Array.Empty<CopilotAgentStepRecord>())
                .Where(step => step?.Observation?.Success == true
                    && string.Equals(step.ToolCall?.ToolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var successfullyReadPaths = successfulReads
                .SelectMany(step => step.Observation.SuccessfullyReadLocalFilePaths)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var scopedPaths = successfulReads
                .SelectMany(step => step.Observation.LocalFileReadScopes)
                .Where(scope => !string.IsNullOrWhiteSpace(scope?.Path))
                .Select(scope => Path.GetFullPath(scope.Path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return expectedPaths.All(path => successfullyReadPaths.Contains(path) && scopedPaths.Contains(path));
        }

        private static async Task<CopilotToolExecutionOutcome?> TryExecutePreselectedEvidenceAsync(
            CopilotAgentRequest request,
            CopilotSubagentRoleDescriptor role,
            IReadOnlyList<ICopilotTool> tools,
            CopilotToolExecutor toolExecutor,
            CancellationToken cancellationToken)
        {
            if (!CanUsePreselectedEvidence(request, role))
                return null;

            var readTool = tools.FirstOrDefault(tool =>
                string.Equals(tool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)
                && tool.CanHandle(request));
            if (readTool == null)
                return null;

            var toolInput = CopilotAgentToolInput.Empty;
            return await toolExecutor.ExecuteAsync(
                new CopilotToolInvocation
                {
                    CallId = $"preselected-{Guid.NewGuid():N}",
                    Round = 1,
                    RuntimeName = "subagent-preload",
                    Tool = readTool,
                    AgentRequest = request,
                    ToolInput = toolInput,
                    ToolCall = new CopilotToolCall
                    {
                        ToolName = readTool.Name,
                        ToolInput = toolInput,
                        Reason = "The host resolved every named task file and preloaded one bounded read-only evidence batch.",
                    },
                },
                _ => { },
                cancellationToken);
        }

        private static CopilotAgentRunResult CreatePreselectedEvidenceRunResult(
            CopilotAgentRequest request,
            CopilotToolExecutionOutcome outcome,
            TimeSpan elapsed)
        {
            var runBudget = CopilotAgentRunBudget.Resolve(request);
            return new CopilotAgentRunResult
            {
                StepRecords = [outcome.StepRecord],
                Usage = CopilotTokenUsage.Empty,
                Budget = runBudget.CreateSnapshot(
                    tokenSnapshot: null,
                    elapsed,
                    toolCalls: 1,
                    timeBudgetExhausted: false),
                StopReason = CopilotAgentStopReason.IncompleteOutput,
            };
        }

        private static bool IsLikelyNamedTaskFile(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var extension = Path.GetExtension(value);
            return extension.Length is > 1 and <= 12 && extension.Skip(1).Any(char.IsLetter);
        }

        internal static bool HasCompleteDeclaration(string? answer) =>
            !string.IsNullOrWhiteSpace(answer) && CompleteDeclarationRegex.IsMatch(answer);

        internal static string[] ResolveNamedTaskFiles(string? task, IEnumerable<string>? roots)
        {
            var normalizedRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots);
            if (string.IsNullOrWhiteSpace(task) || normalizedRoots.Count == 0)
                return Array.Empty<string>();

            var resolvedFiles = new List<string>();
            foreach (var explicitPath in CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(task))
            {
                if (File.Exists(explicitPath)
                    && CopilotWorkspaceSearchSupport.IsPathWithinRoots(explicitPath, normalizedRoots))
                {
                    resolvedFiles.Add(Path.GetFullPath(explicitPath));
                }
            }

            foreach (Match match in NamedTaskFileRegex.Matches(task))
            {
                var fileName = match.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(fileName)
                    || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var root in normalizedRoots)
                {
                    string candidate;
                    try
                    {
                        candidate = Path.GetFullPath(Path.Combine(root, fileName));
                    }
                    catch
                    {
                        continue;
                    }

                    if (!File.Exists(candidate)
                        || !CopilotWorkspaceSearchSupport.IsPathWithinRoots(candidate, normalizedRoots))
                    {
                        continue;
                    }

                    resolvedFiles.Add(candidate);
                    break;
                }
            }

            return resolvedFiles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaximumPreselectedWorkspaceFiles)
                .ToArray();
        }

        internal static int ResolveExplorationRequestTokenBudget(int totalTokenBudget)
        {
            var normalized = Math.Clamp(
                totalTokenBudget,
                CopilotAgentRunBudget.MinimumRequestTokenBudget,
                CopilotSubagentCoordinator.MaximumRunTokenBudget);
            return normalized < MinimumPhasedFinalizationTotalTokens
                ? normalized
                : normalized - PhasedFinalizationTokenReserve;
        }

    }
}
