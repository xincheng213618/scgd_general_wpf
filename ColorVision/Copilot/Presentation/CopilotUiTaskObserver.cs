using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public static class CopilotUiTaskObserver
    {
        public static void Run(Func<Task> operation, string operationName, Action<string>? onError = null)
        {
            _ = ObserveAsync(operation, operationName, onError);
        }

        public static Task ObserveAsync(Func<Task> operation, string operationName, Action<string>? onError = null)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("A UI operation name is required.", nameof(operationName));

            return ObserveCoreAsync(operation, operationName.Trim(), onError);
        }

        private static async Task ObserveCoreAsync(Func<Task> operation, string operationName, Action<string>? onError)
        {
            try
            {
                await operation();
            }
            catch (OperationCanceledException)
            {
                // User-initiated cancellation is a normal terminal state for Copilot UI operations.
            }
            catch (Exception exception)
            {
                var message = CopilotUserFacingErrorFormatter.Sanitize(exception.Message);
                Trace.TraceError($"Copilot UI operation '{operationName}' failed: {message}");
                try
                {
                    onError?.Invoke(message);
                }
                catch (Exception callbackException)
                {
                    Trace.TraceWarning($"Copilot UI error reporter failed: {CopilotUserFacingErrorFormatter.Sanitize(callbackException.Message)}");
                }
            }
        }
    }
}
