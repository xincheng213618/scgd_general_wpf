using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Templates.Flow;
using ProjectARVRPro.PluginConfig;
using System.Linq;

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
            if (ctx?.Batch == null)
                return false;

            string flowName = ctx.FlowName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(flowName) && ctx.Batch.TId is int templateId)
            {
                flowName = TemplateFlow.Params.FirstOrDefault(template => template.Id == templateId)?.Key ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(flowName))
                return false;

            ProjectARVRWindowHost.ShowBatchResult(ctx.Batch, flowName);
            return true;
        }
    }
}
