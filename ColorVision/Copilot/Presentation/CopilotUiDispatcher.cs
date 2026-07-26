using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    internal static class CopilotUiDispatcher
    {
        public static void Invoke(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            Invoke(Application.Current?.Dispatcher, () =>
            {
                action();
                return true;
            }, fallback: false);
        }

        public static T Invoke<T>(Func<T> action, T fallback)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Invoke(Application.Current?.Dispatcher, action, fallback);
        }

        public static async Task InvokeAsync(Action action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            await InvokeAsync(Application.Current?.Dispatcher, () =>
            {
                action();
                return true;
            }, cancellationToken).ConfigureAwait(false);
        }

        public static Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            return InvokeAsync(Application.Current?.Dispatcher, action, cancellationToken);
        }

        internal static T Invoke<T>(Dispatcher? dispatcher, Func<T> action, T fallback)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (dispatcher == null)
                return action();
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return fallback;
            if (dispatcher.CheckAccess())
                return action();

            try
            {
                return dispatcher.Invoke(action);
            }
            catch (TaskCanceledException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return fallback;
            }
            catch (InvalidOperationException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return fallback;
            }
        }

        internal static async Task<T> InvokeAsync<T>(
            Dispatcher? dispatcher,
            Func<T> action,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();
            if (dispatcher == null || dispatcher.CheckAccess())
                return action();
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                throw new OperationCanceledException("The Copilot UI is shutting down.", cancellationToken);

            var operation = dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken);
            return await operation.Task.ConfigureAwait(false);
        }
    }
}
