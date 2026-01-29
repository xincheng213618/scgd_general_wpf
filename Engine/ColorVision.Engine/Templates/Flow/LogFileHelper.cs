using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Flow
{
    public class LogFileHelper
    {
        /// <summary>
        /// 获取最新的日志文件路径（主日志）
        /// </summary>
        public static string GetLatestMainLogPath(string baseDir)
        {
            string logDir = Path.Combine(baseDir, "log");
            return Path.Combine(logDir, $"{DateTime.Now:yyyyMMdd}.log");
        }

        /// <summary>
        /// 获取最新的Info日志文件路径
        /// </summary>
        public static string GetLatestInfoLogPath(string baseDir)
        {
            string logDir = Path.Combine(baseDir, "log", "LogInfo");
            return Path.Combine(logDir, $"{DateTime.Now:yyyyMMdd}.log");
        }

        /// <summary>
        /// 获取最新的Error日志文件路径
        /// </summary>
        public static string GetLatestErrorLogPath(string baseDir)
        {
            string logDir = Path.Combine(baseDir, "log", "LogError");
            return Path.Combine(logDir, $"{DateTime.Now:yyyyMMdd}.log");
        }

        /// <summary>
        /// 获取目录下最新修改的日志文件（备用方案）
        /// </summary>
        public static string GetMostRecentLogFile(string logDirectory, string prefix = "")
        {
            if (!Directory.Exists(logDirectory))
                return null;

            // 如果传进来的是完整路径，取出文件名前缀
            var filePrefix = Path.GetFileNameWithoutExtension(prefix);

            var pattern = $"{filePrefix}*.log";

            var latest = Directory.EnumerateFiles(logDirectory, pattern, SearchOption.TopDirectoryOnly)
                                  .Select(p => new FileInfo(p))
                                  .OrderByDescending(f => f.LastWriteTimeUtc)
                                  .FirstOrDefault();

            return latest?.FullName;
        }
    }
}
