#pragma warning disable CS8625
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.UI;
using FlowEngineLib;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    public class NodeConfiguratorContext
    {
        private static readonly ConditionalWeakTable<CVCommonNode, Dictionary<string, FlowEngineNodeEvent>> _nodeEventHandlers = new();
        private NodePanelBuilder? _panels;

        public STNode Node { get; set; }
        public StackPanel SignStackPanel { get; set; }
        public STNodeEditor STNodeEditor { get; set; }
        public StackPanel PropertyStackPanel { get; set; }
        public Action OnActiveChanged { get; set; }
        public NodePanelBuilder Panels => _panels ??= new NodePanelBuilder(this);

        public void RefreshPropertyEditor()
        {
            PropertyStackPanel?.Children.Clear();
            var resourceManager = PropertyEditorHelper.GetResourceManager(Node);
            PropertyStackPanel?.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(
                Node,
                resourceManager,
                metadataProvider: FlowNodePropertyMetadataProvider.Instance));
        }

        public void RebindNodeEvent(CVCommonNode node, string key, Action refresh)
        {
            if (Node == null)
                throw new InvalidOperationException("Cannot bind node event before the active node is set.");

            var handlers = _nodeEventHandlers.GetValue(node, _ => new Dictionary<string, FlowEngineNodeEvent>());
            lock (handlers)
            {
                if (handlers.TryGetValue(key, out var previousHandler))
                    node.nodeEvent -= previousHandler;

                FlowEngineNodeEvent handler = (_, _) => refresh();
                handlers[key] = handler;
                node.nodeEvent += handler;
            }
        }

        public void AddTemplateCollectionPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ObservableCollection<TemplateModel<T>> itemSource) where T : ParamModBase =>
            Panels.AddTemplateCollectionPanel(updateStorageAction, tempName, signName, itemSource);

        public void AddTemplateJsonPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplateJson<T> template) where T : TemplateJsonParam, new() =>
            Panels.AddTemplateJsonPanel(updateStorageAction, tempName, signName, template);

        public void AddTemplatePanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplate<T> template) where T : ParamModBase, new() =>
            Panels.AddTemplatePanel(updateStorageAction, tempName, signName, template);
    }
}
