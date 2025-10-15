using System.Diagnostics;
using ColorVision.Updater.Logging;

namespace ColorVision.Updater.ProcessManagement;

public class ProcessManager
{
    private readonly UpdateLogger _logger;
    
    public ProcessManager(UpdateLogger logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> WaitForProcessExit(int processId, int timeoutSeconds = 60)
    {
        _logger.Info($"等待进程 {processId} 退出...");
        
        try
        {
            var process = Process.GetProcessById(processId);
            var processName = process.ProcessName;
            
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            
            while (!process.HasExited)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    _logger.Warning($"进程 {processId} ({processName}) 在 {timeoutSeconds} 秒内未退出");
                    return false;
                }
                
                await Task.Delay(100, cts.Token);
            }
            
            _logger.Info($"进程 {processId} ({processName}) 已退出");
            return true;
        }
        catch (ArgumentException)
        {
            _logger.Info($"进程 {processId} 已不存在");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"等待进程退出时出错: {ex.Message}");
            return false;
        }
    }
    
    public bool StartProcess(string exePath, string arguments, string workingDirectory)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) 
                    ? Path.GetDirectoryName(exePath) 
                    : workingDirectory,
                UseShellExecute = true
            };
            
            _logger.Info($"启动进程: {exePath} {arguments}");
            var process = Process.Start(startInfo);
            
            return process != null;
        }
        catch (Exception ex)
        {
            _logger.Error($"启动进程失败: {ex.Message}");
            return false;
        }
    }
}
