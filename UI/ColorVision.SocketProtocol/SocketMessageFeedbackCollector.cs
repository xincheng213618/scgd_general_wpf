using ColorVision.Database;

namespace ColorVision.SocketProtocol
{
    public sealed class SocketMessageFeedbackCollector : SqliteFeedbackCollector
    {
        public override string Name => "Socket 通信记录";
        public override string Description => "收发时间、方向和完整消息正文，用于分析外部控制与响应耗时";
        public override int Order => 35;
        protected override string DatabasePath => SocketMessageManager.SqliteDbPath;
        protected override string DatabaseFileName => "SocketMessages.db";

        protected override void ExportTables(SqliteFeedbackExport export) =>
            export.ExportTable("SocketMessage", export.Recent("SocketMessage", "src", ["MessageTime"]));
    }
}
