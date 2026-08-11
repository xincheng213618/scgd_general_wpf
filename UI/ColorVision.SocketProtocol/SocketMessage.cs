#pragma warning disable CS8618
using ColorVision.Database;
using SqlSugar;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket消息方向
    /// </summary>
    public enum SocketMessageDirection
    {
        [Display(Name = "Socket_Receive", ResourceType = typeof(Properties.Resources))]
        Received,
        [Display(Name = "Socket_Send", ResourceType = typeof(Properties.Resources))]
        Sent
    }

    /// <summary>
    /// Socket消息实体类，用于SQLite持久化
    /// </summary>
    [SugarTable("SocketMessage")]
    public class SocketMessage : ViewEntity
    {
        /// <summary>
        /// 客户端地址
        /// </summary>
        [SugarColumn(ColumnName = "ClientEndPoint", IsNullable = true)]
        public string ClientEndPoint { get; set; }

        /// <summary>
        /// 消息方向
        /// </summary>
        [SugarColumn(ColumnName = "Direction")]
        public SocketMessageDirection Direction { get; set; }

        [SugarColumn(IsIgnore = true)]
        public string DirectionDisplay => Direction == SocketMessageDirection.Received
            ? Properties.Resources.FilterReceived
            : Properties.Resources.FilterSent;

        /// <summary>
        /// 消息内容。正文仅在新增消息或明确查看、复制、重发时存在于内存，
        /// SQLite 长期存储只使用未映射到列表实体的 GZip BLOB。
        /// </summary>
        [Browsable(false)]
        [SugarColumn(IsIgnore = true)]
        public string? Content
        {
            get => _content;
            set
            {
                _content = value;
                IsContentLoaded = true;
                OnPropertyChanged();
            }
        }
        private string? _content;

        /// <summary>
        /// 列表使用的短预览，避免查询或解压完整正文。
        /// </summary>
        [SugarColumn(ColumnName = "ContentPreview", IsNullable = true, ColumnDataType = "text")]
        public string? ContentPreview { get; set; }

        [Browsable(false)]
        [SugarColumn(IsIgnore = true)]
        public bool IsContentLoaded { get; private set; }

        /// <summary>
        /// 消息时间
        /// </summary>
        [SugarColumn(ColumnName = "MessageTime")]
        public DateTime MessageTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 事件名称(JSON消息的EventName字段)
        /// </summary>
        [SugarColumn(ColumnName = "EventName", IsNullable = true)]
        public string? EventName { get; set; }

        /// <summary>
        /// 消息ID(JSON消息的MsgID字段)
        /// </summary>
        [SugarColumn(ColumnName = "MsgID", IsNullable = true)]
        public string? MsgID { get; set; }

        /// <summary>
        /// 响应码(响应消息的Code字段)
        /// </summary>
        [SugarColumn(ColumnName = "ResponseCode", IsNullable = true)]
        public int? ResponseCode { get; set; }

        public override string ToString()
        {
            var directionText = Direction == SocketMessageDirection.Received ? "←" : "→";
            var shortContent = ContentPreview?.Length > 50
                ? string.Concat(ContentPreview.AsSpan(0, 50), "...")
                : ContentPreview;
            return $"[{MessageTime:HH:mm:ss}] {directionText} {shortContent}";
        }
    }
}
