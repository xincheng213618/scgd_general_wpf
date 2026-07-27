using System;

namespace ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class NodeConfiguratorAttribute : Attribute
    {
        public Type NodeType { get; }
        public NodeConfiguratorAttribute(Type nodeType) => NodeType = nodeType;
    }
}
