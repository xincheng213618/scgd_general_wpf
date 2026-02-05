using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class NodeConfiguratorAttribute : Attribute
    {
        public Type NodeType { get; }
        public NodeConfiguratorAttribute(Type nodeType) => NodeType = nodeType;
    }

    public interface INodeConfigurator
    {
        void Configure(NodeConfiguratorContext nodeConfiguratorContext);
    }

    public abstract class NodeConfiguratorBase: INodeConfigurator
    {
        public abstract void Configure(NodeConfiguratorContext nodeConfiguratorContext);
    }

    public class NodeConfiguratorContext
    {
        public STNode STNode { get; set; }

        public StackPanel StackPanel { get; set; }

        public STNodePropertyGrid STNodePropertyGrid { get; set; }
    }
}
