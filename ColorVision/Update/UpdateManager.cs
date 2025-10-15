using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ColorVision.Update;

/// <summary>
/// 更新管理器 - 协调主程序和插件的更新流程
/// </summary>
public class UpdateManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(UpdateManager));
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
    /// <param name="updatePackagePath">更新包路径 (.zip)</param>
    /// <param name="isIncremental">是否为增量更新</param>
    /// <returns>更新清单路径</returns>
    public string PrepareApplicationUpdate(string updatePackagePath, bool isIncremental)
    {
        log.Info($"准备应用程序更新: {updatePackagePath}, 增量更新: {isIncremental}");

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
                Version = AutoUpdater.GetInstance().LatestVersion?.ToString() ?? "",
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

        log.Info($"更新清单已创建: {manifestPath}, 文件数: {files.Count}");
        return manifestPath;
    }

    /// <summary>
    /// 准备插件更新
    /// </summary>
    /// <param name="pluginPackagePaths">插件包路径列表</param>
    /// <returns>更新清单路径</returns>
    public string PreparePluginUpdate(params string[] pluginPackagePaths)
    {
        log.Info($"准备插件更新: {string.Join(", ", pluginPackagePaths)}");

        // 实现逻辑类似 PrepareApplicationUpdate
        // 但目标路径为 Plugins 子目录
        throw new NotImplementedException("插件更新功能待实现");
    }

    /// <summary>
    /// 执行更新并重启
    /// </summary>
    /// <param name="manifestPath">更新清单路径</param>
    /// <param name="useOldBatMethod">是否使用旧的BAT方式（双轨并行期间）</param>
    public void ExecuteUpdate(string manifestPath, bool useOldBatMethod = false)
    {
        if (useOldBatMethod)
        {
            log.Warn("使用旧的BAT更新方式");
            throw new NotImplementedException("使用旧方法，请调用原有更新逻辑");
        }

        log.Info($"开始执行更新: {manifestPath}");

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

        try
        {
            Process.Start(startInfo);
            log.Info("更新器已启动，主程序即将退出");
            
            // 退出主程序
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            log.Error($"启动更新器失败: {ex.Message}", ex);
            throw;
        }
    }

    private string GetUpdaterExecutablePath()
    {
        // 优先使用配置的路径
        if (!string.IsNullOrEmpty(Config.UpdaterPath) && File.Exists(Config.UpdaterPath))
        {
            return Config.UpdaterPath;
        }

        // 使用默认路径
        var defaultPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ColorVision.Updater.exe"
        );

        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        throw new FileNotFoundException("找不到更新器程序 ColorVision.Updater.exe");
    }

    private bool ValidateUpdaterExists()
    {
        try
        {
            var updaterPath = GetUpdaterExecutablePath();
            return File.Exists(updaterPath);
        }
        catch
        {
            return false;
        }
    }

    private void EnsureUpdaterExists()
    {
        var updaterPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ColorVision.Updater.exe"
        );

        if (!File.Exists(updaterPath))
        {
            log.Warn($"更新器不存在: {updaterPath}");
            // TODO: 从嵌入资源中提取
            // ExtractUpdaterFromResources(updaterPath);
        }
    }

    private void ExtractUpdaterFromResources(string targetPath)
    {
        // TODO: 将更新器作为嵌入资源，然后在这里提取
        // 参考: Assembly.GetExecutingAssembly().GetManifestResourceStream()
        log.Info("从资源中提取更新器（待实现）");
    }
}
