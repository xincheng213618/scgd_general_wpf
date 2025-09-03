using SqlSugar;
using System;

namespace ColorVision.Database.SqliteLog
{
    public class LogEntry:IPKModel
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? Thread { get; set; }
        [SugarColumn( IsNullable = true)]
        public string? Level { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string? Logger { get; set; }
        [SugarColumn(IsNullable = true ,ColumnDataType ="text")]
        public string? Message { get; set; } 
        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string? Exception { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;
    }
}
