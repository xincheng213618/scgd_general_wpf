namespace ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration
{
    public interface INodeConfigurator
    {
        void Configure(NodeConfiguratorContext context);
    }

    public abstract class NodeConfiguratorBase : INodeConfigurator
    {
        public abstract void Configure(NodeConfiguratorContext context);
    }
}
