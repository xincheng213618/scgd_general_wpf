using ColorVision.Engine.Services;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    public interface INodeConfigurator
    {
        void Configure(NodeConfiguratorContext context);
    }

    public abstract class NodeConfiguratorBase : INodeConfigurator
    {
        public abstract void Configure(NodeConfiguratorContext context);
    }

    public abstract class NodeConfiguratorBase<TNode> : NodeConfiguratorBase where TNode : STNode
    {
        public sealed override void Configure(NodeConfiguratorContext context)
        {
            if (context.Node is not TNode node)
                throw new InvalidOperationException($"Configurator {GetType().Name} expected {typeof(TNode).FullName}, but received {context.Node?.GetType().FullName ?? "<null>"}.");

            Configure(node, context.Panels);
        }

        protected abstract void Configure(TNode node, NodePanelBuilder panels);
    }

    public abstract class DeviceOnlyNodeConfigurator<TNode, TDevice> : NodeConfiguratorBase<TNode>
        where TNode : CVCommonNode
        where TDevice : DeviceService
    {
        protected override void Configure(TNode node, NodePanelBuilder panels)
        {
        }
    }
}
