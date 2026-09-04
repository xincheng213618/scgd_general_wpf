using ColorVision.Database;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.FlowProcessing;
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

        /// <summary>
        /// ImageEditor 导出的原位深、无标记原图。算法原图被清理后优先用它恢复底图并重新渲染标记。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string? SavedSourceImageFileName { get; set; }

        /// <summary>
        /// ImageEditor 导出的标记图。没有任何可用原图时直接显示，避免重复绘制标记。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string? SavedResultImageFileName { get; set; }

        /// <summary>
        /// 结果生成时写入 SQLite 的图像坐标空间宽度。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? ImageWidth { get; set; }

        /// <summary>
        /// 结果生成时写入 SQLite 的图像坐标空间高度。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? ImageHeight { get; set; }

        public string SN { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public FlowStatus FlowStatus { get; set; } = FlowStatus.Ready;
        public bool Result { get; set; } = true;
        public int TestType { get; set; }
        public long RunTime { get; set; }
        public string Msg { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 首流程收到 ProjectARVRInit、后续流程发送 SwitchPG 的时间。RunAll 或旧记录可能为空。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? SwitchRequestedAt { get; set; }

        /// <summary>
        /// 外部系统回传 SwitchPGCompleted 的时间。RunAll 或旧记录可能为空。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? SwitchAcknowledgedAt { get; set; }

        /// <summary>
        /// 本地切图服务开始执行的时间；RunAll 使用该字段作为内部切图起点。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? PictureSwitchStartedAt { get; set; }

        /// <summary>
        /// 本地切图服务完成（包括配置的稳定等待）的时间。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? PictureSwitchCompletedAt { get; set; }

        /// <summary>
        /// 流程启动前预处理完成的时间。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? PreProcessingCompletedAt { get; set; }

        /// <summary>
        /// FlowControl 开始执行本流程的时间。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? FlowStartedAt { get; set; }

        /// <summary>
        /// FlowControl 返回本流程终态的时间。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? FlowCompletedAt { get; set; }

        /// <summary>
        /// 客户流程结果解析、结果记录持久化及可选快捷方式处理完成的时间。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? ResultProcessingCompletedAt { get; set; }

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

        /// <summary>
        /// 当前流程或按需加载后的结果 JSON。数据库只保存 GZip 压缩字段，
        /// 避免普通结果列表查询加载大文本。
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public string? ViewResultJson { get; set; }
    }
}
