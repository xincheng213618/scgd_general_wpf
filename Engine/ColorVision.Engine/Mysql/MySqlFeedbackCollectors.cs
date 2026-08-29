using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Database
{
    /// <summary>
    /// Adds the compact, restorable resource/configuration database export to feedback packages.
    /// Historical result tables and image data are intentionally excluded.
    /// </summary>
    public sealed class MySqlResourceFeedbackCollector : IFeedbackLogCollector
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlResourceFeedbackCollector));

        public string Name => "数据库资源与配置";
        public string Description => "产品、模板、设备和服务配置，不包含历史检测结果";
        public int Order => 31;
        public bool IsSelectedByDefault => true;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            try
            {
                string backupPath = MySqlLocalServicesManager.CreateFeedbackResourceBackup();
                return [("Database/ColorVisionResources.sql", backupPath)];
            }
            catch (Exception ex)
            {
                log.Warn("反馈诊断包收集数据库资源失败。", ex);
                return [CreateStatusFile("Database/collection-error.txt", "数据库资源与配置收集失败", ex)];
            }
        }

        private static (string EntryPath, string FilePath) CreateStatusFile(string entryPath, string title, Exception exception)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_Feedback_{Guid.NewGuid():N}.txt");
            File.WriteAllText(
                tempPath,
                $"{title}{Environment.NewLine}时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}原因：{exception.GetType().Name}: {exception.Message}",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return (entryPath, tempPath);
        }
    }

    /// <summary>
    /// Exports database-backed device licenses as directly reusable .lic files plus a readable index.
    /// </summary>
    public sealed class LicenseFeedbackCollector : IFeedbackLogCollector
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LicenseFeedbackCollector));
        private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

        public string Name => "许可证信息";
        public string Description => "许可证索引及可直接导入的独立 .lic 文件";
        public int Order => 32;
        public bool IsSelectedByDefault => true;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            try
            {
                List<LicenseModel> licenses = MySqlLocalServicesManager.RunDatabaseMaintenance(() =>
                {
                    using var db = new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = MySqlControl.GetConnectionString(),
                        DbType = DbType.MySql,
                        IsAutoCloseConnection = true,
                    });
                    return db.Queryable<LicenseModel>()
                        .OrderBy(item => item.MacAddress)
                        .ToList();
                });
                return CreateFiles(licenses, DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                log.Warn("反馈诊断包收集许可证信息失败。", ex);
                return [CreateErrorFile(ex)];
            }
        }

        internal static IReadOnlyList<(string EntryPath, string FilePath)> CreateFiles(
            IEnumerable<LicenseModel> source,
            DateTimeOffset generatedAt)
        {
            ArgumentNullException.ThrowIfNull(source);
            List<LicenseModel> licenses = source.OrderBy(item => item.MacAddress, StringComparer.OrdinalIgnoreCase).ToList();
            var results = new List<(string EntryPath, string FilePath)>();
            var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var indexItems = new List<object>(licenses.Count);

            foreach (LicenseModel license in licenses)
            {
                string? licenseFile = null;
                if (!string.IsNullOrWhiteSpace(license.LicenseValue))
                {
                    licenseFile = CreateUniqueLicenseFileName(license, usedFileNames);
                    string tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_License_{Guid.NewGuid():N}.lic");
                    File.WriteAllText(tempPath, license.LicenseValue, Utf8WithoutBom);
                    results.Add(($"License/{licenseFile}", tempPath));
                }

                indexItems.Add(new
                {
                    license.Id,
                    LicenseFile = licenseFile,
                    LicenseType = license.LiceType,
                    DeviceResourceId = license.DevCameraId,
                    CalibrationResourceId = license.DevCaliId,
                    MacOrSerial = license.MacAddress,
                    license.Model,
                    CustomerName = license.CusTomerName,
                    ExpiresAt = license.ExpiryDate,
                    CreatedAt = license.CreateDate,
                    HasLicenseValue = !string.IsNullOrWhiteSpace(license.LicenseValue),
                });
            }

            string indexPath = Path.Combine(Path.GetTempPath(), $"ColorVision_LicenseIndex_{Guid.NewGuid():N}.json");
            string indexJson = JsonConvert.SerializeObject(new
            {
                GeneratedAt = generatedAt,
                SourceTable = "t_scgd_camera_license",
                Count = licenses.Count,
                Licenses = indexItems,
            }, Formatting.Indented);
            File.WriteAllText(indexPath, indexJson, Utf8WithoutBom);
            results.Insert(0, ("License/licenses.json", indexPath));
            return results;
        }

        private static string CreateUniqueLicenseFileName(LicenseModel license, HashSet<string> usedFileNames)
        {
            string baseName = SanitizeFileName(license.MacAddress);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = license.Id > 0 ? $"license-{license.Id}" : "license";

            string candidate = $"{baseName}.lic";
            for (int suffix = 2; !usedFileNames.Add(candidate); suffix++)
                candidate = $"{baseName}-{suffix}.lic";
            return candidate;
        }

        internal static string SanitizeFileName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new(value.Trim().Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
            return sanitized.TrimEnd('.', ' ');
        }

        private static (string EntryPath, string FilePath) CreateErrorFile(Exception exception)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_LicenseError_{Guid.NewGuid():N}.txt");
            File.WriteAllText(
                tempPath,
                $"许可证信息收集失败{Environment.NewLine}时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}原因：{exception.GetType().Name}: {exception.Message}",
                Utf8WithoutBom);
            return ("License/collection-error.txt", tempPath);
        }
    }
}
