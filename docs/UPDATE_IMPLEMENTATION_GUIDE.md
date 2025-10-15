# ColorVision 更新机制实施指南

## 目录

1. [阶段 1：创建独立更新器程序](#阶段-1创建独立更新器程序)
2. [阶段 2：集成到主程序](#阶段-2集成到主程序)
3. [阶段 3：双轨并行测试](#阶段-3双轨并行测试)
4. [阶段 4：完全迁移](#阶段-4完全迁移)
5. [阶段 5：清理优化](#阶段-5清理优化)

---

## 阶段 1：创建独立更新器程序

### 步骤 1.1：创建项目

```bash
# 在解决方案根目录下创建新项目
cd /home/runner/work/scgd_general_wpf/scgd_general_wpf
dotnet new console -n ColorVision.Updater -o Tools/ColorVision.Updater
dotnet sln add Tools/ColorVision.Updater/ColorVision.Updater.csproj
```

### 步骤 1.2：配置项目文件

编辑 `Tools/ColorVision.Updater/ColorVision.Updater.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>updater.ico</ApplicationIcon>
    <AssemblyName>ColorVision.Updater</AssemblyName>
    <RootNamespace>ColorVision.Updater</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

### 步骤 1.3：添加应用程序清单

创建 `Tools/ColorVision.Updater/app.manifest`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="ColorVision.Updater"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <!-- 请求管理员权限 -->
        <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows 10 / 11 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

### 步骤 1.4：创建数据模型

创建 `Tools/ColorVision.Updater/Models/UpdateManifest.cs`：

```csharp
namespace ColorVision.Updater.Models;

public class UpdateManifest
{
    public string Version { get; set; } = "1.0";
    public UpdateType UpdateType { get; set; }
    public UpdateInfo UpdateInfo { get; set; } = new();
    public PathConfiguration Paths { get; set; } = new();
    public ExecutableConfiguration Executable { get; set; } = new();
    public UpdateOptions Options { get; set; } = new();
    public List<FileOperation> Files { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string? Signature { get; set; }
}

public enum UpdateType
{
    Application,
    Plugin
}

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class PathConfiguration
{
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string BackupPath { get; set; } = "";
}

public class ExecutableConfiguration
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
}

public class UpdateOptions
{
    public bool CreateBackup { get; set; } = true;
    public bool VerifyFiles { get; set; } = true;
    public bool RestartAfterUpdate { get; set; } = true;
    public bool CleanupOnSuccess { get; set; } = true;
    public bool RollbackOnFailure { get; set; } = true;
}

public class FileOperation
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public FileAction Action { get; set; } = FileAction.Replace;
    public bool Verify { get; set; } = true;
    public bool Critical { get; set; } = true;
    public string? ExpectedHash { get; set; }
}

public enum FileAction
{
    Replace,
    Add,
    Delete
}
```

### 步骤 1.5：实现日志记录器

创建 `Tools/ColorVision.Updater/Logging/UpdateLogger.cs`：

```csharp
namespace ColorVision.Updater.Logging;

public class UpdateLogger
{
    private readonly string _logFilePath;
    private readonly LogLevel _minLevel;
    
    public UpdateLogger(string logFilePath, LogLevel minLevel = LogLevel.Info)
    {
        _logFilePath = logFilePath;
        _minLevel = minLevel;
        
        // 确保日志目录存在
        var logDir = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
    }
    
    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    
    private void Log(LogLevel level, string message)
    {
        if (level < _minLevel) return;
        
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level,-7}] {message}";
        
        try
        {
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
        }
        catch
        {
            // 忽略日志写入失败
        }
        
        // 同时输出到控制台
        Console.WriteLine(logEntry);
    }
}

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}
```

### 步骤 1.6：实现进程管理器

创建 `Tools/ColorVision.Updater/ProcessManagement/ProcessManager.cs`：

```csharp
using System.Diagnostics;

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
```

### 步骤 1.7：实现文件操作器

创建 `Tools/ColorVision.Updater/FileOperations/FileOperator.cs`：

```csharp
using System.Security.Cryptography;

namespace ColorVision.Updater.FileOperations;

public class FileOperator
{
    private readonly UpdateLogger _logger;
    
    public FileOperator(UpdateLogger logger)
    {
        _logger = logger;
    }
    
    public bool CopyFile(string source, string destination, bool overwrite = true)
    {
        try
        {
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            
            File.Copy(source, destination, overwrite);
            _logger.Debug($"已复制: {source} -> {destination}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"复制文件失败: {source} -> {destination}, 错误: {ex.Message}");
            return false;
        }
    }
    
    public bool DeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.Debug($"已删除: {filePath}");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"删除文件失败: {filePath}, 错误: {ex.Message}");
            return false;
        }
    }
    
    public string CalculateFileHash(string filePath)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.Error($"计算文件哈希失败: {filePath}, 错误: {ex.Message}");
            return string.Empty;
        }
    }
    
    public bool VerifyFile(string filePath, string expectedHash)
    {
        var actualHash = CalculateFileHash(filePath);
        var isValid = actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        
        if (!isValid)
        {
            _logger.Warning($"文件验证失败: {filePath}");
            _logger.Warning($"  期望: {expectedHash}");
            _logger.Warning($"  实际: {actualHash}");
        }
        
        return isValid;
    }
    
    public bool SafeDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return true;
            
            // 移除只读属性
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                var attr = File.GetAttributes(file);
                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(file, attr & ~FileAttributes.ReadOnly);
                }
            }
            
            Directory.Delete(path, true);
            _logger.Debug($"已删除目录: {path}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"删除目录失败: {path}, 错误: {ex.Message}");
            return false;
        }
    }
}
```

### 步骤 1.8：实现备份管理器

创建 `Tools/ColorVision.Updater/FileOperations/BackupManager.cs`：

```csharp
namespace ColorVision.Updater.FileOperations;

