using ColorVision.UI;
using log4net;
using MySqlConnector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;

namespace ColorVision.Database;

public sealed class CameraCaptureTimingFeedbackCollector : IFeedbackLogCollector, IFeedbackLogTimeRangeCollector
{
    private const string SourceTable = "t_scgd_measure_result_img";
    private static readonly ILog log = LogManager.GetLogger(typeof(CameraCaptureTimingFeedbackCollector));

    public string Name => "相机取图与校正耗时";
    public string Description => "按时间导出已保存的相机结果，包括图像文件名、曝光、校正模板和各阶段用时（毫秒）";
    public int Order => 34;
    public bool IsSelectedByDefault => true;
    public int RecentDays { get; set; } = 7;
    public string? LogDirectory => null;

    public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ColorVision_Feedback_{Guid.NewGuid():N}.json");
        DateTimeOffset to = DateTimeOffset.Now;
        try
        {
            using var connection = new MySqlConnection(MySqlControl.GetConnectionString());
            connection.Open();
            using var output = File.CreateText(path);
            WriteExport(connection, output, to, RecentDays);
        }
        catch (Exception ex)
        {
            log.Warn("反馈诊断包收集相机取图与校正耗时失败。", ex);
            File.WriteAllText(path, JsonConvert.SerializeObject(new
            {
                SourceTable, From = to.AddDays(-RecentDays), To = to, Status = "error",
                Error = ex.GetType().Name, MySqlErrorCode = (ex as MySqlException)?.Number
            }));
        }
        return [("Database/CameraCaptureTiming.json", path)];
    }

    internal static void WriteExport(DbConnection connection, TextWriter output, DateTimeOffset to, int recentDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recentDays);
        DateTimeOffset from = to.AddDays(-recentDays);
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, batch_id, create_date, device_code, raw_file, total_time, `params`
            FROM {SourceTable}
            WHERE create_date BETWEEN @from AND @to
            ORDER BY create_date, id
            """;
        foreach (var (name, time) in new[] { ("@from", from.DateTime), ("@to", to.DateTime) })
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = time;
            command.Parameters.Add(parameter);
        }
        using var reader = command.ExecuteReader();
        using var writer = new JsonTextWriter(output);
        var serializer = new JsonSerializer();
        writer.WriteStartObject();
        writer.WritePropertyName("SourceTable");
        writer.WriteValue(SourceTable);
        writer.WritePropertyName("From");
        writer.WriteValue(from);
        writer.WritePropertyName("To");
        writer.WriteValue(to);
        writer.WritePropertyName("Status");
        writer.WriteValue("ok");
        writer.WritePropertyName("Rows");
        writer.WriteStartArray();
        long count = 0;
        while (reader.Read())
        {
            JObject? parameters = null;
            if (!reader.IsDBNull(6))
            {
                // Older records may contain plain text or a different JSON shape.
                try { parameters = JObject.Parse(reader.GetString(6)); }
                catch (JsonReaderException) { }
            }
            var timing = parameters?["Timing"] as JObject;
            serializer.Serialize(writer, new
            {
                Id = reader[0], BatchId = reader[1], CreatedAt = reader.GetDateTime(2),
                DeviceCode = reader[3], RawFile = reader.IsDBNull(4) ? null : Path.GetFileName(reader.GetString(4)),
                TotalTimeMs = reader[5], CalibrationTemplate = (parameters?["Calibration"] as JObject)?["Name"],
                ExposureMs = parameters?["ExpTime"], Timing = timing == null ? null : new
                {
                    Capture = timing["Capture"], Calibration = timing["Calibration"], Save = timing["Save"], Total = timing["Total"]
                }
            });
            count++;
        }
        writer.WriteEndArray();
        writer.WritePropertyName("Count");
        writer.WriteValue(count);
        writer.WriteEndObject();
    }
}
