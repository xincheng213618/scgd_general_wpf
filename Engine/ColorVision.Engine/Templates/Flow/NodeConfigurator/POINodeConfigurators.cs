using ColorVision.Engine.Properties;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine.Templates.Jsons.BuildPOIAA;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [NodeConfigurator(typeof(FlowEngineLib.BuildPOINode))]
    public class BuildPOINodeConfigurator : NodeConfiguratorBase
    {
        public override void Configure(NodeConfiguratorContext context)
        {
            var node = (FlowEngineLib.BuildPOINode)context.Node;
            context.AddTemplatePanel(name => node.TemplateName = name, node.TemplateName, Properties.Resources.BuildTemplate, new TemplateBuildPoi());
            context.AddTemplateJsonPanel(name => node.TemplateName = name, node.TemplateName, "ABuildPOIAAA", new TemplateBuildPOIAA());
        }
    }
}