public class BackupManager
{
    private readonly UpdateLogger _logger;
    private readonly FileOperator _fileOperator;
    
    public BackupManager(UpdateLogger logger, FileOperator fileOperator)
    {
        _logger = logger;
        _fileOperator = fileOperator;
    }
    
    public string? CreateBackup(string targetPath, List<FileOperation> files)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(
                Path.GetDirectoryName(targetPath) ?? targetPath,
                $"ColorVisionBackup_{timestamp}"
            );
            
            _logger.Info($"创建备份: {backupPath}");
            Directory.CreateDirectory(backupPath);
            
            int backedUpCount = 0;
            foreach (var file in files)
            {
                if (file.Action == FileAction.Delete) continue;
                
                var sourcePath = Path.Combine(targetPath, file.Target);
                if (!File.Exists(sourcePath)) continue;
                
                var destPath = Path.Combine(backupPath, file.Target);
                if (_fileOperator.CopyFile(sourcePath, destPath))
                {
                    backedUpCount++;
                }
            }
            
            _logger.Info($"备份完成，已备份 {backedUpCount} 个文件");
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.Error($"创建备份失败: {ex.Message}");
            return null;
        }
    }
    
    public bool Rollback(string backupPath, string targetPath)
    {
        try
        {
            _logger.Info($"开始回滚: {backupPath} -> {targetPath}");
            
            if (!Directory.Exists(backupPath))
            {
                _logger.Error($"备份目录不存在: {backupPath}");
                return false;
            }
            
            int restoredCount = 0;
            foreach (var file in Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(backupPath, file);
                var targetFile = Path.Combine(targetPath, relativePath);
                
                if (_fileOperator.CopyFile(file, targetFile))
                {
                    restoredCount++;
                }
            }
            
            _logger.Info($"回滚完成，已恢复 {restoredCount} 个文件");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"回滚失败: {ex.Message}");
            return false;
        }
    }
    
    public void CleanupOldBackups(string backupParentPath, int retentionDays = 7)
    {
        try
        {
            if (!Directory.Exists(backupParentPath)) return;
            
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var backupDirs = Directory.GetDirectories(backupParentPath, "ColorVisionBackup_*");
            
            foreach (var backupDir in backupDirs)
            {
                var dirInfo = new DirectoryInfo(backupDir);
                if (dirInfo.CreationTime < cutoffDate)
                {
                    _logger.Info($"清理旧备份: {backupDir}");
                    _fileOperator.SafeDeleteDirectory(backupDir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"清理旧备份时出错: {ex.Message}");
        }
    }
}
```

### 步骤 1.9：实现更新执行器

创建 `Tools/ColorVision.Updater/UpdateExecutor.cs`：

```csharp
using ColorVision.Updater.FileOperations;
using ColorVision.Updater.Models;
using ColorVision.Updater.ProcessManagement;
using Newtonsoft.Json;

namespace ColorVision.Updater;

public class UpdateExecutor
{
    private readonly UpdateLogger _logger;
    private readonly FileOperator _fileOperator;
    private readonly BackupManager _backupManager;
    private readonly ProcessManager _processManager;
    
    public UpdateExecutor(UpdateLogger logger)
    {
        _logger = logger;
        _fileOperator = new FileOperator(logger);
        _backupManager = new BackupManager(logger, _fileOperator);
        _processManager = new ProcessManager(logger);
    }
    
    public async Task<bool> ExecuteUpdate(UpdateManifest manifest, int? processId = null)
    {
        _logger.Info("========================================");
        _logger.Info($"开始更新: {manifest.UpdateInfo.Name} v{manifest.UpdateInfo.Version}");
        _logger.Info($"更新类型: {manifest.UpdateType}");
        _logger.Info("========================================");
        
        string? backupPath = null;
        
        try
        {
            // 1. 等待主程序退出
            if (processId.HasValue)
            {
                if (!await _processManager.WaitForProcessExit(processId.Value, 60))
                {
                    _logger.Error("主程序未能在规定时间内退出");
                    return false;
                }
            }
            
            // 额外等待，确保文件解锁
            await Task.Delay(1000);
            
            // 2. 创建备份
            if (manifest.Options.CreateBackup)
            {
                backupPath = _backupManager.CreateBackup(
                    manifest.Paths.TargetPath,
                    manifest.Files
                );
                
                if (backupPath == null && manifest.Options.RollbackOnFailure)
                {
                    _logger.Error("创建备份失败，终止更新");
                    return false;
                }
            }
            
            // 3. 执行文件操作
            _logger.Info("开始复制文件...");
            var failedFiles = new List<FileOperation>();
            
            foreach (var fileOp in manifest.Files)
            {
                bool success = fileOp.Action switch
                {
                    FileAction.Replace or FileAction.Add => CopyFile(manifest, fileOp),
                    FileAction.Delete => DeleteFile(manifest, fileOp),
                    _ => false
                };
                
                if (!success && fileOp.Critical)
                {
                    _logger.Error($"关键文件操作失败: {fileOp.Target}");
                    failedFiles.Add(fileOp);
                }
            }
            
            // 4. 验证文件
            if (manifest.Options.VerifyFiles && failedFiles.Count == 0)
            {
                _logger.Info("验证文件...");
                failedFiles.AddRange(VerifyFiles(manifest));
            }
            
            // 5. 处理失败情况
            if (failedFiles.Count > 0)
            {
                _logger.Error($"{failedFiles.Count} 个文件操作失败");
                
                if (manifest.Options.RollbackOnFailure && backupPath != null)
                {
                    _logger.Info("执行回滚...");
                    _backupManager.Rollback(backupPath, manifest.Paths.TargetPath);
                }
                
                return false;
            }
            
            _logger.Info("文件更新完成");
            
            // 6. 清理临时文件
            if (manifest.Options.CleanupOnSuccess)
            {
                _logger.Info("清理临时文件...");
                _fileOperator.SafeDeleteDirectory(manifest.Paths.SourcePath);
            }
            
            // 7. 重启程序
            if (manifest.Options.RestartAfterUpdate)
            {
                var exePath = Path.Combine(
                    manifest.Paths.TargetPath,
                    manifest.Executable.Name
                );
                
                _processManager.StartProcess(
                    exePath,
                    manifest.Executable.Arguments,
                    manifest.Executable.WorkingDirectory
                );
            }
            
            _logger.Info("更新成功完成！");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"更新过程出错: {ex.Message}");
            _logger.Error($"堆栈跟踪: {ex.StackTrace}");
            
            if (manifest.Options.RollbackOnFailure && backupPath != null)
            {
                _logger.Info("执行回滚...");
                _backupManager.Rollback(backupPath, manifest.Paths.TargetPath);
            }
            
            return false;
        }
    }
    
    private bool CopyFile(UpdateManifest manifest, FileOperation fileOp)
    {
        var sourcePath = Path.Combine(manifest.Paths.SourcePath, fileOp.Source);
        var targetPath = Path.Combine(manifest.Paths.TargetPath, fileOp.Target);
        
        if (!File.Exists(sourcePath))
        {
            _logger.Warning($"源文件不存在: {sourcePath}");
            return !fileOp.Critical;
        }
        
        return _fileOperator.CopyFile(sourcePath, targetPath);
    }
    
    private bool DeleteFile(UpdateManifest manifest, FileOperation fileOp)
    {
        var targetPath = Path.Combine(manifest.Paths.TargetPath, fileOp.Target);
        return _fileOperator.DeleteFile(targetPath);
    }
    
    private List<FileOperation> VerifyFiles(UpdateManifest manifest)
    {
        var failedFiles = new List<FileOperation>();
        
        foreach (var fileOp in manifest.Files)
        {
            if (!fileOp.Verify || string.IsNullOrEmpty(fileOp.ExpectedHash)) continue;
            if (fileOp.Action == FileAction.Delete) continue;
            
            var targetPath = Path.Combine(manifest.Paths.TargetPath, fileOp.Target);
            
            if (!_fileOperator.VerifyFile(targetPath, fileOp.ExpectedHash))
            {
                failedFiles.Add(fileOp);
            }
        }
        
        return failedFiles;
    }
}
```

### 步骤 1.10：实现主程序

创建 `Tools/ColorVision.Updater/Program.cs`：

```csharp
using System.CommandLine;
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
```

### 步骤 1.11：构建和测试

```bash
# 构建项目
cd Tools/ColorVision.Updater
dotnet build -c Release

