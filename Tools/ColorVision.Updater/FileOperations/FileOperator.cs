using System.Security.Cryptography;
using ColorVision.Updater.Logging;

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
