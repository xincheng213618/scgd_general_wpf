using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public partial class FlowVersionHistoryWindow : Window
    {
        private readonly FlowParam flowParam;
        private readonly Func<Task>? restored;
        private readonly Func<bool>? isFlowRunning;
        private string? expectedContentHash;

        public FlowVersionHistoryWindow(
            FlowParam flowParam,
            Func<Task>? restored = null,
            string? expectedContentHash = null,
            Func<bool>? isFlowRunning = null)
        {
            this.flowParam = flowParam
                ?? throw new ArgumentNullException(nameof(flowParam));
            this.restored = restored;
            this.expectedContentHash =
                expectedContentHash;
            this.isFlowRunning = isFlowRunning;
            InitializeComponent();
            ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
            HeaderText.Text = EngineLocalization.Format($"{flowParam.Name} · 本机版本历史");
            Reload();
        }

        private void Reload()
        {
            if (string.IsNullOrWhiteSpace(flowParam.FlowKey))
            {
                RevisionGrid.ItemsSource =
                    Array.Empty<FlowRevisionSummary>();
                StatusText.Text = EngineLocalization.Get("当前流程还没有稳定 FlowKey。");
                return;
            }

            try
            {
                IReadOnlyList<FlowRevisionSummary> revisions =
                    FlowCatalogProvider.Shared.ListHistory(flowParam.FlowKey);
                RevisionGrid.ItemsSource = revisions;
                StatusText.Text = revisions.Count == 0
                    ? EngineLocalization.Get("本机尚未产生版本；保存一次后会建立版本 1。")
                    : EngineLocalization.Format($"本机共 {revisions.Count} 个不可变版本，当前画布对应版本 {flowParam.TemplateRevision?.ToString() ?? EngineLocalization.Get("未知")}。");
            }
            catch (Exception ex)
            {
                RevisionGrid.ItemsSource =
                    Array.Empty<FlowRevisionSummary>();
                StatusText.Text = EngineLocalization.Format($"读取版本目录失败：{ex.Message}");
            }
        }

        private void CompareButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            FlowRevisionSummary[] selected =
                RevisionGrid.SelectedItems
                    .Cast<FlowRevisionSummary>()
                    .OrderBy(item => item.Revision)
                    .ToArray();
            if (selected.Length != 2)
            {
                MessageBox.Show(
                    this,
                    EngineLocalization.Get("请选择恰好两个版本进行对比。"),
                    EngineLocalization.Get("流程版本"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            FlowSemanticDiffResult diff;
            try
            {
                diff = FlowCatalogProvider.Shared.Compare(
                    flowParam.FlowKey!,
                    selected[0].Revision,
                    selected[1].Revision);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    EngineLocalization.Format($"版本对比失败：{ex.Message}"),
                    EngineLocalization.Get("流程版本"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            string viewportChange = diff.ViewportChanged ? EngineLocalization.Get("，视口已变化") : string.Empty;
            string classification = diff.IsLayoutOnly
                ? EngineLocalization.Get("仅布局变化")
                : diff.HasSemanticChanges
                    ? EngineLocalization.Get("语义变化")
                    : EngineLocalization.Get("无变化");
            string summary = EngineLocalization.Format(
                $"版本 {selected[0].Revision} → {selected[1].Revision}\n\n节点：+{diff.AddedNodes.Count} -{diff.RemovedNodes.Count} 类型变化 {diff.ChangedNodeTypes.Count}\n属性变化：{diff.PropertyChanges.Count}\n普通连接：+{diff.AddedEdges.Count} -{diff.RemovedEdges.Count}\n布局变化：{diff.LayoutChanges.Count}{viewportChange}\n\n分类：{classification}");
            MessageBox.Show(
                this,
                summary,
                EngineLocalization.Get("流程版本对比"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void RestoreButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (isFlowRunning?.Invoke() == true)
            {
                MessageBox.Show(
                    this,
                    EngineLocalization.Get("流程正在运行，请在运行结束后恢复版本。"),
                    EngineLocalization.Get("流程版本"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (RevisionGrid.SelectedItem
                is not FlowRevisionSummary selected)
            {
                MessageBox.Show(
                    this,
                    EngineLocalization.Get("请选择一个要恢复的版本。"),
                    EngineLocalization.Get("流程版本"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            if (selected.Revision == flowParam.TemplateRevision)
            {
                MessageBox.Show(
                    this,
                    EngineLocalization.Get("当前画布已经是这个版本。"),
                    EngineLocalization.Get("流程版本"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                EngineLocalization.Format($"恢复版本 {selected.Revision}？\n现有历史不会被覆盖，恢复内容会保存为一个新的版本。\n当前画布尚未保存的修改会被替换。"),
                EngineLocalization.Get("恢复流程版本"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
                return;

            FlowVersionRestoreResult result;
            try
            {
                FlowRevision revision = FlowCatalogProvider.Shared.GetRevision(selected.FlowKey, selected.Revision)
                    ?? throw new InvalidOperationException(EngineLocalization.Get("所选版本已不存在，请刷新版本历史。"));
                result = new FlowVersionRestoreService().Restore(
                    new FlowVersionRestoreRequest(flowParam, revision, expectedContentHash));
            }
            catch (Exception ex)
            {
                result = FlowVersionRestoreResult.Failure(ex.Message);
            }
            if (!result.Succeeded)
            {
                MessageBox.Show(
                    this,
                    EngineLocalization.Format($"恢复版本失败：{result.FailureMessage}"),
                    EngineLocalization.Get("流程版本"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            expectedContentHash = result.LoadedContentHash;
            try
            {
                IsEnabled = false;
                if (restored != null)
                    await restored();
                Reload();
                if (!result.VersionCatalogUpdated)
                {
                    MessageBox.Show(
                        this,
                        EngineLocalization.Get("流程内容已恢复，但本地版本目录更新失败。\n请查看日志并重新保存。"),
                        EngineLocalization.Get("流程已恢复"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    EngineLocalization.Format($"版本已经恢复，但刷新编辑器失败：{ex.Message}"),
                    EngineLocalization.Get("流程版本"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }
    }
}
