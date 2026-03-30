using log4net;
using SqlSugar;
using System.IO;

namespace Spectrum.License
{
    /// <summary>
    /// SQLite-backed license record for tracking imported license files.
    /// </summary>
    [SugarTable("LicenseRecords")]
    public class LicenseRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// License file name (e.g. "ABCD1234.lic")
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 hash of the file content for change detection.
        /// </summary>
        public string FileHash { get; set; } = string.Empty;

        /// <summary>
        /// When this record was first imported.
        /// </summary>
        public DateTime ImportedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSize { get; set; }
    }

    /// <summary>
    /// Manages a SQLite database that stores license file metadata.
    /// On each spectrometer connection, compares DB records with the local license/ directory
    /// and copies any missing or updated files.
    /// </summary>
    public class LicenseDatabase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LicenseDatabase));
        private static readonly object _locker = new();
        private static LicenseDatabase? _instance;

        public static LicenseDatabase Instance
        {
            get
            {
                lock (_locker)
                {
                    _instance ??= new LicenseDatabase();
                    return _instance;
                }
            }
        }

        private readonly string _dbPath;

        public LicenseDatabase()
        {
            string appDataDir = LicenseSync.GlobalLicenseDir;
            Directory.CreateDirectory(appDataDir);
            _dbPath = Path.Combine(appDataDir, "licenses.db");
            InitializeDatabase();
        }

        private SqlSugarClient CreateClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={_dbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        private void InitializeDatabase()
        {
            try
            {
                using var db = CreateClient();
                db.CodeFirst.InitTables<LicenseRecord>();
            }
            catch (Exception ex)
            {
                log.Error("初始化许可证数据库失败", ex);
            }
        }

        /// <summary>
        /// Import a license file into the database and copy it to the local license directory.
        /// </summary>
        public bool ImportLicense(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath)) return false;

            try
            {
                string fileName = Path.GetFileName(sourceFilePath);
                string hash = ComputeFileHash(sourceFilePath);
                long fileSize = new FileInfo(sourceFilePath).Length;

                // Copy to local license dir
                string localDir = LicenseSync.LocalLicenseDir;
                Directory.CreateDirectory(localDir);
                string destPath = Path.Combine(localDir, fileName);
                File.Copy(sourceFilePath, destPath, true);

                // Upsert into DB
                using var db = CreateClient();
                var existing = db.Queryable<LicenseRecord>().First(r => r.FileName == fileName);
                if (existing != null)
                {
                    existing.FileHash = hash;
                    existing.FileSize = fileSize;
                    existing.ImportedAt = DateTime.Now;
                    db.Updateable(existing).ExecuteCommand();
                }
                else
                {
                    db.Insertable(new LicenseRecord
                    {
                        FileName = fileName,
                        FileHash = hash,
                        FileSize = fileSize,
                        ImportedAt = DateTime.Now
                    }).ExecuteCommand();
                }

                log.Info($"许可证已导入: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"导入许可证失败: {sourceFilePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Sync licenses from DB to local license/ directory.
        /// If a license in the global dir (tracked by DB) is missing or different locally, copy it.
        /// </summary>
        public void SyncToLocal()
        {
            try
            {
                string localDir = LicenseSync.LocalLicenseDir;
                string globalDir = LicenseSync.GlobalLicenseDir;
                Directory.CreateDirectory(localDir);

                using var db = CreateClient();
                var records = db.Queryable<LicenseRecord>().ToList();

                foreach (var record in records)
                {
                    string globalFile = Path.Combine(globalDir, record.FileName);
                    string localFile = Path.Combine(localDir, record.FileName);

                    // If the global file exists and local is missing or different, copy it
                    if (File.Exists(globalFile))
                    {
                        bool needsCopy = !File.Exists(localFile);
                        if (!needsCopy)
                        {
                            string localHash = ComputeFileHash(localFile);
                            needsCopy = localHash != record.FileHash;
                        }

                        if (needsCopy)
                        {
                            File.Copy(globalFile, localFile, true);
                            log.Info($"许可证已同步到本地: {record.FileName}");
                        }
                    }
                }

                // Also scan local dir for any .lic files not in DB and register them
                if (Directory.Exists(localDir))
                {
                    foreach (var file in Directory.GetFiles(localDir, "*.lic"))
                    {
                        string fileName = Path.GetFileName(file);
                        var existing = records.FirstOrDefault(r => r.FileName == fileName);
                        if (existing == null)
                        {
                            string hash = ComputeFileHash(file);
                            long fileSize = new FileInfo(file).Length;
                            db.Insertable(new LicenseRecord
                            {
                                FileName = fileName,
                                FileHash = hash,
                                FileSize = fileSize,
                                ImportedAt = DateTime.Now
                            }).ExecuteCommand();

                            // Also copy to global dir
                            string globalFile = Path.Combine(globalDir, fileName);
                            File.Copy(file, globalFile, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("许可证同步失败", ex);
            }
        }

        /// <summary>
        /// Get all license records from the database.
        /// </summary>
        public List<LicenseRecord> GetAllRecords()
        {
            try
            {
                using var db = CreateClient();
                return db.Queryable<LicenseRecord>().ToList();
            }
            catch (Exception ex)
            {
                log.Error("获取许可证记录失败", ex);
                return new List<LicenseRecord>();
            }
        }

        /// <summary>
        /// Remove a license record and optionally delete the file.
        /// </summary>
        public bool RemoveLicense(string fileName, bool deleteFile = true)
        {
            try
            {
                using var db = CreateClient();
                db.Deleteable<LicenseRecord>().Where(r => r.FileName == fileName).ExecuteCommand();

                if (deleteFile)
                {
                    string localPath = Path.Combine(LicenseSync.LocalLicenseDir, fileName);
                    if (File.Exists(localPath)) File.Delete(localPath);

                    string globalPath = Path.Combine(LicenseSync.GlobalLicenseDir, fileName);
                    if (File.Exists(globalPath)) File.Delete(globalPath);
                }

                log.Info($"许可证已删除: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"删除许可证失败: {fileName}", ex);
                return false;
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
