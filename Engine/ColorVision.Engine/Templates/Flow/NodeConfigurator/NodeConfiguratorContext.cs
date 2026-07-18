#pragma warning disable CS8625
using ColorVision.Engine.Templates.Jsons;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    public class NodeConfiguratorContext
    {
        private static readonly ConditionalWeakTable<STNode, Dictionary<string, PropertyChangedEventHandler>> _propertyChangedHandlers = new();
        private NodePanelBuilder? _panels;

        public STNode Node { get; set; }
        public StackPanel SignStackPanel { get; set; }
        public STNodeEditor STNodeEditor { get; set; }
        public Action Refresh { get; set; }
        public NodePanelBuilder Panels => _panels ??= new NodePanelBuilder(this);

        public void ReconfigureOnPropertyChanged(STNode node, string propertyName)
        {
            var handlers = _propertyChangedHandlers.GetValue(node, _ => new Dictionary<string, PropertyChangedEventHandler>());
            lock (handlers)
            {
                if (handlers.TryGetValue(propertyName, out var previousHandler))
                    node.PropertyChanged -= previousHandler;

                PropertyChangedEventHandler handler = (_, e) =>
                {
                    if ((string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == propertyName) && ReferenceEquals(STNodeEditor.ActiveNode, node))
                        Refresh();
                };
                handlers[propertyName] = handler;
                node.PropertyChanged += handler;
            }
        }

        public void AddTemplateCollectionPanel<T>(string propertyName, string signName, ObservableCollection<TemplateModel<T>> itemSource) where T : ParamModBase =>
            Panels.AddTemplateCollectionPanel(propertyName, signName, itemSource);

        public void AddTemplateJsonPanel<T>(string propertyName, string signName, ITemplateJson<T> template) where T : TemplateJsonParam, new() =>
            Panels.AddTemplateJsonPanel(propertyName, signName, template);

        public void AddTemplatePanel<T>(string propertyName, string signName, ITemplate<T> template) where T : ParamModBase, new() =>
            Panels.AddTemplatePanel(propertyName, signName, template);
    }
}
