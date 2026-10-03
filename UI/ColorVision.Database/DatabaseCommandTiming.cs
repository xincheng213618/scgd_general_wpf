using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Diagnostics;

namespace ColorVision.Database;

/// <summary>Measures connection acquisition separately without changing transaction boundaries.</summary>
public static class DatabaseCommandTiming
{
    private static readonly ILog log = LogManager.GetLogger(typeof(DatabaseCommandTiming));

    public static int Execute(SqlSugarClient db, string operation, Func<int> command, string? correlationId = null)
    {
        long started = Stopwatch.GetTimestamp();
        double? openMs = null;
        double? executeMs = null;
        int? result = null;
        string stage = "OpenConnection";
        bool succeeded = false;
        try
        {
            try { db.Ado.Open(); }
            finally { openMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds; }
            stage = "Execute";
            long commandStarted = Stopwatch.GetTimestamp();
            try { result = command(); }
            finally { executeMs = Stopwatch.GetElapsedTime(commandStarted).TotalMilliseconds; }
            succeeded = true;
            return result.Value;
        }
        finally
        {
            double totalMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (log.IsInfoEnabled && (!succeeded || totalMs >= 100))
            {
                log.Info(JsonConvert.SerializeObject(new
                {
                    Event = "DatabaseCommandTiming",
                    ProcessId = Environment.ProcessId,
                    DatabaseType = db.CurrentConnectionConfig.DbType.ToString(),
                    Operation = operation,
                    CorrelationId = correlationId,
                    Result = result,
                    Succeeded = succeeded,
                    FailureStage = succeeded ? null : stage,
                    OpenConnectionMs = openMs.HasValue ? Math.Round(openMs.Value, 3) : (double?)null,
                    // Includes the server's implicit commit and client-side command/connection handling.
                    ExecuteMs = executeMs.HasValue ? Math.Round(executeMs.Value, 3) : (double?)null,
                    TotalMs = Math.Round(totalMs, 3),
                }));
            }
        }
    }
}
