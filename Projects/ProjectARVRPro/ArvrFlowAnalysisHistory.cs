using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.UI;
using Microsoft.Data.Sqlite;
using System.IO;

namespace ProjectARVRPro;

internal static class ArvrFlowAnalysisHistory
{
    internal static MeasureBatchModel ResolveLocalBatch(ProjectARVRReuslt result)
    {
        string path = ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath;
        return new MeasureBatchModel
        {
            Id = result.BatchId,
            Code = ResolveSerialNumber(path, result),
            Name = result.SN,
            CreateDate = result.CreateTime,
            TotalTime = (int)Math.Clamp(result.RunTime, 0, int.MaxValue),
            FlowStatus = result.FlowStatus,
        };
    }

    internal static string ResolveSerialNumber(string databasePath, ProjectARVRReuslt result)
    {
        bool hasCode = !string.IsNullOrWhiteSpace(result.Code);
        if (result.BatchId <= 0 || (!hasCode && string.IsNullOrWhiteSpace(result.SN)))
            throw new InvalidDataException("该记录缺少批次 ID 或运行编号，无法关联流程诊断。");
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("找不到流程诊断库 FlowNodeRecords.db。", databasePath);

        var source = new ReadOnlySqliteDatabase(databasePath);
        string[] tables = ["FlowRunRecord", "FlowNodeRecord", "FlowNodeMessage"];
        var queries = tables.Where(table => source.HasColumn(table, "batch_id") && source.HasColumn(table, "serial_number"))
            .Select(table => $"SELECT serial_number, {(table == "FlowRunRecord" ? 1 : 0)} AS is_run FROM {table} WHERE batch_id = @batchId")
            .ToArray();
        if (queries.Length == 0)
            throw new InvalidDataException("流程诊断库缺少批次 ID 或运行编号字段。");

        using var connection = new SqliteConnection(source.ConnectionString);
        connection.Open();
        using var query = connection.CreateCommand();
        // A saved Code is authoritative. Only records without Code can use the legacy SN convention.
        string match = hasCode
            ? "serial_number COLLATE BINARY = @code"
            : """
              serial_number COLLATE BINARY IN (@sn, @sanitizedSn)
              OR substr(serial_number, 1, length(@prefix)) COLLATE BINARY = @prefix
              """;
        query.CommandText = $"""
            SELECT serial_number, SUM(is_run)
            FROM ({string.Join(" UNION ALL ", queries)})
            WHERE {match}
            GROUP BY serial_number COLLATE BINARY
            LIMIT 2
            """;
        query.Parameters.AddWithValue("@batchId", result.BatchId);
        if (hasCode)
            query.Parameters.AddWithValue("@code", result.Code);
        else
        {
            query.Parameters.AddWithValue("@sn", result.SN);
            query.Parameters.AddWithValue("@sanitizedSn", Tool.SanitizeFileName(result.SN));
            query.Parameters.AddWithValue("@prefix", Tool.SanitizeFileName(result.SN + "_"));
        }

        using var rows = query.ExecuteReader();
        if (!rows.Read())
            throw new InvalidDataException("流程诊断库中没有与该批次和运行编号匹配的记录。");
        string serialNumber = rows.GetString(0);
        if (rows.GetInt64(1) > 1 || rows.Read())
            throw new InvalidDataException("该批次匹配到多个流程运行，无法确定要查看的记录。");
        return serialNumber;
    }
}
