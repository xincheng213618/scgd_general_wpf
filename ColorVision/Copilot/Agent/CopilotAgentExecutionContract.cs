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
        DirectUrlEvidence,
        PublicWebSearch,
    }

    internal sealed class CopilotAgentExecutionContract
    {
        private readonly string[] _preferredToolNames;
        private readonly HashSet<string> _acceptedToolNames;

        private CopilotAgentExecutionContract(
            CopilotAgentExecutionRequirement requirement,
            IEnumerable<string> preferredToolNames)
        {
            Requirement = requirement;
            _preferredToolNames = preferredToolNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _acceptedToolNames = _preferredToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public CopilotAgentExecutionRequirement Requirement { get; }

        public bool IsRequired => Requirement != CopilotAgentExecutionRequirement.None && _preferredToolNames.Length > 0;

        public IReadOnlyList<string> AcceptedToolNames => _preferredToolNames;

        public string Description => Requirement switch
        {
            CopilotAgentExecutionRequirement.DirectUrlEvidence => "direct URL evidence",
            CopilotAgentExecutionRequirement.PublicWebSearch => "explicit public web search",
            _ => "no mandatory tool evidence",
        };

        public static CopilotAgentExecutionContract Create(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools)
        {
            ArgumentNullException.ThrowIfNull(request);
            availableTools ??= Array.Empty<ICopilotTool>();

            if (CopilotToolIntentPolicy.ExplicitlyDisallowsPublicWebAccess(request))
                return new CopilotAgentExecutionContract(CopilotAgentExecutionRequirement.None, Array.Empty<string>());

            var urlFetchTools = availableTools.Where(CopilotToolIntentPolicy.IsUrlFetchTool).Select(tool => tool.Name);
            var webSearchTools = availableTools.Where(CopilotToolIntentPolicy.IsPublicWebSearchTool).Select(tool => tool.Name);
            if (CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Count > 0)
            {
                return new CopilotAgentExecutionContract(
                    CopilotAgentExecutionRequirement.DirectUrlEvidence,
                    urlFetchTools.Concat(webSearchTools));
            }

            if (CopilotToolIntentPolicy.ExplicitlyRequiresPublicWebSearch(request))
            {
                return new CopilotAgentExecutionContract(
                    CopilotAgentExecutionRequirement.PublicWebSearch,
                    webSearchTools);
            }

            return new CopilotAgentExecutionContract(CopilotAgentExecutionRequirement.None, Array.Empty<string>());
        }

        public CopilotAgentExecutionContractEvaluation Evaluate(IReadOnlyList<CopilotAgentStepRecord> steps)
        {
            steps ??= Array.Empty<CopilotAgentStepRecord>();
            if (!IsRequired)
                return CopilotAgentExecutionContractEvaluation.NotRequired;

            var relevant = steps
                .Where(step => step != null && _acceptedToolNames.Contains(step.Execution.ToolName))
                .OrderBy(step => step.Round)
                .ToArray();
            var successful = relevant.LastOrDefault(step =>
                step.Observation.Success
                && step.Execution.State == CopilotToolExecutionState.Completed);
            if (successful != null)
            {
                return new CopilotAgentExecutionContractEvaluation
                {
                    IsRequired = true,
                    IsSatisfied = true,
                    LastRelevantStep = successful,
                };
            }

            var attemptedNames = relevant.Select(step => step.Execution.ToolName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var untriedNames = _preferredToolNames.Where(name => !attemptedNames.Contains(name)).ToArray();
            return new CopilotAgentExecutionContractEvaluation
            {
                IsRequired = true,
                IsSatisfied = false,
                ShouldReinvoke = untriedNames.Length > 0,
                Feedback = BuildFeedback(untriedNames),
                LastRelevantStep = relevant.LastOrDefault(),
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
                Code = Requirement == CopilotAgentExecutionRequirement.DirectUrlEvidence
                    ? "required_url_evidence_missing"
                    : "required_web_search_missing",
                Summary = step == null
                    ? "The model ended an explicit evidence request without calling an available matching tool."
                    : "The explicit evidence request ended without a successful matching tool result.",
                ToolName = step?.Execution.ToolName ?? string.Empty,
                SourceCallKey = step == null ? string.Empty : CopilotAgentTaskEventIds.ForCall(step.Execution.CallId),
                RetryEligible = step?.Execution.RetryEligible ?? false,
                RequiresUserInput = false,
            };
        }

        private string BuildFeedback(string[] untriedNames)
        {
            var preferred = untriedNames.Length > 0 ? untriedNames[0] : _preferredToolNames[0];
            return Requirement switch
            {
                CopilotAgentExecutionRequirement.DirectUrlEvidence =>
                    $"Execution contract: the user supplied a direct URL, but no successful URL evidence has been collected. Call the available {preferred} tool now and base the answer on its observation. If it fails, try another available web evidence tool only when useful; never claim the page was inspected without a successful result.",
                CopilotAgentExecutionRequirement.PublicWebSearch =>
                    $"Execution contract: the user explicitly requested a public web search, but no successful search evidence has been collected. Call the available {preferred} tool now and base the answer on its observation. If every available search path fails, report a concrete blocker instead of presenting unverified claims as searched results.",
                _ => string.Empty,
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
