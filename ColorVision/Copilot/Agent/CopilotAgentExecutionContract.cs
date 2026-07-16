#pragma warning disable MAAI001
using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotAgentExecutionRequirement
    {
        None,
        AttachedFileEvidence,
        GitReviewEvidence,
        DirectUrlEvidence,
        PublicWebSearch,
        WorkspaceEdit,
        WorkspaceEditAndValidation,
        WorkspaceCreate,
        WorkspaceCreateAndValidation,
        WorkspaceValidation,
        WorkspaceRollback,
    }

    internal sealed class CopilotAgentExecutionContract
    {
        private readonly string[] _preferredToolNames;
        private readonly HashSet<string> _acceptedToolNames;
        private readonly string[][] _requiredToolGroups;
        private readonly bool _requiresAttachedFileEvidence;

        private CopilotAgentExecutionContract(
            CopilotAgentExecutionRequirement requirement,
            IEnumerable<IEnumerable<string>> requiredToolGroups,
            bool requiresAttachedFileEvidence = false)
        {
            Requirement = requirement;
            _requiresAttachedFileEvidence = requiresAttachedFileEvidence;
            _requiredToolGroups = requiredToolGroups
                .Select(group => group
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray())
                .Where(group => group.Length > 0)
                .ToArray();
            _preferredToolNames = _requiredToolGroups.SelectMany(group => group).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _acceptedToolNames = _preferredToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            return "\n\nCurrent-turn execution contract: obtain successful tool evidence in this exact order before giving the final answer: "
                + string.Join(" -> ", orderedGroups)
                + ". Do not claim a step completed before its successful tool result. If an earlier required step fails, report the concrete blocker instead of continuing with a dependent action.";
        }

        public string Description
        {
            get
            {
                var requirementDescription = Requirement switch
                {
                    CopilotAgentExecutionRequirement.AttachedFileEvidence => "attached file evidence",
                    CopilotAgentExecutionRequirement.GitReviewEvidence => "Git working tree and diff evidence",
                    CopilotAgentExecutionRequirement.DirectUrlEvidence => "direct URL evidence",
                    CopilotAgentExecutionRequirement.PublicWebSearch => "explicit public web search",
                    CopilotAgentExecutionRequirement.WorkspaceEdit => "approved workspace edit",
                    CopilotAgentExecutionRequirement.WorkspaceEditAndValidation => "approved workspace edit followed by validation",
                    CopilotAgentExecutionRequirement.WorkspaceCreate => "approved workspace file creation",
                    CopilotAgentExecutionRequirement.WorkspaceCreateAndValidation => "approved workspace file creation followed by validation",
                    CopilotAgentExecutionRequirement.WorkspaceValidation => "approved workspace validation",
                    CopilotAgentExecutionRequirement.WorkspaceRollback => "approved workspace rollback",
                    _ => "no mandatory tool evidence",
                };
                return _requiresAttachedFileEvidence && Requirement != CopilotAgentExecutionRequirement.AttachedFileEvidence
                    ? $"attached file evidence followed by {requirementDescription}"
                    : requirementDescription;
            }
        }

        public static CopilotAgentExecutionContract Create(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools)
        {
            ArgumentNullException.ThrowIfNull(request);
            availableTools ??= Array.Empty<ICopilotTool>();

            var attachedFileReadTools = request.Attachments
                .Where(item => item?.Type == CopilotAttachmentType.File && !string.IsNullOrWhiteSpace(item.Value))
                .Any()
                    ? availableTools.Where(tool => string.Equals(tool.Name, "ReadAttachedFile", StringComparison.OrdinalIgnoreCase)).Select(tool => tool.Name).ToArray()
                    : Array.Empty<string>();
            var requiresAttachedFileEvidence = attachedFileReadTools.Length > 0;
            var explicitlyDisallowsPublicWebAccess = CopilotToolIntentPolicy.ExplicitlyDisallowsPublicWebAccess(request);

            var workspaceApplyTools = availableTools.Where(CopilotToolIntentPolicy.IsWorkspaceApplyTool).Select(tool => tool.Name);
            var workspaceValidationTools = availableTools.Where(CopilotToolIntentPolicy.IsWorkspaceValidationTool).Select(tool => tool.Name);
            var workspaceRollbackTools = availableTools.Where(CopilotToolIntentPolicy.IsWorkspaceRollbackTool).Select(tool => tool.Name);
            var needsValidation = CopilotToolIntentPolicy.NeedsWorkspaceValidation(request);
            if (CopilotToolIntentPolicy.NeedsWorkspaceRollback(request))
            {
                return Required(
                    CopilotAgentExecutionRequirement.WorkspaceRollback,
                    [workspaceRollbackTools],
                    attachedFileReadTools);
            }
            if (CopilotToolIntentPolicy.NeedsWorkspaceCreate(request))
            {
                return Required(
                    needsValidation
                        ? CopilotAgentExecutionRequirement.WorkspaceCreateAndValidation
                        : CopilotAgentExecutionRequirement.WorkspaceCreate,
                    needsValidation
                        ? [workspaceApplyTools, workspaceValidationTools]
                        : [workspaceApplyTools],
                    attachedFileReadTools);
            }
            if (CopilotToolIntentPolicy.NeedsWorkspaceEdit(request))
            {
                return Required(
                    needsValidation
                        ? CopilotAgentExecutionRequirement.WorkspaceEditAndValidation
                        : CopilotAgentExecutionRequirement.WorkspaceEdit,
                    needsValidation
                        ? [workspaceApplyTools, workspaceValidationTools]
                        : [workspaceApplyTools],
                    attachedFileReadTools);
            }
            if (needsValidation)
            {
                return Required(
                    CopilotAgentExecutionRequirement.WorkspaceValidation,
                    [workspaceValidationTools],
                    attachedFileReadTools);
            }

            if (request.Mode == CopilotAgentMode.Review)
            {
                var gitWorkingTreeTools = availableTools
                    .Where(tool => string.Equals(tool.Name, "InspectGitWorkingTree", StringComparison.OrdinalIgnoreCase))
                    .Select(tool => tool.Name)
                    .ToArray();
                var gitDiffTools = availableTools
                    .Where(tool => string.Equals(tool.Name, "InspectGitDiff", StringComparison.OrdinalIgnoreCase))
                    .Select(tool => tool.Name)
                    .ToArray();
                if (gitWorkingTreeTools.Length > 0 || gitDiffTools.Length > 0)
                {
                    return Required(
                        CopilotAgentExecutionRequirement.GitReviewEvidence,
                        [gitWorkingTreeTools, gitDiffTools],
                        attachedFileReadTools);
                }
            }

            var urlFetchTools = availableTools.Where(CopilotToolIntentPolicy.IsUrlFetchTool).Select(tool => tool.Name);
            var webSearchTools = availableTools.Where(CopilotToolIntentPolicy.IsPublicWebSearchTool).Select(tool => tool.Name);
            if (!explicitlyDisallowsPublicWebAccess && CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Count > 0)
            {
                return Required(
                    CopilotAgentExecutionRequirement.DirectUrlEvidence,
                    [urlFetchTools.Concat(webSearchTools)],
                    attachedFileReadTools);
            }

            if (!explicitlyDisallowsPublicWebAccess && CopilotToolIntentPolicy.ExplicitlyRequiresPublicWebSearch(request))
            {
                return Required(
                    CopilotAgentExecutionRequirement.PublicWebSearch,
                    [webSearchTools],
                    attachedFileReadTools);
            }

            return requiresAttachedFileEvidence
                ? Required(
                    CopilotAgentExecutionRequirement.AttachedFileEvidence,
                    Array.Empty<IEnumerable<string>>(),
                    attachedFileReadTools)
                : None();
        }

        private static CopilotAgentExecutionContract Required(
            CopilotAgentExecutionRequirement requirement,
            IEnumerable<IEnumerable<string>> requiredToolGroups,
            IReadOnlyList<string> attachedFileReadTools)
        {
            var requiresAttachedFileEvidence = attachedFileReadTools.Count > 0;
            var groups = requiresAttachedFileEvidence
                ? new[] { attachedFileReadTools.AsEnumerable() }.Concat(requiredToolGroups)
                : requiredToolGroups;
            return new CopilotAgentExecutionContract(requirement, groups, requiresAttachedFileEvidence);
        }

        private static CopilotAgentExecutionContract None() => new(
            CopilotAgentExecutionRequirement.None,
            Array.Empty<IEnumerable<string>>());

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
            foreach (var group in _requiredToolGroups)
            {
                var matchedIndex = Array.FindIndex(relevant, cursor + 1, step =>
                    group.Contains(step.Execution.ToolName, StringComparer.OrdinalIgnoreCase)
                    && IsAcceptedEvidence(step));
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
            return new CopilotAgentExecutionContractEvaluation
            {
                IsRequired = true,
                IsSatisfied = false,
                ShouldReinvoke = untriedNames.Length > 0,
                Feedback = BuildFeedback(missingGroup, untriedNames),
                LastRelevantStep = relevant.LastOrDefault(),
                MissingToolNames = missingGroup,
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

        private string BuildFeedback(string[] missingGroup, string[] untriedNames)
        {
            var preferred = untriedNames.Length > 0 ? untriedNames[0] : missingGroup[0];
            var usesPatchEnvelope = string.Equals(preferred, "ApplyWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase)
                || string.Equals(preferred, "RollbackWorkspacePatchEnvelope", StringComparison.OrdinalIgnoreCase);
            if (missingGroup.Contains("ReadAttachedFile", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: the user attached one or more files, but no successful attached-file evidence has been collected. Call ReadAttachedFile now before answering or taking a dependent action. Base subsequent claims on its observation; if the read fails, report a concrete blocker instead of claiming the file was inspected.";
            }
            if (missingGroup.Contains("InspectGitWorkingTree", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: Review mode requires current working-tree evidence before a code-review conclusion. Call InspectGitWorkingTree now and use its bounded status as evidence. If inspection fails or approval is denied, report the concrete blocker instead of claiming the repository state was inspected.";
            }
            if (missingGroup.Contains("InspectGitDiff", StringComparer.OrdinalIgnoreCase))
            {
                return "Execution contract: Review mode has not collected the relevant staged or unstaged patch. Call InspectGitDiff now after working-tree inspection, then base findings only on the returned bounded diff. If output_complete is false, disclose the bounded scope and do not infer that omitted changes are clean.";
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
                CopilotAgentExecutionRequirement.WorkspaceCreate =>
                    usesPatchEnvelope
                        ? "Execution contract: the requested workspace file creation has not completed. Call PreviewWorkspacePatchEnvelope once with the complete Add/Update/Delete operation list, then call ApplyWorkspacePatchEnvelope once with the returned changeSetId after native approval."
                        : $"Execution contract: the user explicitly requested a new workspace file, but no approved creation has completed. Call the available {preferred} tool and do not claim the file exists before it returns success.",
                CopilotAgentExecutionRequirement.WorkspaceCreateAndValidation =>
                    $"Execution contract: the requested file creation and validation are not both complete in order. Create the approved file first, then call RunWorkspaceValidation and base the answer on its reported outcome. The next untried required tool is {preferred}; never validate before creation or claim an unverified result.",
                CopilotAgentExecutionRequirement.WorkspaceValidation =>
                    $"Execution contract: the user explicitly requested workspace validation, but no approved validation result was collected. Call {preferred} with a workspace solution or project path and report its structured passed/failed outcome; do not claim a build or test was run without this result.",
                CopilotAgentExecutionRequirement.WorkspaceRollback =>
                    usesPatchEnvelope
                        ? "Execution contract: the requested workspace rollback has not completed. Call RollbackWorkspacePatchEnvelope once with the exact prior changeSetId so every Add/Update/Delete operation is restored as one guarded unit."
                        : $"Execution contract: the user explicitly requested a workspace rollback, but no approved rollback has completed. Call the available {preferred} tool and do not claim the rollback completed before it returns success.",
                _ => string.Empty,
            };
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

        private string GetMissingEvidenceCode(CopilotAgentExecutionContractEvaluation evaluation)
        {
            if (evaluation.MissingToolNames.Contains("ReadAttachedFile", StringComparer.OrdinalIgnoreCase))
                return "required_attachment_evidence_missing";
            if (evaluation.MissingToolNames.Any(name => string.Equals(name, "InspectGitWorkingTree", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "InspectGitDiff", StringComparison.OrdinalIgnoreCase)))
            {
                return "required_git_review_evidence_missing";
            }

            return Requirement switch
            {
                CopilotAgentExecutionRequirement.AttachedFileEvidence => "required_attachment_evidence_missing",
                CopilotAgentExecutionRequirement.GitReviewEvidence => "required_git_review_evidence_missing",
                CopilotAgentExecutionRequirement.DirectUrlEvidence => "required_url_evidence_missing",
                CopilotAgentExecutionRequirement.PublicWebSearch => "required_web_search_missing",
                CopilotAgentExecutionRequirement.WorkspaceEdit => "required_workspace_edit_missing",
                CopilotAgentExecutionRequirement.WorkspaceEditAndValidation => "required_workspace_edit_validation_missing",
                CopilotAgentExecutionRequirement.WorkspaceCreate => "required_workspace_create_missing",
                CopilotAgentExecutionRequirement.WorkspaceCreateAndValidation => "required_workspace_create_validation_missing",
                CopilotAgentExecutionRequirement.WorkspaceValidation => "required_workspace_validation_missing",
                CopilotAgentExecutionRequirement.WorkspaceRollback => "required_workspace_rollback_missing",
                _ => "required_tool_evidence_missing",
            };
        }
    }

    internal sealed class CopilotAgentExecutionContractEvaluation
    {
        public static CopilotAgentExecutionContractEvaluation NotRequired { get; } = new() { IsSatisfied = true };

        public bool IsRequired { get; init; }

        public bool IsSatisfied { get; init; }

        public bool ShouldReinvoke { get; init; }

        public string Feedback { get; init; } = string.Empty;

        public CopilotAgentStepRecord? LastRelevantStep { get; init; }

        public IReadOnlyList<string> MissingToolNames { get; init; } = Array.Empty<string>();
    }

    internal sealed class CopilotAgentExecutionContractLoopEvaluator : LoopEvaluator
    {
        private readonly CopilotAgentExecutionContract _contract;
        private readonly Func<IReadOnlyList<CopilotAgentStepRecord>> _getSteps;
        private readonly Action<string> _onReinvoke;

        public CopilotAgentExecutionContractLoopEvaluator(
            CopilotAgentExecutionContract contract,
            Func<IReadOnlyList<CopilotAgentStepRecord>> getSteps,
            Action<string> onReinvoke)
        {
            _contract = contract ?? throw new ArgumentNullException(nameof(contract));
            _getSteps = getSteps ?? throw new ArgumentNullException(nameof(getSteps));
            _onReinvoke = onReinvoke ?? throw new ArgumentNullException(nameof(onReinvoke));
        }

        public override ValueTask<LoopEvaluation> EvaluateAsync(LoopContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            var evaluation = _contract.Evaluate(_getSteps());
            if (!evaluation.ShouldReinvoke)
                return ValueTask.FromResult(LoopEvaluation.Stop());

            _onReinvoke(evaluation.Feedback);
            return ValueTask.FromResult(LoopEvaluation.Continue(evaluation.Feedback));
        }
    }
}
