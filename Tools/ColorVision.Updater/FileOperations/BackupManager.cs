using ColorVision.Updater.Logging;
using ColorVision.Updater.Models;

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
