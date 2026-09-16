using ColorVision.Database;
using ColorVision.UI;

namespace ColorVision.Engine.Messages
{
    public sealed class MsgRecordFeedbackCollector : SqliteFeedbackCollector
    {
        public override string Name => "MQTT 服务通信记录";
        public override string Description => "服务请求、响应、超时状态及完整消息正文，用于分析服务通信耗时";
        public override int Order => 36;
        protected override string DatabasePath => ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>().SqliteDbPath;
        protected override string DatabaseFileName => "MsgRecords.db";

        protected override void ExportTables(SqliteFeedbackExport context) =>
            context.ExportTable("MsgRecord", context.Recent("MsgRecord", "src", ["SendTime", "ReciveTime", "CreateTime", "UpdateTime"]));
    }
}
