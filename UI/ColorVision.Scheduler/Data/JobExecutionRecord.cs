using SqlSugar;
using System;

namespace ColorVision.Scheduler.Data
{
    [SugarTable("job_execution_record")]
    [SugarIndex("IX_JobName_Group", nameof(JobName), OrderByType.Asc, nameof(GroupName), OrderByType.Asc)]
    [SugarIndex("IX_StartTime", nameof(StartTime), OrderByType.Desc)]
    public class JobExecutionRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public string JobName { get; set; } = string.Empty;

        public string GroupName { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public long ExecutionTimeMs { get; set; }

        public bool Success { get; set; }

        /// <summary>
        /// 执行结果状态文本（如：成功、失败、Completed、Failed、OverTime 等）
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 64)]
        public string? Result { get; set; }

        /// <summary>
        /// 执行结果详情（如输出参数、错误信息等）
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string? Message { get; set; }
    }
}
