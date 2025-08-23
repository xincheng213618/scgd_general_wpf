using SqlSugar;
using System;

namespace ColorVision.Database.SqliteLog
{
    public class LogEntry
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public DateTime Date { get; set; }

        [SugarColumn(IsPrimaryKey = true, IsNullable = true)]
        public string? Thread { get; set; }
        [SugarColumn(IsPrimaryKey = true, IsNullable = true)]
        public string? Level { get; set; } 
        [SugarColumn(IsPrimaryKey = true, IsNullable = true)]
        public string? Logger { get; set; }
        [SugarColumn(IsPrimaryKey = true, IsNullable = true)]
        public string? Message { get; set; } 
        [SugarColumn(IsPrimaryKey = true, IsNullable = true)]
        public string? Exception { get; set; }
    }
}
