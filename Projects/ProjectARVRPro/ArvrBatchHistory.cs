using ColorVision.Database;
using ColorVision.Engine;
using log4net;
using MySqlConnector;
using SqlSugar;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProjectARVRPro;

internal static class ArvrBatchHistory
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ArvrBatchHistory));
    private static readonly Func<BatchResultMasterDao, int, MeasureBatchModel?>? ReadBoundBatch =
        typeof(BatchResultMasterDao).GetMethod(nameof(BatchResultMasterDao.GetByIdForHistory), [typeof(int)])
            ?.CreateDelegate<Func<BatchResultMasterDao, int, MeasureBatchModel?>>();
    private static readonly Func<BatchResultMasterDao, MeasureBatchModel, Func<SqlSugarClient>?>? ReadOwnedDatabase =
        typeof(BatchResultMasterDao).GetMethod("TryGetOwnedDatabaseFactory", [typeof(MeasureBatchModel)])
            ?.CreateDelegate<Func<BatchResultMasterDao, MeasureBatchModel, Func<SqlSugarClient>?>>();

    internal static Func<SqlSugarClient>? CaptureDatabase(MeasureBatchModel batch) => ReadOwnedDatabase == null
        ? CaptureConfiguredDatabase()
        : ReadOwnedDatabase(BatchResultMasterDao.Instance, batch);

    internal static string? GetDatabaseKey(Func<SqlSugarClient> openBatchDatabase)
    {
        try
        {
            using var db = openBatchDatabase();
            var config = db.CurrentConnectionConfig;
            if (config.DbType != DbType.MySql) return null;
            var mysql = new MySqlConnectionStringBuilder(config.ConnectionString);
            if (string.IsNullOrWhiteSpace(mysql.Server) || mysql.Server.Contains(',') || string.IsNullOrWhiteSpace(mysql.Database))
                return null;
            string[] parts = [mysql.Server.Trim().ToUpperInvariant(), mysql.Port.ToString(CultureInfo.InvariantCulture), mysql.Database];
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
        }
        catch (Exception error)
        {
            log.Warn($"无法识别 ARVR 批次数据库，跳过本地结果复用。错误类型：{error.GetType().Name}");
            return null;
        }
    }

    internal static bool ContainsBatch(MeasureBatchModel batch, Func<SqlSugarClient> openBatchDatabase)
    {
        using var db = openBatchDatabase();
        return ContainsBatch(db, batch);
    }

    internal static IReadOnlyList<MeasureResultImgModel> ReadCapturedImages(MeasureBatchModel batch, Func<SqlSugarClient> openBatchDatabase)
    {
        using var db = openBatchDatabase();
        if (!ContainsBatch(db, batch)) return [];
        return db.Queryable<MeasureResultImgModel>().Where(item => item.BatchId == batch.Id).OrderBy(item => item.Id).ToList();
    }

    private static bool ContainsBatch(SqlSugarClient db, MeasureBatchModel batch) => batch.Id > 0
        && (string.IsNullOrWhiteSpace(batch.Code)
            ? !string.IsNullOrWhiteSpace(batch.Name) && db.Queryable<MeasureBatchModel>().Any(row => row.Id == batch.Id && row.Name == batch.Name && row.CreateDate == batch.CreateDate)
            : db.Queryable<MeasureBatchModel>().Any(row => row.Id == batch.Id && row.Code == batch.Code));

    internal static MeasureBatchModel? Resolve(ProjectARVRReuslt? result, Func<int, MeasureBatchModel?>? read = null)
    {
        if (result is not { BatchId: > 0 }) return null;
        bool hasCode = !string.IsNullOrWhiteSpace(result.Code);
        if (!hasCode && string.IsNullOrWhiteSpace(result.SN)) return null;

        try
        {
            Func<SqlSugarClient>? configuredDatabase = ReadBoundBatch == null ? CaptureConfiguredDatabase() : null;
            MeasureBatchModel? batch;
            if (read != null)
                batch = read(result.BatchId);
            else if (configuredDatabase != null)
            {
                using var db = configuredDatabase();
                batch = db.Queryable<MeasureBatchModel>().First(item => item.Id == result.BatchId);
            }
            else
                batch = ReadBoundBatch!(BatchResultMasterDao.Instance, result.BatchId);
            if (batch?.Id != result.BatchId) return null;
            if (!string.IsNullOrWhiteSpace(result.BatchDatabaseKey))
            {
                var openDatabase = configuredDatabase ?? CaptureDatabase(batch);
                if (openDatabase == null || !string.Equals(GetDatabaseKey(openDatabase), result.BatchDatabaseKey, StringComparison.Ordinal))
                    return null;
            }
            bool matches = hasCode
                ? string.Equals(batch.Code, result.Code, StringComparison.Ordinal)
                : string.Equals(batch.Name, result.SN, StringComparison.Ordinal);
            return matches ? batch : null;
        }
        catch (Exception error)
        {
            log.Warn($"流程批次历史读取失败：batchId={result.BatchId}", error);
            return null;
        }
    }

    private static Func<SqlSugarClient> CaptureConfiguredDatabase()
    {
        string connectionString = MySqlControl.GetConnectionString();
        return () => new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DbType.MySql,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    }
}
