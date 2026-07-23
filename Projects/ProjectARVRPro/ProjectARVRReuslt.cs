using ColorVision.Database;
using ColorVision.Engine.Templates.Flow;
using SqlSugar;

namespace ProjectARVRPro
{
    /// <summary>
    /// ARVR 测试结果实体（纯数据模型，不含 UI / 数据库操作）
    /// </summary>
    [SugarTable("ARVRReuslt")]
    public class ProjectARVRReuslt : ViewEntity 
    {
        public int BatchId { get => _BatchId; set { _BatchId = value; OnPropertyChanged(); } }
        private int _BatchId;

        public string Model { get; set; } = string.Empty;

        [SugarColumn(IsNullable = true)]
        public string? FileName { get; set; }
        public string SN { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public FlowStatus FlowStatus { get; set; } = FlowStatus.Ready;
        public bool Result { get; set; } = true;
        public int TestType { get; set; }
        public long RunTime { get; set; }
        public string Msg { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 执行此结果解析的 IProcess 完整类型名。
        /// 历史结果使用该字段恢复解析器，不依赖当前流程组中的模板映射。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string? ProcessTypeFullName { get; set; }

        /// <summary>
        /// 执行时的流程解析配置快照，用于稳定重放历史叠图和结果文本。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string? ProcessConfigJson { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? ViewResultJson { get; set; }
    }
}
