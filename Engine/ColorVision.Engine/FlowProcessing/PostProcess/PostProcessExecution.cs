using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    public enum PostProcessFailurePolicy
    {
        Warning,
        Required
    }

    public enum PostProcessExecutionStatus
    {
        Succeeded,
        ReturnedFalse,
        ThrewException,
        Unavailable
    }

    public enum FlowFinalOutcome
    {
        Succeeded,
        SucceededWithWarnings,
        Failed,
        Canceled,
        TimedOut
    }

    public sealed class PostProcessExecutionResult
    {
        internal PostProcessExecutionResult(
            string name,
            string processTypeName,
            PostProcessFailurePolicy failurePolicy,
            PostProcessExecutionStatus status,
            string message,
            DateTime startedTimeUtc,
            DateTime completedTimeUtc,
            Exception? exception = null)
        {
            Name = name;
            ProcessTypeName = processTypeName;
            FailurePolicy = failurePolicy;
            Status = status;
            Message = message;
            StartedTimeUtc = startedTimeUtc;
            CompletedTimeUtc = completedTimeUtc;
            Exception = exception;
        }

        public string Name { get; }

        public string ProcessTypeName { get; }

        public PostProcessFailurePolicy FailurePolicy { get; }

        public PostProcessExecutionStatus Status { get; }

        public bool Succeeded => Status == PostProcessExecutionStatus.Succeeded;

        public string Message { get; }

        public DateTime StartedTimeUtc { get; }

        public DateTime CompletedTimeUtc { get; }

        public Exception? Exception { get; }
    }

    public sealed class FlowRunFinalizedData
    {
        public FlowRunFinalizedData(
            FlowControlData engineResult,
            FlowFinalOutcome finalOutcome,
            IReadOnlyList<PostProcessExecutionResult> postProcessResults,
            DateTime finalizedTimeUtc)
        {
            EngineResult = engineResult ?? throw new ArgumentNullException(nameof(engineResult));
            FinalOutcome = finalOutcome;
            PostProcessResults = postProcessResults?.ToArray()
                ?? throw new ArgumentNullException(nameof(postProcessResults));
            FinalizedTimeUtc = finalizedTimeUtc;
        }

        public FlowControlData EngineResult { get; }

        public FlowFinalOutcome FinalOutcome { get; }

        public IReadOnlyList<PostProcessExecutionResult> PostProcessResults { get; }

        public DateTime FinalizedTimeUtc { get; }
    }

    internal static class PostProcessExecutionRunner
    {
        public static IReadOnlyList<PostProcessExecutionResult> Execute(
            IEnumerable<PostProcessMeta> metas,
            PostProcessContext context)
        {
            ArgumentNullException.ThrowIfNull(metas);
            ArgumentNullException.ThrowIfNull(context);

            var results = new List<PostProcessExecutionResult>();
            foreach (PostProcessMeta meta in metas)
            {
                DateTime startedTimeUtc = DateTime.UtcNow;
                IPostProcessor? processor = meta.PostProcessor;
                if (processor == null)
                {
                    results.Add(new PostProcessExecutionResult(
                        meta.Name,
                        string.Empty,
                        meta.FailurePolicy,
                        PostProcessExecutionStatus.Unavailable,
                        "后处理器不可用。",
                        startedTimeUtc,
                        DateTime.UtcNow));
                    continue;
                }

                try
                {
                    bool succeeded = processor.Process(context);
                    results.Add(new PostProcessExecutionResult(
                        meta.Name,
                        processor.GetType().FullName ?? processor.GetType().Name,
                        meta.FailurePolicy,
                        succeeded
                            ? PostProcessExecutionStatus.Succeeded
                            : PostProcessExecutionStatus.ReturnedFalse,
                        succeeded ? string.Empty : "处理器返回 false。",
                        startedTimeUtc,
                        DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    results.Add(new PostProcessExecutionResult(
                        meta.Name,
                        processor.GetType().FullName ?? processor.GetType().Name,
                        meta.FailurePolicy,
                        PostProcessExecutionStatus.ThrewException,
                        ex.Message,
                        startedTimeUtc,
                        DateTime.UtcNow,
                        ex));
                }
            }

            return results;
        }
    }

    internal static class FlowFinalOutcomeResolver
    {
        public static FlowFinalOutcome Resolve(
            FlowControlData engineResult,
            IEnumerable<PostProcessExecutionResult> postProcessResults)
        {
            ArgumentNullException.ThrowIfNull(engineResult);
            ArgumentNullException.ThrowIfNull(postProcessResults);

            FlowFinalOutcome engineOutcome = engineResult.FlowStatus switch
            {
                FlowStatus.Completed => FlowFinalOutcome.Succeeded,
                FlowStatus.Canceled => FlowFinalOutcome.Canceled,
                FlowStatus.OverTime => FlowFinalOutcome.TimedOut,
                _ => FlowFinalOutcome.Failed
            };

            if (engineOutcome != FlowFinalOutcome.Succeeded)
                return engineOutcome;

            PostProcessExecutionResult[] failedResults = postProcessResults
                .Where(result => !result.Succeeded)
                .ToArray();
            if (failedResults.Any(result =>
                    result.FailurePolicy == PostProcessFailurePolicy.Required))
            {
                return FlowFinalOutcome.Failed;
            }
            return failedResults.Length > 0
                || engineResult.HandledFailures.Count > 0
                ? FlowFinalOutcome.SucceededWithWarnings
                : FlowFinalOutcome.Succeeded;
        }
    }
}
