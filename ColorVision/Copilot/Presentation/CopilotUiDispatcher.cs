using System;
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
    }
}
