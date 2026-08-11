using System;
using System.Collections.Generic;

namespace ColorVision
{
    internal readonly record struct ApplicationExitCleanupStep(string Name, Action Cleanup);
    internal readonly record struct ApplicationExitHandoffState(bool UpdateIsActive, bool ReplacementIsActive);

    internal static class ApplicationExitCleanup
    {
        public static void Run(
            IEnumerable<ApplicationExitCleanupStep> steps,
            Action<string, Exception> reportFailure)
        {
            foreach (ApplicationExitCleanupStep step in steps)
            {
                try
                {
                    step.Cleanup();
                }
                catch (Exception exception)
                {
                    try
                    {
                        reportFailure(step.Name, exception);
                    }
                    catch
                    {
                        // Exit cleanup must continue even when failure reporting is unavailable.
                    }
                }
            }
        }

        public static ApplicationExitHandoffState? RunSocketBeforePrefetchedUpdate(
            bool isSessionEnding,
            Func<ApplicationExitHandoffState> resolveHandoffState,
            Func<bool> shutdownSocket,
            Action applyPrefetchedUpdate,
            Action<string, Exception> reportFailure)
        {
            ApplicationExitHandoffState? handoffState = null;
            Run(
                [
                    new("update handoff state", () => handoffState = resolveHandoffState()),
                    new("socket server", () => _ = shutdownSocket()),
                    new("prefetched update", () =>
                    {
                        if (!isSessionEnding
                            && handoffState is { UpdateIsActive: false, ReplacementIsActive: false })
                            applyPrefetchedUpdate();
                    })
                ],
                reportFailure);
            return handoffState;
        }
    }
}
