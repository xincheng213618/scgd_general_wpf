#pragma warning disable MAAI001
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal enum CopilotAgentExecutionRequirement
    {
        None,
        SubagentEvidence,
        AttachedFileEvidence,
        LocalFileEvidence,
        GitReviewEvidence,
        GitReviewAndWorkspaceValidation,
        DirectUrlEvidence,
        PublicWebSearch,
        WorkspaceEdit,
        WorkspaceEditAndValidation,
        WorkspaceEditAndShellExecution,
        WorkspaceEditAndShellExecutionAndValidation,
        WorkspaceCreate,
        WorkspaceCreateAndValidation,
        WorkspaceCreateAndShellExecution,
        WorkspaceCreateAndShellExecutionAndValidation,
        WorkspaceValidation,
        WorkspaceRollback,
        ShellExecution,
        BatchImageConversion,
        BatchImageProcessing,
    }

    internal sealed partial class CopilotAgentExecutionContract
    {
        private readonly string[] _preferredToolNames;
        private readonly HashSet<string> _acceptedToolNames;
        private readonly string[][] _requiredToolGroups;
        private readonly bool _requiresAttachedFileEvidence;
        private readonly bool _requiresLocalFileEvidence;
        private readonly string[] _requiredAttachedFilePaths;
        private readonly string[] _requiredLocalFilePaths;
        private readonly CopilotWorkspaceReviewTargetContext? _requiredWorkspaceReviewTarget;

        private CopilotAgentExecutionContract(
            CopilotAgentExecutionRequirement requirement,
            IEnumerable<IEnumerable<string>> requiredToolGroups,
            IEnumerable<string>? requiredAttachedFilePaths = null,
            IEnumerable<string>? requiredLocalFilePaths = null,
            CopilotWorkspaceReviewTargetContext? requiredWorkspaceReviewTarget = null)
        {
            Requirement = requirement;
            _requiredToolGroups = requiredToolGroups
                .Select(group => group
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray())
                .Where(group => group.Length > 0)
                .ToArray();
            _preferredToolNames = _requiredToolGroups.SelectMany(group => group).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _acceptedToolNames = _preferredToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _requiresAttachedFileEvidence = _acceptedToolNames.Contains("ReadAttachedFile");
            _requiresLocalFileEvidence = _acceptedToolNames.Contains("ReadLocalFile");
            _requiredAttachedFilePaths = _requiresAttachedFileEvidence
                ? (requiredAttachedFilePaths ?? Array.Empty<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : Array.Empty<string>();
            _requiredLocalFilePaths = _requiresLocalFileEvidence
                ? (requiredLocalFilePaths ?? Array.Empty<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : Array.Empty<string>();
            _requiredWorkspaceReviewTarget = requiredWorkspaceReviewTarget?.IsStructurallyValid() == true
                ? requiredWorkspaceReviewTarget.CreateSnapshot()
                : null;
        }

        public CopilotAgentExecutionRequirement Requirement { get; }

        public bool IsRequired => Requirement != CopilotAgentExecutionRequirement.None && _requiredToolGroups.Length > 0;

        public IReadOnlyList<string> AcceptedToolNames => _preferredToolNames;

        public string BuildInitialInstruction()
        {
            if (!IsRequired)
                return string.Empty;

            var orderedGroups = _requiredToolGroups.Select(group => group.Length == 1
                ? group[0]
                : $"({string.Join(" or ", group)})");
            var instruction = "\n\nCurrent-turn execution contract: obtain successful tool evidence in this exact order before giving the final answer: "
                + string.Join(" -> ", orderedGroups)
                + ". Do not claim a step completed before its successful tool result. If an earlier required step fails, report the concrete blocker instead of continuing with a dependent action.";
            if (_requiredAttachedFilePaths.Length > 0)
            {
                instruction += "\nReadAttachedFile evidence is complete only after successful results cover every current-turn file attachment. Start with its bounded batch form, then select any remaining path reported by the result:\n"
                    + BuildBoundedPathList(_requiredAttachedFilePaths);
            }
            if (_requiredLocalFilePaths.Length > 0)
            {
                instruction += "\nReadLocalFile evidence is complete only after successful results cover every explicit current-turn file. Use an exact path for any file not covered by the bounded batch result:\n"
                    + BuildBoundedPathList(_requiredLocalFilePaths);
            }
            if (_requiredWorkspaceReviewTarget != null)
                instruction += "\n" + BuildRequiredReviewTargetInstruction();
            return instruction;
        }

        public string Description
        {
            get
            {
                var requirementDescription = Requirement switch
                {
                    CopilotAgentExecutionRequirement.SubagentEvidence => "successful request-scoped evidence",
                    CopilotAgentExecutionRequirement.AttachedFileEvidence => "attached file evidence",
                    CopilotAgentExecutionRequirement.LocalFileEvidence => "explicit local file evidence",
                    CopilotAgentExecutionRequirement.GitReviewEvidence => "Git working tree and diff evidence",
                    CopilotAgentExecutionRequirement.GitReviewAndWorkspaceValidation => "Git working tree and diff evidence followed by approved workspace validation",
                    CopilotAgentExecutionRequirement.DirectUrlEvidence => "direct URL evidence",
                    CopilotAgentExecutionRequirement.PublicWebSearch => "explicit public web search",
                    CopilotAgentExecutionRequirement.WorkspaceEdit => "approved workspace edit",
                    CopilotAgentExecutionRequirement.WorkspaceEditAndValidation => "approved workspace edit followed by validation",
                    CopilotAgentExecutionRequirement.WorkspaceEditAndShellExecution => "approved workspace edit followed by command or script execution",
                    CopilotAgentExecutionRequirement.WorkspaceEditAndShellExecutionAndValidation => "approved workspace edit followed by command or script execution and validation",
                    CopilotAgentExecutionRequirement.WorkspaceCreate => "approved workspace file creation",
                    CopilotAgentExecutionRequirement.WorkspaceCreateAndValidation => "approved workspace file creation followed by validation",
                    CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecution => "approved workspace file creation followed by command or script execution",
                    CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecutionAndValidation => "approved workspace file creation followed by command or script execution and validation",
                    CopilotAgentExecutionRequirement.WorkspaceValidation => "approved workspace validation",
                    CopilotAgentExecutionRequirement.WorkspaceRollback => "approved workspace rollback",
                    CopilotAgentExecutionRequirement.ShellExecution => "approved command or script execution",
                    CopilotAgentExecutionRequirement.BatchImageConversion => "approved native batch image conversion",
                    CopilotAgentExecutionRequirement.BatchImageProcessing => "native batch image processor",
                    _ => "no mandatory tool evidence",
                };
                var prerequisites = new List<string>();
                if (_requiresAttachedFileEvidence && Requirement != CopilotAgentExecutionRequirement.AttachedFileEvidence)
                    prerequisites.Add("attached file evidence");
                if (_requiresLocalFileEvidence && Requirement != CopilotAgentExecutionRequirement.LocalFileEvidence)
                    prerequisites.Add("explicit local file evidence");
                prerequisites.Add(requirementDescription);
                return string.Join(" followed by ", prerequisites);
            }
        }

        public CopilotAgentExecutionContractEvaluation Evaluate(IReadOnlyList<CopilotAgentStepRecord> steps)
        {
            steps ??= Array.Empty<CopilotAgentStepRecord>();
            if (!IsRequired)
                return CopilotAgentExecutionContractEvaluation.NotRequired;

            var relevant = steps
                .Where(step => step != null && _acceptedToolNames.Contains(step.Execution.ToolName))
                .OrderBy(step => step.Round)
                .ThenBy(step => step.Execution.StartedAtUtc)
                .ToArray();
            var cursor = -1;
            string[]? missingGroup = null;
            string[] missingAttachedFilePaths = Array.Empty<string>();
            string[] missingLocalFilePaths = Array.Empty<string>();
            string[] attemptedFilePaths = Array.Empty<string>();
            foreach (var group in _requiredToolGroups)
            {
                if (TryEvaluateFileEvidenceGroup(group, relevant, cursor, out var fileEvidence))
                {
                    if (!fileEvidence.IsSatisfied)
                    {
                        missingGroup = group;
                        attemptedFilePaths = fileEvidence.AttemptedPaths;
                        if (string.Equals(fileEvidence.ToolName, "ReadAttachedFile", StringComparison.OrdinalIgnoreCase))
                            missingAttachedFilePaths = fileEvidence.MissingPaths;
                        else
                            missingLocalFilePaths = fileEvidence.MissingPaths;
                        break;
                    }

                    cursor = fileEvidence.LastMatchedIndex;
                    continue;
                }

                var matchedIndex = Array.FindIndex(relevant, cursor + 1, step =>
                    group.Contains(step.Execution.ToolName, StringComparer.OrdinalIgnoreCase)
                    && IsAcceptedGroupEvidence(group, step));
                if (matchedIndex < 0)
                {
                    missingGroup = group;
                    break;
                }
                cursor = matchedIndex;
            }
            if (missingGroup == null)
            {
                return new CopilotAgentExecutionContractEvaluation
                {
                    IsRequired = true,
                    IsSatisfied = true,
                    LastRelevantStep = cursor >= 0 ? relevant[cursor] : null,
                };
            }

            var attemptedAfterCursor = relevant.Skip(cursor + 1)
                .Select(step => step.Execution.ToolName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var untriedNames = missingGroup.Where(name => !attemptedAfterCursor.Contains(name)).ToArray();
            var latestMissingGroupAttempt = relevant
                .Skip(cursor + 1)
                .LastOrDefault(step => missingGroup.Contains(
                    step.Execution.ToolName,
                    StringComparer.OrdinalIgnoreCase));
            var canRetryLatestReadFailure = latestMissingGroupAttempt?.Execution is
            {
                RetryEligible: true,
                Access: CopilotToolAccess.ReadOnly,
                Idempotency: CopilotToolIdempotency.Idempotent,
                State: CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut,
            } retryExecution
                && retryExecution.Attempt >= 1
                && retryExecution.Attempt < retryExecution.MaxAttempts;
            var hasUnattemptedFilePath = missingAttachedFilePaths
                .Concat(missingLocalFilePaths)
                .Any(path => !attemptedFilePaths.Contains(path, StringComparer.OrdinalIgnoreCase));
            var reviewDiffAttempts = missingGroup.Contains("InspectGitDiff", StringComparer.OrdinalIgnoreCase)
                ? relevant.Skip(cursor + 1)
                    .Where(step => string.Equals(
                        step.Execution.ToolName,
                        "InspectGitDiff",
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray()
                : Array.Empty<CopilotAgentStepRecord>();
            var hasReviewTargetMismatch = reviewDiffAttempts.Any(step =>
                    IsAcceptedEvidence(step)
                    && !MatchesRequiredReviewTarget(step.ToolCall.ToolInput))
                && !reviewDiffAttempts.Any(step => MatchesRequiredReviewTarget(step.ToolCall.ToolInput));
            return new CopilotAgentExecutionContractEvaluation
            {
                IsRequired = true,
                IsSatisfied = false,
                ShouldReinvoke = untriedNames.Length > 0
                    || hasUnattemptedFilePath
                    || hasReviewTargetMismatch
                    || canRetryLatestReadFailure,
                Feedback = BuildFeedback(missingGroup, untriedNames, missingAttachedFilePaths, missingLocalFilePaths),
                LastRelevantStep = relevant.LastOrDefault(),
                MissingToolNames = missingGroup,
                MissingAttachedFilePaths = missingAttachedFilePaths,
                MissingLocalFilePaths = missingLocalFilePaths,
            };
        }

        public CopilotAgentBlockerSnapshot? CreateBlocker(CopilotAgentExecutionContractEvaluation evaluation)
        {
            if (!evaluation.IsRequired || evaluation.IsSatisfied)
                return null;

            var step = evaluation.LastRelevantStep;
            return new CopilotAgentBlockerSnapshot
            {
                Kind = step == null ? CopilotAgentBlockerKind.ProviderOutput : CopilotAgentBlockerKind.ToolFailure,
                Code = GetMissingEvidenceCode(evaluation),
                Summary = step == null
                    ? "The model ended an explicit evidence request without calling an available matching tool."
                    : "The explicit evidence request ended without a successful matching tool result.",
                ToolName = step?.Execution.ToolName ?? string.Empty,
                SourceCallKey = step == null ? string.Empty : CopilotAgentTaskEventIds.ForCall(step.Execution.CallId),
                RetryEligible = step?.Execution.RetryEligible ?? false,
                RequiresUserInput = false,
            };
        }

        private string BuildFeedback(
            string[] missingGroup,
            string[] untriedNames,
            IReadOnlyList<string> missingAttachedFilePaths,
            IReadOnlyList<string> missingLocalFilePaths)
        {
            var preferred = untriedNames.Length > 0 ? untriedNames[0] : missingGroup[0];
            var usesPatchEnvelope = string.Equals(preferred, "ApplyWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(preferred, "RollbackWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase);
            if (missingGroup.Contains("ReadAttachedFile", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: one or more current-turn file attachments have not been successfully read. Call ReadAttachedFile for every attachment still missing below before answering or taking a dependent action. You may omit path for the first bounded batch, then select remaining paths exactly. If a read fails, report the concrete blocker instead of claiming the file was inspected.\n"
                    + BuildBoundedPathList(missingAttachedFilePaths);
            }
            if (missingGroup.Contains("ReadLocalFile", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: one or more explicit current-turn local files have not been successfully read. Call ReadLocalFile separately with the exact path field for every file still missing below before answering or taking a dependent action. If a read fails, report the concrete blocker instead of claiming the file was inspected.\n"
                    + BuildBoundedPathList(missingLocalFilePaths);
            }
            if (missingGroup.Any(name => name.StartsWith("Delegate", StringComparison.OrdinalIgnoreCase)))
            {
                return $"Execution contract: the user explicitly required delegated evidence and disabled equivalent direct parent tools. Call {preferred} now and base the answer on its successful result. If delegation fails, report that concrete blocker instead of bypassing the user's tool-boundary instruction.";
            }
            if (missingGroup.Contains("InspectGitWorkingTree", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: Review mode requires current working-tree evidence before a code-review conclusion. Call InspectGitWorkingTree now and use its bounded status as evidence. If inspection fails or approval is denied, report the concrete blocker instead of claiming the repository state was inspected.";
            }
            if (missingGroup.Contains("InspectGitDiff", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: Review mode has not collected a successful Git patch for the exact structured target. "
                    + BuildRequiredReviewTargetInstruction()
                    + " Base findings only on that returned bounded diff. If output_complete is false, disclose the bounded scope and do not infer that omitted changes are clean.";
            }
            if (missingGroup.Contains("RunShellCommand", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: the user explicitly requested real command or script execution, but no successful process result was collected. Call RunShellCommand now after any required workspace write, use the exact working directory, and base the answer on its exit code, stdout, and stderr. Do not substitute a command suggestion or code block for execution.";
            }
            if (missingGroup.Contains(
                    "StartBackgroundShellCommand",
                    StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: the user explicitly requested a background command, but no approved application-managed process was started. Call StartBackgroundShellCommand now with the exact command and working directory. A successful start proves only that the process launched; inspect its bounded output or a specialized readiness signal before claiming that the service is ready.";
            }
            if (missingGroup.Contains("ConvertBatchImages", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: the user requested real native batch image conversion, but no successful conversion result was collected. Call ConvertBatchImages with the exact approved sources, output format, and destination. Base the answer on its succeeded/failed counts and output paths; do not substitute opening the batch window or merely describe how to convert.";
            }
            if (missingGroup.Contains("OpenBatchImageProcessing", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: the user requested native batch image conversion or processing, but the ColorVision batch processor was not opened. Call OpenBatchImageProcessing now, then explain the remaining review-and-start step without claiming that any files were converted yet.";
            }

            return Requirement switch
            {
                CopilotAgentExecutionRequirement.DirectUrlEvidence =>
                    $"Execution contract: the user supplied a direct URL, but no successful URL evidence has been collected. Call the available {preferred} tool now and base the answer on its observation. If it fails, try another available web evidence tool only when useful; never claim the page was inspected without a successful result.",
                CopilotAgentExecutionRequirement.PublicWebSearch =>
                    $"Execution contract: the user explicitly requested a public web search, but no successful search evidence has been collected. Call the available {preferred} tool now and base the answer on its observation. If every available search path fails, report a concrete blocker instead of presenting unverified claims as searched results.",
                CopilotAgentExecutionRequirement.WorkspaceEdit =>
                    usesPatchEnvelope
                        ? "Execution contract: the requested workspace edit has not completed. Call PreviewWorkspacePatchEnvelope once with the complete Add/Update/Delete operation list, inspect its bound file list and hashes, then call ApplyWorkspacePatchEnvelope once with the returned changeSetId. Never split the envelope into separately approved child writes."
                        : $"Execution contract: the user explicitly requested a workspace edit, but no approved edit has completed. Call the available {preferred} tool and do not claim the file changed before it returns success.",
                CopilotAgentExecutionRequirement.WorkspaceEditAndValidation =>
                    $"Execution contract: the requested workspace edit and validation are not both complete in order. Apply the approved workspace patch envelope first, then call RunWorkspaceValidation and base the answer on its reported outcome. The next untried required tool is {preferred}; never validate before the write or claim an unverified result.",
                CopilotAgentExecutionRequirement.WorkspaceEditAndShellExecution =>
                    $"Execution contract: the requested workspace edit and command execution are not both complete in order. Apply the approved workspace patch envelope first, then call {preferred}. Never claim that the changed code ran without a successful process result.",
                CopilotAgentExecutionRequirement.WorkspaceEditAndShellExecutionAndValidation =>
                    $"Execution contract: the requested workspace edit, command execution, and validation are not complete in order. Apply the patch first, run the requested command or script second, then call RunWorkspaceValidation. The next untried required tool is {preferred}.",
                CopilotAgentExecutionRequirement.WorkspaceCreate =>
                    usesPatchEnvelope
                        ? "Execution contract: the requested workspace file creation has not completed. Call PreviewWorkspacePatchEnvelope once with the complete Add/Update/Delete operation list, then call ApplyWorkspacePatchEnvelope once with the returned changeSetId after native approval."
                        : $"Execution contract: the user explicitly requested a new workspace file, but no approved creation has completed. Call the available {preferred} tool and do not claim the file exists before it returns success.",
                CopilotAgentExecutionRequirement.WorkspaceCreateAndValidation =>
                    $"Execution contract: the requested file creation and validation are not both complete in order. Create the approved file first, then call RunWorkspaceValidation and base the answer on its reported outcome. The next untried required tool is {preferred}; never validate before creation or claim an unverified result.",
                CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecution =>
                    $"Execution contract: the requested script or file creation and execution are not both complete in order. Create the approved file first, then call {preferred} from its exact working directory; never claim the new file ran without a successful process result.",
                CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecutionAndValidation =>
                    $"Execution contract: the requested file creation, command execution, and validation are not complete in order. Create the file first, run it second, then call RunWorkspaceValidation. The next untried required tool is {preferred}.",
                CopilotAgentExecutionRequirement.WorkspaceValidation =>
                    $"Execution contract: the user explicitly requested workspace validation, but no approved validation result was collected. Call {preferred} with a workspace solution or project path and report its structured passed/failed outcome; do not claim a build or test was run without this result.",
                CopilotAgentExecutionRequirement.GitReviewAndWorkspaceValidation =>
                    $"Execution contract: verification requires current Git working-tree and diff evidence followed by an approved bounded build or test. Call {preferred} for the next missing step and end with PASS only after every required result succeeds; never modify files or claim uncollected validation.",
                CopilotAgentExecutionRequirement.WorkspaceRollback =>
                    usesPatchEnvelope
                        ? "Execution contract: the requested workspace rollback has not completed. Call RollbackWorkspacePatchEnvelope once with the exact prior changeSetId so every Add/Update/Delete operation is restored as one guarded unit."
                        : $"Execution contract: the user explicitly requested a workspace rollback, but no approved rollback has completed. Call the available {preferred} tool and do not claim the rollback completed before it returns success.",
                CopilotAgentExecutionRequirement.ShellExecution =>
                    $"Execution contract: the user explicitly requested command or script execution, but no successful process result was collected. Call {preferred} and report its actual exit code and output; do not replace execution with instructions.",
                CopilotAgentExecutionRequirement.BatchImageConversion =>
                    $"Execution contract: the user explicitly requested native batch image conversion. Call {preferred} after approval and report its actual per-file output evidence; do not claim conversion completed from a preview or an opened window.",
                CopilotAgentExecutionRequirement.BatchImageProcessing =>
                    $"Execution contract: the user explicitly requested native batch image conversion or processing. Call {preferred}, then tell the user to review inputs and output settings before starting; do not claim conversion completed merely because the window opened.",
                _ => string.Empty,
            };
        }

        private bool TryEvaluateFileEvidenceGroup(
            string[] group,
            CopilotAgentStepRecord[] relevant,
            int cursor,
            out FileEvidenceGroupEvaluation evaluation)
        {
            var toolName = string.Empty;
            string[] requiredPaths = Array.Empty<string>();
            if (_requiredAttachedFilePaths.Length > 0
                && group.Contains("ReadAttachedFile", StringComparer.OrdinalIgnoreCase))
            {
                toolName = "ReadAttachedFile";
                requiredPaths = _requiredAttachedFilePaths;
            }
            else if (_requiredLocalFilePaths.Length > 0
                && group.Contains("ReadLocalFile", StringComparer.OrdinalIgnoreCase))
            {
                toolName = "ReadLocalFile";
                requiredPaths = _requiredLocalFilePaths;
            }
            else
            {
                evaluation = default;
                return false;
            }

            var attemptedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var successfulPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lastMatchedIndex = -1;
            for (var index = cursor + 1; index < relevant.Length; index++)
            {
                var step = relevant[index];
                if (!string.Equals(step.Execution.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var path in step.Observation.AttemptedLocalFilePaths ?? Array.Empty<string>())
                {
                    var normalizedPath = NormalizePath(path);
                    if (!string.IsNullOrWhiteSpace(normalizedPath))
                        attemptedPaths.Add(normalizedPath);
                }

                if (!IsAcceptedEvidence(step))
                    continue;

                var successfulPathCount = successfulPaths.Count;
                foreach (var path in step.Observation.SuccessfullyReadLocalFilePaths ?? Array.Empty<string>())
                {
                    var normalizedPath = NormalizePath(path);
                    if (requiredPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
                        successfulPaths.Add(normalizedPath);
                }
                if (successfulPaths.Count > successfulPathCount)
                    lastMatchedIndex = index;
                if (successfulPaths.Count == requiredPaths.Length)
                    break;
            }

            var missingPaths = requiredPaths
                .Where(path => !successfulPaths.Contains(path))
                .ToArray();
            evaluation = new FileEvidenceGroupEvaluation(
                toolName,
                missingPaths.Length == 0,
                lastMatchedIndex,
                missingPaths,
                attemptedPaths.ToArray());
            return true;
        }

        private static string BuildBoundedPathList(IEnumerable<string> paths)
        {
            const int maximumPaths = 8;
            var availablePaths = (paths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var lines = availablePaths.Take(maximumPaths).Select(path => $"- {path}").ToList();
            if (availablePaths.Length > maximumPaths)
                lines.Add($"- ... {availablePaths.Length - maximumPaths} additional explicit path(s) remain; continue from the original request after these are read.");
            return string.Join("\n", lines);
        }

        private static string NormalizeExistingFilePath(string? path)
        {
            var normalized = NormalizePath(path);
            return !string.IsNullOrWhiteSpace(normalized) && File.Exists(normalized)
                ? normalized
                : string.Empty;
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsAcceptedEvidence(CopilotAgentStepRecord step)
        {
            if (step.Observation.Success && step.Execution.State == CopilotToolExecutionState.Completed)
                return true;

            return string.Equals(step.Execution.ToolName, "RunWorkspaceValidation", StringComparison.OrdinalIgnoreCase)
                && step.Execution.State == CopilotToolExecutionState.Failed
                && step.Observation.FailureKind == CopilotToolFailureKind.Validation
                && string.Equals(
                    CopilotToolFailureCode.Normalize(step.Observation.FailureCode),
                    CopilotWorkspaceValidationService.ValidationFailedFailureCode,
                    StringComparison.Ordinal);
        }

        private bool IsAcceptedGroupEvidence(string[] group, CopilotAgentStepRecord step)
        {
            if (!IsAcceptedEvidence(step))
                return false;
            return !group.Contains("InspectGitDiff", StringComparer.OrdinalIgnoreCase)
                || !string.Equals(step.Execution.ToolName, "InspectGitDiff", StringComparison.OrdinalIgnoreCase)
                || MatchesRequiredReviewTarget(step.ToolCall.ToolInput);
        }

        private bool MatchesRequiredReviewTarget(CopilotAgentToolInput? input)
        {
            if (_requiredWorkspaceReviewTarget == null)
                return true;

            input ??= CopilotAgentToolInput.Empty;
            var target = ReadStringArgument(input, "target");
            var revision = ReadStringArgument(input, "revision");
            var scope = ReadStringArgument(input, "scope");
            return _requiredWorkspaceReviewTarget.Target switch
            {
                CopilotWorkspaceReviewTarget.BaseBranch =>
                    string.Equals(target, "base_branch", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(revision, _requiredWorkspaceReviewTarget.Revision, StringComparison.Ordinal)
                    && !HasArgument(input, "scope"),
                CopilotWorkspaceReviewTarget.Commit =>
                    string.Equals(target, "commit", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(revision, _requiredWorkspaceReviewTarget.Revision, StringComparison.OrdinalIgnoreCase)
                    && !HasArgument(input, "scope"),
                _ =>
                    string.Equals(target, "working_tree", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(scope, "both", StringComparison.OrdinalIgnoreCase)
                    && !HasArgument(input, "revision"),
            };
        }

        private string BuildRequiredReviewTargetInstruction()
        {
            if (_requiredWorkspaceReviewTarget == null)
                return "Call InspectGitDiff with the target named by the request.";

            return _requiredWorkspaceReviewTarget.Target switch
            {
                CopilotWorkspaceReviewTarget.BaseBranch =>
                    "Call InspectGitDiff with exactly target=\"base_branch\" and revision="
                    + JsonSerializer.Serialize(_requiredWorkspaceReviewTarget.Revision)
                    + "; omit scope.",
                CopilotWorkspaceReviewTarget.Commit =>
                    "Call InspectGitDiff with exactly target=\"commit\" and revision="
                    + JsonSerializer.Serialize(_requiredWorkspaceReviewTarget.Revision)
                    + "; omit scope.",
                _ =>
                    "Call InspectGitDiff with exactly target=\"working_tree\" and scope=\"both\"; omit revision.",
            };
        }

        private static bool HasArgument(CopilotAgentToolInput input, string name) =>
            input.Arguments.Keys.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

        private static string ReadStringArgument(CopilotAgentToolInput input, string name)
        {
            var pair = input.Arguments.FirstOrDefault(argument =>
                string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            return pair.Value switch
            {
                string text => text.Trim(),
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()?.Trim() ?? string.Empty,
                _ => string.Empty,
            };
        }

        private string GetMissingEvidenceCode(CopilotAgentExecutionContractEvaluation evaluation)
        {
            if (evaluation.MissingToolNames.Contains("ReadAttachedFile", StringComparer.OrdinalIgnoreCase))
                return "required_attachment_evidence_missing";
            if (evaluation.MissingToolNames.Contains("ReadLocalFile", StringComparer.OrdinalIgnoreCase))
                return "required_local_file_evidence_missing";
            if (evaluation.MissingToolNames.Any(name => string.Equals(name, "InspectGitWorkingTree", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "InspectGitDiff", StringComparison.OrdinalIgnoreCase)))
            {
                return "required_git_review_evidence_missing";
            }
            if (evaluation.MissingToolNames.Any(name => name.StartsWith("Delegate", StringComparison.OrdinalIgnoreCase)))
                return "required_delegated_evidence_missing";

            return Requirement switch
            {
                CopilotAgentExecutionRequirement.AttachedFileEvidence => "required_attachment_evidence_missing",
                CopilotAgentExecutionRequirement.LocalFileEvidence => "required_local_file_evidence_missing",
                CopilotAgentExecutionRequirement.GitReviewEvidence => "required_git_review_evidence_missing",
                CopilotAgentExecutionRequirement.DirectUrlEvidence => "required_url_evidence_missing",
                CopilotAgentExecutionRequirement.PublicWebSearch => "required_web_search_missing",
                CopilotAgentExecutionRequirement.WorkspaceEdit => "required_workspace_edit_missing",
                CopilotAgentExecutionRequirement.WorkspaceEditAndValidation => "required_workspace_edit_validation_missing",
                CopilotAgentExecutionRequirement.WorkspaceEditAndShellExecution => "required_workspace_edit_shell_execution_missing",
                CopilotAgentExecutionRequirement.WorkspaceEditAndShellExecutionAndValidation => "required_workspace_edit_shell_execution_validation_missing",
                CopilotAgentExecutionRequirement.WorkspaceCreate => "required_workspace_create_missing",
                CopilotAgentExecutionRequirement.WorkspaceCreateAndValidation => "required_workspace_create_validation_missing",
                CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecution => "required_workspace_create_shell_execution_missing",
                CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecutionAndValidation => "required_workspace_create_shell_execution_validation_missing",
                CopilotAgentExecutionRequirement.WorkspaceValidation => "required_workspace_validation_missing",
                CopilotAgentExecutionRequirement.WorkspaceRollback => "required_workspace_rollback_missing",
                CopilotAgentExecutionRequirement.ShellExecution => "required_shell_execution_missing",
                CopilotAgentExecutionRequirement.BatchImageConversion => "required_batch_image_conversion_missing",
                CopilotAgentExecutionRequirement.BatchImageProcessing => "required_batch_image_processing_missing",
                _ => "required_tool_evidence_missing",
            };
        }

        private readonly record struct FileEvidenceGroupEvaluation(
            string ToolName,
            bool IsSatisfied,
            int LastMatchedIndex,
            string[] MissingPaths,
            string[] AttemptedPaths);
    }

}
