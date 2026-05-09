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
        public string FileName { get; set; } = string.Empty;
        public string SN { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        public FlowStatus FlowStatus { get; set; } = FlowStatus.Ready;
        public bool Result { get; set; } = true;
        public int TestType { get; set; }
        public long RunTime { get; set; }
        public string Msg { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; } = DateTime.Now;

        [SugarColumn(IsNullable = true)]
        public string ViewResultJson { get; set; } 
    }
}