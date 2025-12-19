using ColorVision.Common.MVVM;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Configuration for file cleanup after flow execution
    /// </summary>
    public class FileCleanupProcessConfig : ViewModelBase
    {
        [DisplayName("清理文件夹")]
        [Description("要清理的文件夹路径")]
        [PropertyEditorType(typeof(ColorVision.UI.TextSelectFolderPropertiesEditor))]
        public string FolderPath { get => _FolderPath; set { _FolderPath = value; OnPropertyChanged(); } }
        private string _FolderPath = string.Empty;

        [DisplayName("文件扩展名")]
        [Description("要删除的文件扩展名（逗号分隔，例如: .tmp,.log,.cache）。留空表示删除所有文件")]
        public string FileExtensions { get => _FileExtensions; set { _FileExtensions = value; OnPropertyChanged(); } }
        private string _FileExtensions = ".tmp,.cache";

        [DisplayName("文件名模式")]
        [Description("要删除的文件名模式（支持通配符*和?，例如: temp_*）。留空表示匹配所有文件")]
        public string FilePattern { get => _FilePattern; set { _FilePattern = value; OnPropertyChanged(); } }
        private string _FilePattern = string.Empty;

        [DisplayName("包含子文件夹")]
        [Description("是否包含子文件夹中的文件")]
        public bool IncludeSubfolders { get => _IncludeSubfolders; set { _IncludeSubfolders = value; OnPropertyChanged(); } }
        private bool _IncludeSubfolders = false;

        [DisplayName("保留最近N个文件")]
        [Description("保留最近修改的N个文件（按最后修改时间排序）。设置为0表示不保留")]
        public int KeepRecentFiles { get => _KeepRecentFiles; set { _KeepRecentFiles = value; OnPropertyChanged(); } }
        private int _KeepRecentFiles = 0;

        [DisplayName("仅删除旧于N天的文件")]
        [Description("仅删除最后修改时间早于N天的文件。设置为0表示不限制")]
        public int DeleteOlderThanDays { get => _DeleteOlderThanDays; set { _DeleteOlderThanDays = value; OnPropertyChanged(); } }
        private int _DeleteOlderThanDays = 0;

        [DisplayName("启用")]
        [Description("是否启用此后处理")]
        public bool Enabled { get => _Enabled; set { _Enabled = value; OnPropertyChanged(); } }
        private bool _Enabled = true;

        [DisplayName("删除空文件夹")]
        [Description("删除文件后，是否删除空的文件夹")]
        public bool DeleteEmptyFolders { get => _DeleteEmptyFolders; set { _DeleteEmptyFolders = value; OnPropertyChanged(); } }
        private bool _DeleteEmptyFolders = false;
    }

    [BatchProcess("文件清理", "删除流程中产生的临时文件或指定文件")]
    public class FileCleanupProcess : BatchProcessBase<FileCleanupProcessConfig>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FileCleanupProcess));

        public override bool Process(IBatchContext ctx)
        {
            if (!Config.Enabled)
            {
                log.Info("FileCleanupProcess 未启用，跳过");
                return true;
            }

            if (string.IsNullOrWhiteSpace(Config.FolderPath))
            {
                log.Warn("FileCleanupProcess: 文件夹路径未配置");
                return true; // Don't fail the batch
            }

            if (!Directory.Exists(Config.FolderPath))
            {
                log.Warn($"FileCleanupProcess: 文件夹不存在 {Config.FolderPath}");
                return true; // Don't fail the batch
            }

            try
            {
                // Parse file extensions
                var extensions = ParseExtensions(Config.FileExtensions);
                
                // Get all files matching the criteria
                var searchOption = Config.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var pattern = string.IsNullOrWhiteSpace(Config.FilePattern) ? "*.*" : Config.FilePattern;
                
                var allFiles = Directory.GetFiles(Config.FolderPath, pattern, searchOption);
                
                // Filter by extensions if specified
                var files = extensions.Count > 0
                    ? allFiles.Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList()
                    : allFiles.ToList();

                if (files.Count == 0)
                {
                    log.Info($"FileCleanupProcess: 文件夹中没有匹配的文件 {Config.FolderPath}");
                    return true;
                }

                // Filter by age if specified
                if (Config.DeleteOlderThanDays > 0)
                {
                    var cutoffDate = DateTime.Now.AddDays(-Config.DeleteOlderThanDays);
                    var filteredFiles = new List<string>();
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            if (File.GetLastWriteTime(file) < cutoffDate)
                            {
                                filteredFiles.Add(file);
                            }
                        }
                        catch
                        {
                            // Skip files we can't access
                        }
                    }
                    
                    files = filteredFiles;
                    
                    if (files.Count == 0)
                    {
                        log.Info($"FileCleanupProcess: 没有早于 {Config.DeleteOlderThanDays} 天的文件");
                        return true;
                    }
                }

                // Create FileInfo objects only for files that passed the filters
                var fileInfos = new List<FileInfo>();
                foreach (var file in files)
                {
                    try
                    {
                        fileInfos.Add(new FileInfo(file));
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }
                
                // Sort by last write time
                fileInfos.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));

                if (Config.KeepRecentFiles > 0 && fileInfos.Count > Config.KeepRecentFiles)
                {
                    // Remove the most recent files from deletion list
                    fileInfos = fileInfos.Skip(Config.KeepRecentFiles).ToList();
                }

                if (fileInfos.Count == 0)
                {
                    log.Info("FileCleanupProcess: 根据保留规则，没有文件需要删除");
                    return true;
                }

                // Delete files
                int deletedCount = 0;
                long deletedSize = 0;
                var foldersToCheck = new HashSet<string>();

                foreach (var fileInfo in fileInfos)
                {
                    try
                    {
                        long fileSize = fileInfo.Length;
                        string folder = fileInfo.DirectoryName;
                        File.Delete(fileInfo.FullName);
                        deletedSize += fileSize;
                        deletedCount++;
                        log.Debug($"删除文件: {fileInfo.Name} ({fileSize / 1024}KB)");
                        
                        if (Config.DeleteEmptyFolders && !string.IsNullOrEmpty(folder))
                        {
                            foldersToCheck.Add(folder);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"删除文件失败: {fileInfo.Name}", ex);
                    }
                }

                log.Info($"FileCleanupProcess: 清理完成。删除了 {deletedCount} 个文件，释放了 {deletedSize / (1024 * 1024)}MB");

                // Delete empty folders if requested
                if (Config.DeleteEmptyFolders && foldersToCheck.Count > 0)
                {
                    DeleteEmptyFolders(foldersToCheck, Config.FolderPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error("FileCleanupProcess 执行失败", ex);
                return true; // Don't fail the batch on errors
            }
        }

        private void DeleteEmptyFolders(HashSet<string> foldersToCheck, string rootFolder)
        {
            // Order folders by depth (deepest first) to delete from bottom up
            var sortedFolders = foldersToCheck
                .OrderByDescending(f => f.Split(Path.DirectorySeparatorChar).Length)
                .ToList();

            int deletedFolderCount = 0;

            foreach (var folder in sortedFolders)
            {
                try
                {
                    // Don't delete the root folder
                    if (folder.Equals(rootFolder, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Check if folder is empty
                    if (Directory.Exists(folder) && 
                        !Directory.EnumerateFileSystemEntries(folder).Any())
                    {
                        Directory.Delete(folder);
                        deletedFolderCount++;
                        log.Debug($"删除空文件夹: {folder}");
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"删除空文件夹失败: {folder}", ex);
                }
            }

            if (deletedFolderCount > 0)
            {
                log.Info($"FileCleanupProcess: 删除了 {deletedFolderCount} 个空文件夹");
            }
        }

        private HashSet<string> ParseExtensions(string extensionsStr)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            if (string.IsNullOrWhiteSpace(extensionsStr))
                return result;

            var parts = extensionsStr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var ext = part.Trim();
                if (!ext.StartsWith("."))
                    ext = "." + ext;
                result.Add(ext.ToLowerInvariant());
            }

            return result;
        }
    }
}
