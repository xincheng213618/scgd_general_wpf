using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Common.Utilities
{
    public static class TaskConflator
    {
        private static readonly ConcurrentDictionary<string, SerialExecutor> _executors = new();

        /// <summary>
        /// [同步版] 添加或更新任务。
        /// </summary>
        public static void RunOrUpdate(string key, Action action, int throttleDelayMs = 0)
        {
            // 将 Action 包装成返回 Task 的 Func，以便统一处理
            RunOrUpdate(key, () => { action(); return Task.CompletedTask; }, throttleDelayMs);
        }

        /// <summary>
        /// [异步版 - 推荐] 添加或更新异步任务。
        /// 这里的 func 应该返回一个 Task，Conflator 会等待这个 Task 完成后才开始下一次循环。
        /// </summary>
        public static void RunOrUpdate(string key, Func<Task> func, int throttleDelayMs = 0)
        {
            var executor = _executors.GetOrAdd(key, _ => new SerialExecutor());
            executor.Run(func, throttleDelayMs);
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void DisposeAll()
        {
            foreach (var executor in _executors.Values)
            {
                executor.Cancel();
            }
            _executors.Clear();
        }

        // 内部类：负责单个 Key 的串行合并逻辑
        private class SerialExecutor
        {
            private readonly object _lock = new();
            private bool _isRunning;
            private Func<Task> _pendingFunc;
            private CancellationTokenSource _cts = new();

            public void Run(Func<Task> func, int throttleDelayMs)
            {
                lock (_lock)
                {
                    if (_isRunning)
                    {
                        // 如果正在运行，覆盖 Pending 任务为最新的
                        _pendingFunc = func;
                        return;
                    }

                    // 标记为运行
                    _isRunning = true;
                }

                // 启动后台处理循环
                Task.Run(() => ProcessLoopAsync(func, throttleDelayMs, _cts.Token));
            }

            public void Cancel()
            {
                _cts.Cancel();
            }

            private async Task ProcessLoopAsync(Func<Task> firstFunc, int delayMs, CancellationToken token)
            {
                var currentFunc = firstFunc;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // 1. 执行任务，并【等待】它完成
                        // 这是解决你问题的关键：直到 await 完成前，不会进入下一次循环
                        if (currentFunc != null)
                        {
                            await currentFunc.Invoke();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"TaskConflator Error: {ex}");
                    }

                    // 2. (可选) 节流延时
                    // 给 CPU 一点喘息时间，对于 Slider 这种高频事件，建议设置 10-30ms
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, token);
                    }

                    lock (_lock)
                    {
                        // 3. 检查是否有新任务插队
                        if (_pendingFunc != null)
                        {
                            currentFunc = _pendingFunc;
                            _pendingFunc = null; // 清空槽位
                        }
                        else
                        {
                            // 没有待处理任务，结束循环
                            _isRunning = false;
                            return;
                        }
                    }
                }

                // 如果被 Cancel，确保重置运行状态
                lock (_lock) { _isRunning = false; }
            }
        }
    }
}