# 测试更新器（需要准备测试清单）
./bin/Release/net8.0-windows/ColorVision.Updater.exe --manifest test-manifest.json --log-level Debug
```

---

## 阶段 2：集成到主程序

### 步骤 2.1：创建 UpdateManager 类

在主程序中创建 `ColorVision/Update/UpdateManager.cs`：

```csharp
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace ColorVision.Update;

public class UpdateManager
{
    private static UpdateManager? _instance;
    private static readonly object _lock = new();
    
    public static UpdateManager Instance
    {
        get
        {
            lock (_lock)
            {
                return _instance ??= new UpdateManager();
            }
        }
    }
    
    public UpdateManagerConfig Config { get; }
    
    private UpdateManager()
    {
        Config = UpdateManagerConfig.Instance;
        EnsureUpdaterExists();
    }
    
    /// <summary>
    /// 准备应用程序更新
    /// </summary>
    public string PrepareApplicationUpdate(string updatePackagePath, bool isIncremental)
    {
        // 1. 创建临时目录
        var tempDir = Path.Combine(Config.TempUpdateDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        // 2. 解压更新包
        var extractPath = Path.Combine(tempDir, "Extract");
        ZipFile.ExtractToDirectory(updatePackagePath, extractPath);
        
        // 3. 生成文件列表
        var files = new List<FileOperation>();
        foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(extractPath, file);
            files.Add(new FileOperation
            {
                Source = relativePath,
                Target = relativePath,
                Action = FileAction.Replace,
                Verify = false, // 可选：添加哈希验证
                Critical = true
            });
        }
        
        // 4. 创建更新清单
        var manifest = new UpdateManifest
        {
            UpdateType = UpdateType.Application,
            UpdateInfo = new UpdateInfo
            {
                Version = AutoUpdater.LatestVersion?.ToString() ?? "",
                Name = "ColorVision",
                Description = isIncremental ? "增量更新" : "完整更新"
            },
            Paths = new PathConfiguration
            {
                SourcePath = extractPath,
                TargetPath = AppDomain.CurrentDomain.BaseDirectory,
                BackupPath = "" // 由更新器自动生成
            },
            Executable = new ExecutableConfiguration
            {
                Name = Path.GetFileName(Environment.ProcessPath) ?? "ColorVision.exe",
                Arguments = "",
                WorkingDirectory = ""
            },
            Options = new UpdateOptions
            {
                CreateBackup = Config.EnableBackup,
                VerifyFiles = false,
                RestartAfterUpdate = true,
                CleanupOnSuccess = true,
                RollbackOnFailure = true
            },
            Files = files,
            Timestamp = DateTime.UtcNow
        };
        
        // 5. 保存清单
        var manifestPath = Path.Combine(tempDir, "update-manifest.json");
        var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
        File.WriteAllText(manifestPath, manifestJson);
        
        return manifestPath;
    }
    
