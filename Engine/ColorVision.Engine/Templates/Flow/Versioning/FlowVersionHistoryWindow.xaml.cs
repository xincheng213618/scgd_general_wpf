using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Routing;
using FlowEngineLib.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public partial class FlowVersionHistoryWindow : Window
    {
        private readonly FlowParam flowParam;
        private readonly Action? restored;
        private readonly Func<bool>? isFlowRunning;
        private string? expectedContentHash;

        public FlowVersionHistoryWindow(
            FlowParam flowParam,
            Action? restored = null,
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
            HeaderText.Text = $"{flowParam.Name} · 本机版本历史";
            Reload();
        }

        private void Reload()
        {
            if (string.IsNullOrWhiteSpace(flowParam.FlowKey))
            {
                RevisionGrid.ItemsSource =
                    Array.Empty<FlowVersionHistoryRow>();
                StatusText.Text = "当前流程还没有稳定 FlowKey。";
                return;
            }

            try
            {
                IReadOnlyList<FlowRevision> revisions =
                    FlowCatalogProvider.Shared.List(flowParam.FlowKey);
                RevisionGrid.ItemsSource = revisions
                    .OrderByDescending(item => item.Revision)
                    .Select(item => new FlowVersionHistoryRow(item))
                    .ToArray();
                StatusText.Text = revisions.Count == 0
                    ? "本机尚未产生版本；保存一次后会建立版本 1。"
                    : $"本机共 {revisions.Count} 个不可变版本，"
                        + $"当前画布对应版本 "
                        + $"{flowParam.TemplateRevision?.ToString() ?? "未知"}。";
            }
            catch (Exception ex)
            {
                RevisionGrid.ItemsSource =
                    Array.Empty<FlowVersionHistoryRow>();
                StatusText.Text = $"读取版本目录失败：{ex.Message}";
            }
        }

        private void CompareButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            FlowVersionHistoryRow[] selected =
                RevisionGrid.SelectedItems
                    .Cast<FlowVersionHistoryRow>()
                    .OrderBy(item => item.Revision)
                    .ToArray();
            if (selected.Length != 2)
            {
                MessageBox.Show(
                    this,
                    "请选择恰好两个版本进行对比。",
                    "流程版本",
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
                    $"版本对比失败：{ex.Message}",
                    "流程版本",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            string summary =
                $"版本 {selected[0].Revision} → "
                + $"{selected[1].Revision}\n\n"
                + $"节点：+{diff.AddedNodes.Count} "
                + $"-{diff.RemovedNodes.Count} "
                + $"类型变化 {diff.ChangedNodeTypes.Count}\n"
                + $"属性变化：{diff.PropertyChanges.Count}\n"
                + $"普通连接：+{diff.AddedEdges.Count} "
                + $"-{diff.RemovedEdges.Count}\n"
                + $"子流程：+{diff.AddedSubflows.Count} "
                + $"-{diff.RemovedSubflows.Count}\n"
                + $"错误路由：+{diff.AddedErrorRoutes.Count} "
                + $"-{diff.RemovedErrorRoutes.Count}\n"
                + $"重试策略：+{diff.AddedRetryPolicies.Count} "
                + $"-{diff.RemovedRetryPolicies.Count}\n"
                + $"布局变化：{diff.LayoutChanges.Count}"
                + $"{(diff.ViewportChanged ? "，视口已变化" : string.Empty)}"
                + $"\n\n分类：{(diff.IsLayoutOnly ? "仅布局变化" : diff.HasSemanticChanges ? "语义变化" : "无变化")}";
            MessageBox.Show(
                this,
                summary,
                "流程版本对比",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RestoreButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (isFlowRunning?.Invoke() == true)
            {
                MessageBox.Show(
                    this,
                    "流程正在运行，请在运行结束后恢复版本。",
                    "流程版本",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (RevisionGrid.SelectedItem
                is not FlowVersionHistoryRow selected)
            {
                MessageBox.Show(
                    this,
                    "请选择一个要恢复的版本。",
                    "流程版本",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            if (selected.Revision == flowParam.TemplateRevision)
            {
                MessageBox.Show(
                    this,
                    "当前画布已经是这个版本。",
                    "流程版本",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                $"恢复版本 {selected.Revision}？\n"
                + "现有历史不会被覆盖，恢复内容会保存为一个新的版本。"
                + "\n当前画布尚未保存的修改会被替换。",
                "恢复流程版本",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
                return;

            string previousData = flowParam.DataBase64;
            int? previousTemplateRevision =
                flowParam.TemplateRevision;
            FlowExecutionPolicySnapshot? previousPolicy = null;
            FlowExecutionPolicySnapshot? savedPolicy = null;
            bool templateSaved = false;
            try
            {
                if (!FlowExecutionPolicyStoreProvider.Shared.TryLoad(
                        flowParam.FlowKey!,
                        out previousPolicy,
                        out string? policyFailure))
                {
                    throw new InvalidOperationException(
                        "读取当前执行策略失败，未开始恢复："
                        + policyFailure);
                }

                FlowExecutionPolicySaveRequest targetPolicy =
                    CreatePolicySaveRequest(
                        flowParam.FlowKey!,
                        previousPolicy.Revision,
                        selected.Model.SemanticDocument);
                ValidateRestoreProjection(
                    selected.Model,
                    targetPolicy);
                NormalizedFlowExecutionPolicy normalizedPolicy =
                    FlowExecutionPolicyRules.Normalize(
                        flowParam.FlowKey!,
                        targetPolicy.ErrorRoutes,
                        targetPolicy.RetryPolicies);
                if (!string.Equals(
                        previousPolicy.ContentHash,
                        normalizedPolicy.ContentHash,
                        StringComparison.Ordinal))
                {
                    savedPolicy =
                        FlowExecutionPolicyStoreProvider.Shared.Save(
                            targetPolicy);
                }

                flowParam.DataBase64 = Convert.ToBase64String(
                    selected.Model.FullSnapshot);
                // Pin the source revision long enough for TemplateFlow to
                // inherit its exact immutable subflow sidecar.
                flowParam.TemplateRevision = selected.Revision;
                TemplateFlow.Save2DB(
                    flowParam,
                    new FlowTemplateSaveCondition(
                        expectedContentHash));
                expectedContentHash =
                    flowParam.LoadedContentHash;
                templateSaved = true;
                restored?.Invoke();
                Reload();
                if (flowParam.TemplateRevision == null)
                {
                    MessageBox.Show(
                        this,
                        "流程内容和执行策略已恢复，但本地版本目录更新失败。"
                        + "\n请查看日志并修复本地侧车存储后重新保存。",
                        "流程已恢复",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                string? rollbackFailure = null;
                if (!templateSaved)
                {
                    flowParam.DataBase64 = previousData;
                    flowParam.TemplateRevision =
                        previousTemplateRevision;
                    rollbackFailure =
                        TryRestorePreviousPolicy(
                            flowParam.FlowKey!,
                            previousPolicy,
                            savedPolicy);
                }
                MessageBox.Show(
                    this,
                    (templateSaved
                        ? "版本已经恢复，但刷新编辑器失败："
                        : "恢复版本失败：")
                    + ex.Message
                    + (rollbackFailure == null
                        ? string.Empty
                        : "\n恢复执行策略时又发生错误："
                            + rollbackFailure),
                    "流程版本",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void ValidateRestoreProjection(
            FlowRevision revision,
            FlowExecutionPolicySaveRequest policy)
        {
            StoredFlowSubflowDefinition? sidecar =
                FlowSubflowDefinitionStoreProvider.Shared.GetRevision(
                    revision.FlowKey,
                    revision.Revision);
            if (revision.SemanticDocument.Subflows.Count > 0
                && sidecar == null)
            {
                throw new InvalidOperationException(
                    $"版本 {revision.Revision} 引用了子流程，"
                    + "但对应的不可变子流程侧车不存在。");
            }

            NormalizedFlowExecutionPolicy normalized =
                FlowExecutionPolicyRules.Normalize(
                    revision.FlowKey,
                    policy.ErrorRoutes,
                    policy.RetryPolicies);
            var snapshot = new FlowExecutionPolicySnapshot(
                revision.FlowKey,
                revision: 0,
                normalized.ContentHash,
                DateTime.UnixEpoch,
                normalized.ErrorRoutes,
                normalized.RetryPolicies);
            FlowCanvasCatalogBuildResult projection =
                new FlowCanvasCatalogBuilder().Build(
                    revision.FullSnapshot,
                    sidecar?.Sidecar
                        ?? FlowSubflowSidecar.Empty,
                    snapshot);
            string semanticHash =
                FlowSemanticHash.ComputeSemanticHash(
                    projection.SemanticDocument);
            string layoutHash =
                FlowSemanticHash.ComputeLayoutHash(
                    projection.SemanticDocument);
            if (!string.Equals(
                    semanticHash,
                    revision.SemanticHash,
                    StringComparison.Ordinal)
                || !string.Equals(
                    layoutHash,
                    revision.LayoutHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"版本 {revision.Revision} 的 STN 与侧车内容"
                    + "无法重建出原始语义，已拒绝恢复。");
            }
        }

        internal static FlowExecutionPolicySaveRequest
            CreatePolicySaveRequest(
                string flowKey,
                long expectedRevision,
                FlowSemanticDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            var retries = document.RetryPolicies
                .Select(policy => new FlowRetryPolicy(
                    policy.NodeId,
                    policy.MaxAttempts,
                    policy.InitialDelayMs,
                    policy.Backoff,
                    policy.MaxDelayMs,
                    policy.RetryableKinds
                        .Select(ParseFailureKind)
                        .ToArray()))
                .ToArray();

            var routeBindings = document.ErrorRoutes
                .Select(route =>
                {
                    if (!route.IsInterrupting)
                    {
                        throw new InvalidOperationException(
                            "当前运行时不支持恢复非中断型错误路由。");
                    }
                    return new
                    {
                        route.SourceNodeId,
                        route.TargetNodeId,
                        TargetInputIndex =
                            ParseTargetInputIndex(route.TargetPort),
                        FailureKind =
                            ParseFailureKind(route.ErrorCode),
                    };
                })
                .ToArray();
            FlowErrorRoutePolicy[] routes = routeBindings
                .GroupBy(item => new
                {
                    item.SourceNodeId,
                    item.TargetNodeId,
                    item.TargetInputIndex,
                })
                .Select(group => new FlowErrorRoutePolicy(
                    group.Key.SourceNodeId,
                    group.Key.TargetNodeId,
                    group.Key.TargetInputIndex,
                    group.Select(item => item.FailureKind)
                        .Distinct()
                        .ToArray()))
                .ToArray();
            return new FlowExecutionPolicySaveRequest(
                flowKey,
                expectedRevision,
                routes,
                retries);
        }

        private static FlowFailureKind ParseFailureKind(
            string value)
        {
            if (!Enum.TryParse(
                    value,
                    ignoreCase: false,
                    out FlowFailureKind kind)
                || !Enum.IsDefined(kind))
            {
                throw new InvalidOperationException(
                    $"版本包含无法识别的失败类型：{value}。");
            }
            return kind;
        }

        private static int ParseTargetInputIndex(
            string targetPort)
        {
            const string prefix = "in:";
            if (string.IsNullOrWhiteSpace(targetPort)
                || !targetPort.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                || !int.TryParse(
                    targetPort[prefix.Length..],
                    out int inputIndex)
                || inputIndex < 0)
            {
                throw new InvalidOperationException(
                    $"版本包含无效的错误路由目标端口："
                    + $"{targetPort}。");
            }
            return inputIndex;
        }

        private static string? TryRestorePreviousPolicy(
            string flowKey,
            FlowExecutionPolicySnapshot? previous,
            FlowExecutionPolicySnapshot? saved)
        {
            if (previous == null || saved == null)
                return null;
            try
            {
                FlowExecutionPolicyStoreProvider.Shared.Save(
                    new FlowExecutionPolicySaveRequest(
                        flowKey,
                        saved.Revision,
                        previous.ErrorRoutes,
                        previous.RetryPolicies));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private sealed class FlowVersionHistoryRow
        {
            public FlowVersionHistoryRow(FlowRevision model)
            {
                Model = model;
            }

            public FlowRevision Model { get; }

            public int Revision => Model.Revision;

            public FlowRevisionSource Source => Model.Source;

            public bool IsPublished => Model.IsPublished;

            public DateTime CreatedTimeUtc => Model.CreatedTimeUtc;

            public string? Author => Model.Author;

            public string? Message => Model.Message;

            public string ShortBinaryHash =>
                Model.BinaryHash.Length <= 12
                    ? Model.BinaryHash
                    : Model.BinaryHash[..12];
        }
    }
}
