using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows; // 用于 Application.Current.Dispatcher

namespace ColorVision.Common.Utilities
{


    public static class TaskConflator
    {
        // 用于存储每个 key 对应的执行器
        private static readonly ConcurrentDictionary<string, SerialExecutor> _executors = new();

        /// <summary>
        /// 添加或更新任务。
        /// 如果当前没有任务在运行，立即运行。
        /// 如果有任务在运行，将此任务标记为"待执行"（覆盖之前的待执行任务）。
        /// </summary>
        public static void RunOrUpdate(string key, Action action)
        {
            var executor = _executors.GetOrAdd(key, _ => new SerialExecutor());
            executor.Run(action);
        }

        /// <summary>
        /// 针对带参数 Action 的重载
        /// </summary>
        public static void RunOrUpdate<T>(string key, Action<T> action, T parameter)
        {
            RunOrUpdate(key, () => action(parameter));
        }

        /// <summary>
        /// 针对带两个参数 Action 的重载
        /// </summary>
        public static void RunOrUpdate<T, T1>(string key, Action<T, T1> action, T parameter, T1 parameter1)
        {
            RunOrUpdate(key, () => action(parameter, parameter1));
        }

        /// <summary>
        /// 专用于 UI 更新的重载（自动切回 Dispatcher）
        /// 注意：如果算法耗时，请不要直接把算法逻辑放进这里，而是应该把算法放在普通 Action 里，算法算完后的 UI 更新才用 Dispatcher。
        /// </summary>
        public static void RunOrUpdateDispatcher(string key, Action action)
        {
            RunOrUpdate(key, () =>
            {
                Application.Current?.Dispatcher.Invoke(action);
            });
        }

        /// <summary>
        /// 清理所有执行器（通常在页面关闭时调用）
        /// </summary>
        public static void DisposeAll()
        {
            _executors.Clear();
        }

        // 内部类：负责单个 Key 的串行合并逻辑
        private class SerialExecutor
        {
            private readonly object _lock = new();
            private bool _isRunning;
            private Action _pendingAction;

            public void Run(Action action)
            {
                lock (_lock)
                {
                    if (_isRunning)
                    {
                        // 逻辑核心：如果正在运行，更新 Pending Action 为最新的
                        // 这相当于"保留最后一次"，中间的会被丢弃
                        _pendingAction = action;
                        return;
                    }

                    // 如果没在运行，标记为运行，并开始处理
                    _isRunning = true;
                }

                // 在后台线程开始处理，避免阻塞调用方（如UI线程）
                Task.Run(() => ProcessLoop(action));
            }

            private void ProcessLoop(Action firstAction)
            {
                var currentAction = firstAction;

                while (true)
                {
                    try
                    {
                        // 执行耗时算法
                        currentAction?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        // 建议记录日志，防止异常导致循环崩溃
                        System.Diagnostics.Debug.WriteLine($"TaskConflator Error: {ex}");
                    }

                    lock (_lock)
                    {
                        // 当前任务执行完毕，检查有没有新的 Pending 任务
                        if (_pendingAction != null)
                        {
                            // 取出最新的任务作为下一次循环的目标
                            currentAction = _pendingAction;
                            _pendingAction = null; // 清空槽位
                        }
                        else
                        {
                            // 没有后续任务了，重置状态并退出循环
                            _isRunning = false;
                            return;
                        }
                    }
                }
            }
        }
    }
}
