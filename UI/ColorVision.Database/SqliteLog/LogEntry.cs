using SqlSugar;
using System;

namespace ColorVision.Database.SqliteLog
{
    public class LogEntry
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Thread { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Logger { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Exception { get; set; } = string.Empty;
    }
}
