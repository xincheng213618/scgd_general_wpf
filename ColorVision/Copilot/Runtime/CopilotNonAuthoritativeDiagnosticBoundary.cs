using System;
using System.Threading;

namespace ColorVision.Copilot
{
    internal static class CopilotNonAuthoritativeDiagnosticBoundary
    {
        private static long _containedFailureCount;

        public static long ContainedFailureCount =>
            Math.Max(0, Interlocked.Read(ref _containedFailureCount));

        public static bool TryWrite(Action write)
        {
            ArgumentNullException.ThrowIfNull(write);
            try
            {
                write();
                return true;
            }
            catch (Exception ex) when (!IsFatal(ex))
            {
                Interlocked.Increment(ref _containedFailureCount);
                return false;
            }
        }

        private static bool IsFatal(Exception exception) =>
            exception is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException;
    }
}