    /// <summary>
    /// 准备插件更新
    /// </summary>
    public string PreparePluginUpdate(params string[] pluginPackagePaths)
    {
        // 实现逻辑类似 PrepareApplicationUpdate
        // 但目标路径为 Plugins 子目录
        throw new NotImplementedException();
    }
    
    /// <summary>
    /// 执行更新并重启
    /// </summary>
    public void ExecuteUpdate(string manifestPath, bool useOldBatMethod = false)
    {
        if (useOldBatMethod)
        {
            // 回退到旧的 BAT 方式
            throw new NotImplementedException("使用旧方法");
        }
        
        // 保存配置
        ConfigHandler.GetInstance().SaveConfigs();
        
        // 获取当前进程 ID
        var processId = Process.GetCurrentProcess().Id;
        
        // 启动更新器
        var updaterPath = GetUpdaterExecutablePath();
        var startInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = $"--manifest \"{manifestPath}\" --pid {processId}",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        
        // 如果在 Program Files，请求管理员权限
        if (AppDomain.CurrentDomain.BaseDirectory.Contains("Program Files"))
        {
            startInfo.Verb = "runas";
        }
        
        Process.Start(startInfo);
        
        // 退出主程序
        Environment.Exit(0);
    }
    
    private string GetUpdaterExecutablePath()
    {
        if (!string.IsNullOrEmpty(Config.UpdaterPath) && File.Exists(Config.UpdaterPath))
        {
            return Config.UpdaterPath;
        }
        
        var defaultPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ColorVision.Updater.exe"
        );
        
