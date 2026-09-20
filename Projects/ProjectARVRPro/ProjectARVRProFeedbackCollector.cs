using ColorVision.Database;

namespace ProjectARVRPro
{
    public sealed class ProjectARVRProFeedbackCollector : SqliteFeedbackCollector
    {
        public override string Name => "ARVRPro 测试与阶段耗时记录";
        public override string Description => "测试结果、切图、预处理、流程及结果处理时间，包含压缩结果正文";
        public override int Order => 34;
        protected override string DatabasePath => ViewResultManager.SqliteDbPath;
        protected override string DatabaseFileName => "ProjectARVRPro.db";

        protected override void ExportTables(SqliteFeedbackExport export)
        {
            string[] resultTimes = ["CreateTime", "SwitchRequestedAt", "SwitchAcknowledgedAt", "PictureSwitchStartedAt",
                "PictureSwitchCompletedAt", "PreProcessingCompletedAt", "FlowStartedAt", "FlowCompletedAt", "ResultProcessingCompletedAt"];
            string groups = export.Recent("ObjectiveTestResultRecord", "src", ["CreateTime", "UpdateTime"]);
            if (export.HasTable("ARVRReuslt"))
                groups += " OR src.ResultId IN (SELECT result.Id FROM source.ARVRReuslt result WHERE "
                    + export.Recent("ARVRReuslt", "result", resultTimes) + ")";
            export.ExportTable("ObjectiveTestResultRecord", groups);

            string results = export.Recent("ARVRReuslt", "src", resultTimes);
            if (export.HasTable("ObjectiveTestResultRecord"))
                results += " OR src.Id IN (SELECT ResultId FROM main.ObjectiveTestResultRecord)";
            export.ExportTable("ARVRReuslt", results);
        }
    }
}
