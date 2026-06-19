using SqlSugar;

namespace ColorVision.UI.Desktop.Download
{
    internal sealed class DownloadTaskStore
    {
        private readonly string _dbPath;

        public DownloadTaskStore(string dbPath)
        {
            _dbPath = dbPath;
        }

        public static SqlSugarClient CreateDbClient(string dbPath)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={dbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
        }

        public void Initialize()
        {
            using var db = CreateDbClient(_dbPath);
            db.CodeFirst.InitTables<DownloadEntry>();
        }

        public List<DownloadEntry> GetIncompleteEntries()
        {
            using var db = CreateDbClient(_dbPath);
            return db.Queryable<DownloadEntry>()
                .Where(x => x.Status == (int)DownloadStatus.Waiting || x.Status == (int)DownloadStatus.Downloading || x.Status == (int)DownloadStatus.Paused)
                .ToList();
        }

        public int Insert(DownloadEntry entry)
        {
            using var db = CreateDbClient(_dbPath);
            return db.Insertable(entry).ExecuteReturnIdentity();
        }

        public List<DownloadEntry> GetCompletedEntriesByUrl(string url)
        {
            using var db = CreateDbClient(_dbPath);
            return db.Queryable<DownloadEntry>()
                .Where(x => x.Status == (int)DownloadStatus.Completed && x.Url == url)
                .OrderByDescending(x => x.CompleteTime)
                .ToList();
        }

        public void Delete(int id)
        {
            using var db = CreateDbClient(_dbPath);
            db.Deleteable<DownloadEntry>().In(id).ExecuteCommand();
        }

        public void DeleteMany(int[] ids)
        {
            using var db = CreateDbClient(_dbPath);
            db.Deleteable<DownloadEntry>().In(ids).ExecuteCommand();
        }

        public void Clear()
        {
            using var db = CreateDbClient(_dbPath);
            db.Deleteable<DownloadEntry>().ExecuteCommand();
        }

        public List<DownloadEntry> LoadRecords(string? searchKeyword, int pageSize, int page)
        {
            using var db = CreateDbClient(_dbPath);
            var query = db.Queryable<DownloadEntry>();

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                query = query.Where(x => x.FileName.Contains(searchKeyword) || x.Url.Contains(searchKeyword));
            }

            return query.OrderByDescending(x => x.CreateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public int GetTotalCount(string? searchKeyword)
        {
            using var db = CreateDbClient(_dbPath);
            var query = db.Queryable<DownloadEntry>();
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                query = query.Where(x => x.FileName.Contains(searchKeyword) || x.Url.Contains(searchKeyword));
            }
            return query.Count();
        }

        public void UpdateStatus(int id, DownloadStatus status, string? errorMessage = null)
        {
            using var db = CreateDbClient(_dbPath);
            db.Updateable<DownloadEntry>()
                .SetColumns(x => x.Status == (int)status)
                .SetColumns(x => x.ErrorMessage == errorMessage)
                .Where(x => x.Id == id)
                .ExecuteCommand();
        }

        public void UpdateBytes(int id, long totalBytes, long downloadedBytes)
        {
            using var db = CreateDbClient(_dbPath);
            db.Updateable<DownloadEntry>()
                .SetColumns(x => x.TotalBytes == totalBytes)
                .SetColumns(x => x.DownloadedBytes == downloadedBytes)
                .Where(x => x.Id == id)
                .ExecuteCommand();
        }

        public void UpdateFileName(int id, string fileName)
        {
            using var db = CreateDbClient(_dbPath);
            db.Updateable<DownloadEntry>()
                .SetColumns(x => x.FileName == fileName)
                .Where(x => x.Id == id)
                .ExecuteCommand();
        }

        public void MarkCompleted(int id, long totalBytes, long downloadedBytes, DateTime completeTime)
        {
            using var db = CreateDbClient(_dbPath);
            db.Updateable<DownloadEntry>()
                .SetColumns(x => x.Status == (int)DownloadStatus.Completed)
                .SetColumns(x => x.TotalBytes == totalBytes)
                .SetColumns(x => x.DownloadedBytes == downloadedBytes)
                .SetColumns(x => x.CompleteTime == completeTime)
                .Where(x => x.Id == id)
                .ExecuteCommand();
        }
    }
}
