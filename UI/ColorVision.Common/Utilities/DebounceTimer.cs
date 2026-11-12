using System;
using System.Timers;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Common.Utilities
{

    public static class DebounceTimer
    {
        private static readonly DebounceScheduler Scheduler = new();

        public static void AddOrResetTimer<T,T1>(string actionType, int delayMilliseconds, Action<T,T1> action, T parameter, T1 parameter1)
        {
            ArgumentNullException.ThrowIfNull(action);
            ElapsedEventHandler handler = (s, e) => action(parameter, parameter1);
            AddOrResetTimer(actionType, delayMilliseconds, handler);
        }

        public static void AddOrResetTimer<T>(string actionType, int delayMilliseconds, Action<T> action, T parameter)
        {
            ArgumentNullException.ThrowIfNull(action);
            ElapsedEventHandler handler = (s, e) => action(parameter);
            AddOrResetTimer(actionType, delayMilliseconds, handler);
        }
        /// <summary>
        /// 匿名类型使用
        /// </summary>
        public static void AddOrResetTimer(string actionType, int delayMilliseconds, Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ElapsedEventHandler handler = (s, e) => action();
            AddOrResetTimer(actionType, delayMilliseconds, handler);
        }

        public static void AddOrResetTimerDispatcher(string actionType, int delayMilliseconds, Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            Scheduler.AddOrReset(actionType, fire => new SystemDebounceTimer(delayMilliseconds, (sender, args) => action(), fire),
                (execute, abandon) => QueueOnDispatcher(Application.Current?.Dispatcher, execute, abandon));
        }

        /// <summary>
        /// Adds or resets a debounced event timer for a given action type.
        /// </summary>
        /// <param name="actionType">The identifier for the debounced action.</param>
        /// <param name="delayMilliseconds">The delay in milliseconds before the action is triggered.</param>
        /// <param name="action">The callback to invoke after the delay.</param>
        public static void AddOrResetTimer(string actionType, int delayMilliseconds, ElapsedEventHandler action)
        {
            ArgumentNullException.ThrowIfNull(action);
            Scheduler.AddOrReset(actionType, fire => new SystemDebounceTimer(delayMilliseconds, action, fire));
        }

        public static void Cancel(string actionType) => Scheduler.Cancel(actionType);

        internal static void QueueOnDispatcher(Dispatcher? dispatcher, Action execute, Action abandon)
        {
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                abandon();
                return;
            }
            DispatcherOperation operation = dispatcher.BeginInvoke(execute);
            operation.Aborted += (sender, args) => abandon();
            if (operation.Status == DispatcherOperationStatus.Aborted) abandon();
        }

        /// <summary>
        /// Disposes all timers and clears the dictionary.
        /// </summary>
        public static void DisposeAllTimers() => Scheduler.DisposeAll();

        private sealed class SystemDebounceTimer : IDebounceTimer
        {
            private readonly Timer _timer;

            internal SystemDebounceTimer(int delayMilliseconds, ElapsedEventHandler action, Action<Action> fire)
            {
                _timer = new Timer(delayMilliseconds) { AutoReset = false };
                _timer.Elapsed += (sender, args) => fire(() => action(sender, args));
            }

            public void Start() => _timer.Start();

            public void Dispose() => _timer.Dispose();
        }
    }
}
