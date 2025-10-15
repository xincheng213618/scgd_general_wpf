using ColorVision.Updater.FileOperations;
using ColorVision.Updater.Logging;
using ColorVision.Updater.Models;
using ColorVision.Updater.ProcessManagement;

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
