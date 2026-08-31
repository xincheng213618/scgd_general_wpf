using ColorVision.Common.MVVM;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow.Versioning;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow.Search
{
    public sealed class FlowNodeDynamicSearchProvider :
        IDynamicSearchProvider
    {
        public IEnumerable<ISearch> Search(
            string query,
            int limit)
        {
            if (string.IsNullOrWhiteSpace(query) || limit <= 0)
                yield break;

            IReadOnlyList<FlowNodeSearchEntry> entries;
            try
            {
                entries = FlowCatalogProvider.Shared.SearchLatest(
                    query,
                    limit);
            }
            catch
            {
                yield break;
            }

            foreach (FlowNodeSearchEntry entry in entries)
            {
                TemplateModel<FlowParam>? template =
                    TemplateFlow.Params.FirstOrDefault(item =>
                        string.Equals(
                            item.Value?.FlowKey,
                            entry.FlowKey,
                            StringComparison.Ordinal));
                if (template?.Value == null
                    || template.Value.TemplateRevision
                        != entry.Revision)
                    continue;

                string nodeName = entry.DisplayName
                    ?? entry.Title
                    ?? entry.NodeTypeKey;
                string flowName = template.Key;
                string flowKey = entry.FlowKey;
                int revision = entry.Revision;
                Guid sourceNodeGuid = entry.SourceNodeGuid;
                yield return new SearchMeta
                {
                    Type = SearchType.File,
                    CategoryKey = "FlowNodes",
                    Header = nodeName,
                    Description = $"{flowName} / {entry.NodeTypeKey}",
                    GuidId = $"flow-node:{Uri.EscapeDataString(flowKey)}:{revision}:{sourceNodeGuid:D}",
                    Aliases =
                        new[]
                        {
                            flowName,
                            entry.FlowKey,
                            entry.NodeTypeKey,
                            entry.DeviceCode,
                            entry.ServiceCode,
                            entry.Tags,
                        }.Where(value =>
                            !string.IsNullOrWhiteSpace(value)).ToArray(),
                    Command = new RelayCommand(_ =>
                    {
                        TemplateModel<FlowParam>? current =
                            TemplateFlow.Params.FirstOrDefault(item =>
                                string.Equals(
                                    item.Value?.FlowKey,
                                    flowKey,
                                    StringComparison.Ordinal));
                        if (current?.Value == null
                            || current.Value.TemplateRevision != revision)
                        {
                            MessageBox.Show(
                                Application.Current.GetActiveWindow(),
                                "搜索结果对应的流程版本已经变化，请重新搜索。",
                                "流程搜索",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            return;
                        }

                        var window =
                            new FlowEngineToolWindow(current.Value);
                        window.Loaded += (_, _) =>
                        {
                            if (!window.View.TryFocusNode(sourceNodeGuid))
                            {
                                MessageBox.Show(
                                    window,
                                    "流程已打开，但目标节点不存在。请重新保存流程并刷新搜索索引。",
                                    "流程搜索",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                        };
                        window.Show();
                    }),
                };
            }
        }
    }
}
