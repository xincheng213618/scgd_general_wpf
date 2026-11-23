using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ProjectARVRPro.PluginConfig;

namespace ProjectARVRPro.Process
{
    [PostProcess(
        "ARVR结果查看",
        "流程完成后打开ARVR窗口，按原有解析、叠图和文本逻辑显示当前批次结果",
        Category = PostProcessTypeCatalog.ArvrCategory,
        Order = 0)]
    public sealed class ArvrWindowPostProcessor : IPostProcessor
    {
        public bool Process(PostProcessContext ctx)
        {
            if (ctx?.Batch is not { Id: > 0 } batch)
                return false;

            var openDatabase = ArvrBatchHistory.CaptureDatabase(batch);
            if (openDatabase == null)
                return false;

            string flowName = ctx.FlowName?.Trim() ?? string.Empty;
            if (flowName.Length == 0 && batch.TId is int templateId && templateId > 0)
            {
                using var db = openDatabase();
                flowName = db.Queryable<ModMasterModel>()
                    .Where(template => template.Id == templateId && template.Pid == 11 && template.TenantId == 0 && !template.IsDelete)
                    .Select(template => template.Name).First()?.Trim() ?? string.Empty;
            }
            if (flowName.Length == 0)
                return false;

            ProjectARVRWindowHost.ShowBatchResult(batch, flowName, openDatabase);
            return true;
        }
    }
}
