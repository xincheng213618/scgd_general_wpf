using System;
using System.Collections.Concurrent;
using System.Timers;
using System.Windows;

namespace ColorVision.Common.Utilities
{
    public static class DebounceTimer
    {
        private static readonly ConcurrentDictionary<string, Timer> _timers = new();

        public static void AddOrResetTimer<T,T1>(string actionType, int delayMilliseconds, Action<T,T1> action, T parameter, T1 parameter1)
        {
            ElapsedEventHandler handler = (s, e) => action(parameter, parameter1);
            AddOrResetTimer(actionType, delayMilliseconds, handler);
        }

        public static void AddOrResetTimer<T>(string actionType, int delayMilliseconds, Action<T> action, T parameter)
        {
            ElapsedEventHandler handler = (s, e) => action(parameter);
            AddOrResetTimer(actionType, delayMilliseconds, handler);
        }
        /// <summary>
        /// 匿名类型使用
        /// </summary>
        public static void AddOrResetTimer(string actionType, int delayMilliseconds, Action action)
        {
            ElapsedEventHandler handler = (s, e) => action();
            AddOrResetTimer(actionType, delayMilliseconds, handler);
        }

        public static void AddOrResetTimerDispatcher(string actionType, int delayMilliseconds, Action action)
        {
            ElapsedEventHandler handler = (s, e) => Application.Current?.Dispatcher.BeginInvoke(action);
            AddOrResetTimer(actionType, delayMilliseconds, handler);
        }




        /// <summary>
        /// Adds or resets a debounced event timer for a given action type.
        /// </summary>
        /// <param name="actionType">The identifier for the debounced action.</param>
        /// <param name="delayMilliseconds">The delay in milliseconds before the action is triggered.</param>
        /// <param name="action">The callback to invoke after the delay.</param>
        public static void AddOrResetTimer(string actionType, int delayMilliseconds, ElapsedEventHandler action)
        {
            // Stop and dispose the existing timer if it exists.
            if (_timers.TryRemove(actionType, out var existingTimer))
            {
                existingTimer.Stop();
                existingTimer.Dispose();
            }

            // Create a new timer and configure it.
            var timer = new Timer(delayMilliseconds)
            {
                AutoReset = false,
                Enabled = true
            };
            timer.Elapsed += (sender, args) =>
            {
                action(sender, args);
                // Dispose the timer after the action is triggered.
                if (_timers.TryRemove(actionType, out var elapsedTimer))
                {
                    elapsedTimer.Dispose();
                }
            };

            // Start the timer and add it to the dictionary.
            timer.Start();
            _timers.TryAdd(actionType, timer);
        }

        /// <summary>
        /// Disposes all timers and clears the dictionary.
        /// </summary>
        public static void DisposeAllTimers()
        {
            foreach (var timer in _timers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            _timers.Clear();
        }
    }
}
