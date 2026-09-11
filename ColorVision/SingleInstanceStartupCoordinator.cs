using log4net;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision;

internal enum SingleInstanceStartupChoice { ForceClose, OpenAdditional, Cancel }
internal enum SingleInstanceStartupResult { ClosedEarlierInstances, OpenAdditional, Cancel }

internal sealed class SingleInstanceStartupCoordinator
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(SingleInstanceStartupCoordinator));
    private readonly Func<CancellationToken, Task> _forceCloseAsync;
    private CancellationTokenSource? _attemptCancellation;
    private TaskCompletionSource<SingleInstanceStartupChoice> _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _finished;

    public event Action? StateChanged;
    public bool IsForcing { get; private set; }
    public string Status { get; private set; } = "正在关闭旧程序";
    public string Detail { get; private set; } = string.Empty;

    public SingleInstanceStartupCoordinator(Func<CancellationToken, Task> forceCloseAsync) => _forceCloseAsync = forceCloseAsync;

    public void Choose(SingleInstanceStartupChoice choice)
    {
        if (_finished || (IsForcing && choice == SingleInstanceStartupChoice.ForceClose) || !_choice.TrySetResult(choice))
            return;
        _attemptCancellation?.Cancel();
    }

    public async Task<SingleInstanceStartupResult> RunAsync()
    {
        while (true)
        {
            if (!_choice.Task.IsCompleted)
            {
                using var cancellation = new CancellationTokenSource();
                _attemptCancellation = cancellation;
                IsForcing = true;
                Status = "正在关闭旧程序";
                Detail = string.Empty;
                StateChanged?.Invoke();
                try
                {
                    await _forceCloseAsync(cancellation.Token);
                    if (!_choice.Task.IsCompleted)
                        return Finish(SingleInstanceStartupResult.ClosedEarlierInstances);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    Log.Error("Unable to terminate earlier ColorVision instances during startup.", exception);
                    Status = "旧进程未能结束";
                    Detail = exception.Message + "\n\n" + GetFailureAdvice(exception);
                }
                finally
                {
                    _attemptCancellation = null;
                    IsForcing = false;
                    StateChanged?.Invoke();
                }
            }

            // Await cancellation cleanup before allowing another instance to start.
            SingleInstanceStartupChoice choice = await _choice.Task;
            if (choice != SingleInstanceStartupChoice.ForceClose)
                return Finish(choice == SingleInstanceStartupChoice.OpenAdditional
                    ? SingleInstanceStartupResult.OpenAdditional : SingleInstanceStartupResult.Cancel);
            _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private static string GetFailureAdvice(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException or Win32Exception { NativeErrorCode: 5 })
                return "系统仍拒绝访问。请检查 ColorVision 服务主机；也可用管理员任务管理器结束对应进程后重试。";
        }
        return "请检查 ColorVision 服务主机，或在任务管理器的“详细信息”中按进程号检查。若以管理员身份仍无法结束，请保存其他工作后重启 Windows。";
    }

    private SingleInstanceStartupResult Finish(SingleInstanceStartupResult result)
    {
        _finished = true;
        return result;
    }
}
