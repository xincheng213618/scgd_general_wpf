using SqlSugar;
using System;

namespace ColorVision.Database.SqliteLog
{
    // 添加索引定义
    // IndexName: 索引名称
    // nameof(Date): 字段名
    // Desc: 降序索引（因为日志通常是按时间倒序查，建降序索引能极大加速 OrderBy Date Desc）
    [SugarIndex("Index_Date", nameof(Date), OrderByType.Desc)]
    [SugarIndex("Index_Level", nameof(Level), OrderByType.Asc)]
    public class LogEntry : IEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? Thread { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? Level { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string? Logger { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string? Message { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string? Exception { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;
    }
}