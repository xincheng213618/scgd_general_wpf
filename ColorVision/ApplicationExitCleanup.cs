using System;
using System.Collections.Generic;

namespace ColorVision
{
    internal readonly record struct ApplicationExitCleanupStep(string Name, Action Cleanup);

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
    }
}
