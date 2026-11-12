using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.PostProcess;
using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

internal sealed record FlowTemplateUsage(long ExecutionCount, double? ExpectedMs, int SampleCount);

/// <summary>Reads retained local run summaries without initializing diagnostics writers or loading payloads.</summary>
internal static class FlowTemplateUsageReader
{
    internal const int EstimateSampleLimit = 20;

    internal static FlowTemplateUsage? Read(string path, FlowIdentity identity, string? contentHash)
    {
        if (identity.IsEmpty || !File.Exists(path)) return null;
        var database = new ReadOnlySqliteDatabase(path);
        const string table = "FlowRunRecord";
        if (!database.HasTable(table)) return null;
        database.RequireColumns(table, "template_id", "status", "elapsed_ms", "completed_time");
        bool hasKey = database.HasColumn(table, "flow_key");
        string filter;
        if (identity.FlowKey != null && hasKey)
            filter = "(flow_key=$key OR ((flow_key IS NULL OR flow_key='') AND $id>0 AND template_id=$id))";
        else if (identity.TemplateId > 0) filter = "template_id=$id";
        else if (identity.FlowKey != null || !database.HasColumn(table, "flow_name")) return null;
        else filter = "template_id<=0 AND " + (hasKey ? "(flow_key IS NULL OR flow_key='') AND " : "") + "flow_name=$name";

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandTimeout = 1;
        command.Parameters.AddWithValue("$key", (object?)identity.FlowKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", identity.TemplateId);
        command.Parameters.AddWithValue("$name", (object?)identity.FlowName ?? DBNull.Value);
        command.CommandText = $"SELECT COUNT(*) FROM FlowRunRecord WHERE {filter}";
        long count = (long)command.ExecuteScalar()!;

        string versionFilter = string.Empty;
        if (!string.IsNullOrEmpty(contentHash))
        {
            // A changed saved version must not inherit a duration estimate from an older version.
            if (!database.HasColumn(table, "content_hash")) return new(count, null, 0);
            versionFilter = " AND content_hash=$hash";
            command.Parameters.AddWithValue("$hash", contentHash);
        }
        string outcomeFilter = database.HasColumn(table, "final_outcome")
            ? $" AND (final_outcome IS NULL OR final_outcome IN ({(int)FlowFinalOutcome.Succeeded},{(int)FlowFinalOutcome.SucceededWithWarnings}))" : string.Empty;
        command.Parameters.AddWithValue("$completed", (int)FlowStatus.Completed);
        command.CommandText = $"""
            SELECT COUNT(*), AVG(elapsed_ms) FROM (
              SELECT elapsed_ms FROM FlowRunRecord WHERE {filter}
                AND status=$completed AND elapsed_ms>=0{versionFilter}{outcomeFilter}
              ORDER BY completed_time DESC LIMIT {EstimateSampleLimit});
            """;
        using var reader = command.ExecuteReader();
        reader.Read();
        int samples = reader.GetInt32(0);
        return new(count, reader.IsDBNull(1) ? null : reader.GetDouble(1), samples);
    }
}
