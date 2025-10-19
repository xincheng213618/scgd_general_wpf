using System.CommandLine;
using ColorVision.Updater.Logging;
using ColorVision.Updater.Models;
using Newtonsoft.Json;

namespace ColorVision.Updater;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var manifestOption = new Option<string>(
            name: "--manifest",
            description: "更新清单文件路径 (JSON)")
        {
            IsRequired = true
        };
        
        var pidOption = new Option<int?>(
            name: "--pid",
            description: "主程序进程ID，等待其退出");
        
        var logLevelOption = new Option<string>(
            name: "--log-level",
            description: "日志级别 (Debug|Info|Warning|Error)",
            getDefaultValue: () => "Info");
        
        var rootCommand = new RootCommand("ColorVision 更新器")
        {
            manifestOption,
            pidOption,
            logLevelOption
        };
        
        rootCommand.SetHandler(async (manifestPath, pid, logLevelStr) =>
        {
            await ExecuteUpdate(manifestPath, pid, logLevelStr);
        }, manifestOption, pidOption, logLevelOption);
        
        return await rootCommand.InvokeAsync(args);
    }
    
    static async Task ExecuteUpdate(string manifestPath, int? processId, string logLevelStr)
    {
        // 解析日志级别
        Enum.TryParse<LogLevel>(logLevelStr, true, out var logLevel);
        
        // 初始化日志
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ColorVision",
            "Logs"
        );
        var logPath = Path.Combine(logDir, $"Updater_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        var logger = new UpdateLogger(logPath, logLevel);
        
        try
        {
            // 读取清单
            if (!File.Exists(manifestPath))
            {
                logger.Error($"清单文件不存在: {manifestPath}");
                Environment.Exit(1);
            }
            
            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonConvert.DeserializeObject<UpdateManifest>(manifestJson);
            
            if (manifest == null)
            {
                logger.Error("清单文件解析失败");
                Environment.Exit(1);
            }
            
            // 执行更新
            var executor = new UpdateExecutor(logger);
            var success = await executor.ExecuteUpdate(manifest, processId);
            
            Environment.Exit(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            logger.Error($"更新器异常: {ex.Message}");
            logger.Error($"堆栈跟踪: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
