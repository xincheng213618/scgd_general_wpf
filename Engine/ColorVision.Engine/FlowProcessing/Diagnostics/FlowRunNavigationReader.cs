using SqlSugar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

internal readonly record struct FlowRunNavigationSummary(
    int BatchId, string? SerialNumber, DateTime FirstStartTime, int FirstRecordId);

internal static class FlowRunNavigationReader
{
    // Local SQLite timestamps use zero-padded, timezone-free text. Their binary
    // order matches DateTime, including fractional seconds. Project other formats
    // row by row so the navigation can compare the parsed dates as before.
    private const string CanonicalTime = """
        typeof(start_time) = 'text'
        AND substr(start_time, 1, 19) GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9] [0-9][0-9]:[0-9][0-9]:[0-9][0-9]'
        AND (length(start_time) = 19 OR (
            length(start_time) BETWEEN 21 AND 27
            AND substr(start_time, 20, 1) = '.'
            AND substr(start_time, 21) NOT GLOB '*[^0-9]*'))
        """;

    internal static List<FlowRunNavigationSummary> ReadRecent(SqlSugarClient db, int batchLimit, CancellationToken cancellationToken)
    {
        if (batchLimit <= 0)
            return [];
        return Read(db, """
            batch_id IN (SELECT batch_id FROM FlowNodeRecord GROUP BY batch_id ORDER BY batch_id DESC LIMIT @limit)
            """, [new SugarParameter("@limit", batchLimit)], cancellationToken);
    }

    internal static List<FlowRunNavigationSummary> ReadBySerials(SqlSugarClient db, string[] serials, CancellationToken cancellationToken)
    {
        if (serials.Length == 0)
            return [];
        SugarParameter[] parameters = serials.Select((serial, index) => new SugarParameter($"@serial{index}", serial)).ToArray();
        string names = string.Join(",", parameters.Select(parameter => parameter.ParameterName));
        return Read(db, $"serial_number IN ({names})", parameters, cancellationToken);
    }

    private static List<FlowRunNavigationSummary> Read(SqlSugarClient db, string filter,
        SugarParameter[] parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = db.Ado.GetDataReader($"""
            SELECT batch_id, serial_number, MIN(start_time COLLATE BINARY), MIN(id)
            FROM FlowNodeRecord
            WHERE {filter}
            GROUP BY batch_id, serial_number COLLATE BINARY, CASE WHEN {CanonicalTime} THEN NULL ELSE id END
            """, parameters);
        var rows = new List<FlowRunNavigationSummary>();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            // SqlSugar's entity mapping reads dates as converted values. The SQLite
            // reader's typed getter interprets offset timestamps in UTC instead.
            DateTime firstStart = reader.IsDBNull(2) ? default : Convert.ToDateTime(reader.GetValue(2), CultureInfo.CurrentCulture);
            rows.Add(new(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                firstStart, reader.GetInt32(3)));
        }
        return rows;
    }
}