        return File.Exists(defaultPath) ? defaultPath : throw new FileNotFoundException("找不到更新器程序");
    }
    
    private void EnsureUpdaterExists()
    {
        var updaterPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ColorVision.Updater.exe"
        );
        
        if (!File.Exists(updaterPath))
        {
            // 从嵌入资源中提取
            ExtractUpdaterFromResources(updaterPath);
        }
    }
    
    private void ExtractUpdaterFromResources(string targetPath)
    {
        // TODO: 将更新器作为嵌入资源，然后在这里提取
        // 参考: Assembly.GetExecutingAssembly().GetManifestResourceStream()
    }
}

// 数据模型类（与更新器中的定义一致）
// 可以考虑将这些模型移到共享类库中
public class UpdateManifest { /* ... */ }
public class UpdateInfo { /* ... */ }
public class PathConfiguration { /* ... */ }
public class ExecutableConfiguration { /* ... */ }
public class UpdateOptions { /* ... */ }
public class FileOperation { /* ... */ }
public enum UpdateType { Application, Plugin }
public enum FileAction { Replace, Add, Delete }
```

### 步骤 2.2：创建配置类

```csharp
public class UpdateManagerConfig : ViewModelBase, IConfig
{
    public static UpdateManagerConfig Instance => ConfigService.Instance.GetRequiredService<UpdateManagerConfig>();
    
    public bool UseNewUpdateMechanism { get => _useNew; set { _useNew = value; OnPropertyChanged(); } }
    private bool _useNew = true;
    
    public string UpdaterPath { get => _updaterPath; set { _updaterPath = value; OnPropertyChanged(); } }
    private string _updaterPath = "";
    
    public bool EnableBackup { get => _enableBackup; set { _enableBackup = value; OnPropertyChanged(); } }
    private bool _enableBackup = true;
    
    public int BackupRetentionDays { get => _retentionDays; set { _retentionDays = value; OnPropertyChanged(); } }
    private int _retentionDays = 7;
    
    public string TempUpdateDirectory { get => _tempDir; set { _tempDir = value; OnPropertyChanged(); } }
    private string _tempDir = Path.Combine(Path.GetTempPath(), "ColorVisionUpdate");
}
```

### 步骤 2.3：改造 AutoUpdater

```csharp
// 在 AutoUpdater.cs 中
public static void RestartIsIncrementApplication(string downloadPath)
{
    if (UpdateManagerConfig.Instance.UseNewUpdateMechanism)
    {
        var manifestPath = UpdateManager.Instance.PrepareApplicationUpdate(downloadPath, true);
        UpdateManager.Instance.ExecuteUpdate(manifestPath);
    }
    else
    {
        RestartIsIncrementApplication_Old(downloadPath);
    }
}

// 重命名原方法
private static void RestartIsIncrementApplication_Old(string downloadPath)
{
    // ... 原有实现 ...
}
```

---

## 阶段 3-5：后续实施

详细步骤将在实际执行时根据前期成果调整。关键点：

1. **阶段 3**：充分测试，确保新旧方案都正常工作
2. **阶段 4**：逐步切换到新方案，监控用户反馈
3. **阶段 5**：移除旧代码，完成清理

---

## 总结

按照本指南逐步实施，可以安全、平稳地完成从 BAT 脚本到独立更新器的迁移。每个阶段都应该：

1. ✅ 编写代码
2. ✅ 单元测试
3. ✅ 集成测试
4. ✅ 代码审查
5. ✅ 文档更新

确保每一步都经过验证后再进入下一阶段。
