using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Versioning;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing.Compilation;

public partial class FlowSubflowEditorWindow : Window
{
    private readonly FlowParam flowParam;
    private readonly IReadOnlyList<FlowSubflowConnectionChoice> connections;
    private readonly IReadOnlyList<FlowSubflowTargetChoice> targets;
    private readonly ObservableCollection<CallRow> rows = new();
    private FlowSubflowSidecar workingSidecar;

    public FlowSubflowEditorWindow(
        FlowParam flowParam,
        STNodeEditor editor)
    {
        this.flowParam = flowParam
            ?? throw new ArgumentNullException(nameof(flowParam));
        ArgumentNullException.ThrowIfNull(editor);
        InitializeComponent();

        connections =
            FlowSubflowAuthoring.CaptureConnections(editor);
        targets = LoadTargets(flowParam);
        workingSidecar = LoadCurrentSidecar(flowParam);
        ConnectionBox.ItemsSource = connections;
        TargetBox.ItemsSource = targets;
        CallsGrid.ItemsSource = rows;
        ConnectionBox.SelectedIndex =
            connections.Count == 0 ? -1 : 0;
        TargetBox.SelectedIndex = targets.Count == 0 ? -1 : 0;
        CallIdBox.Text = CreateNextCallId();
        RefreshRows();
        UpdateStatus();
    }

    public FlowSubflowSidecar WorkingSidecar =>
        FlowSubflowSidecarPersistence.Clone(workingSidecar);

    private static IReadOnlyList<FlowSubflowTargetChoice>
        LoadTargets(FlowParam root)
    {
        var choices = new List<FlowSubflowTargetChoice>();
        foreach (TemplateModel<FlowParam> template in
            TemplateFlow.Params
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Value.FlowKey)
                    && !string.Equals(
                        item.Value.FlowKey,
                        root.FlowKey,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    item => item.Key,
                    StringComparer.CurrentCulture))
        {
            foreach (FlowRevision revision in
                FlowCatalogProvider.Shared
                    .List(template.Value.FlowKey!)
                    .OrderByDescending(item => item.Revision))
            {
                choices.Add(new FlowSubflowTargetChoice(
                    template.Key,
                    revision.FlowKey,
                    revision.Revision,
                    revision.BinaryHash));
            }
        }
        return choices
            .GroupBy(
                item => (item.FlowKey, item.Revision),
                FlowTargetKeyComparer.Instance)
            .Select(group => group.First())
            .ToArray();
    }

    private static FlowSubflowSidecar LoadCurrentSidecar(
        FlowParam flowParam)
    {
        if (string.IsNullOrWhiteSpace(flowParam.FlowKey))
            return FlowSubflowSidecar.Empty;
        int? revision = flowParam.TemplateRevision;
        if (revision == null
            && !string.IsNullOrWhiteSpace(flowParam.DataBase64))
        {
            FlowRevision? matching =
                FlowCatalogProvider.Shared.FindRevision(
                    flowParam.FlowKey,
                    Convert.FromBase64String(
                        flowParam.DataBase64));
            revision = matching?.Revision;
        }
        return revision is > 0
            ? FlowSubflowDefinitionStoreProvider.Shared
                .GetRevision(
                    flowParam.FlowKey,
                    revision.Value)
                ?.Sidecar
                ?? FlowSubflowSidecar.Empty
            : FlowSubflowSidecar.Empty;
    }

    private void Upsert_Click(object sender, RoutedEventArgs e)
    {
        if (ConnectionBox.SelectedItem
                is not FlowSubflowConnectionChoice connection
            || TargetBox.SelectedItem
                is not FlowSubflowTargetChoice target)
        {
            MessageBox.Show(
                this,
                connections.Count == 0
                    ? "当前画布没有可作为调用点的连接。"
                    : "请先选择目标流程的固定版本。",
                "可复用子流程",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        try
        {
            workingSidecar = FlowSubflowAuthoring.Upsert(
                workingSidecar,
                CallIdBox.Text,
                connection,
                target);
            RefreshRows();
            CallIdBox.Text = CreateNextCallId();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "可复用子流程",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (CallsGrid.SelectedItem is not CallRow selected)
            return;
        workingSidecar = FlowSubflowAuthoring.Remove(
            workingSidecar,
            selected.Call.CallId);
        RefreshRows();
        UpdateStatus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FlowSubflowConfigurationSaveResult result =
                TemplateFlow.SaveSubflowConfiguration(
                    flowParam,
                    workingSidecar,
                    PublishArtifactBox.IsChecked == true);
            if (result.ArtifactFailure != null)
            {
                MessageBox.Show(
                    this,
                    $"子流程侧车和流程版本 r"
                    + $"{result.FlowRevision.Revision} 已保存，"
                    + "但 Artifact 未保存或发布。"
                    + Environment.NewLine
                    + result.ArtifactFailure.Message,
                    "可复用子流程",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                StatusText.Text =
                    $"版本 r{result.FlowRevision.Revision} 已保存；"
                    + "Artifact 失败";
                return;
            }

            StatusText.Text =
                $"版本 r{result.FlowRevision.Revision}，Artifact r"
                + $"{result.ArtifactRevision!.Revision} "
                + $"{result.ArtifactRevision.State}";
            MessageBox.Show(
                this,
                "子流程配置已保存。"
                + Environment.NewLine
                + StatusText.Text,
                "可复用子流程",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "保存子流程配置失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CallsGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (CallsGrid.SelectedItem is not CallRow row)
            return;
        CallIdBox.Text = row.Call.CallId;
        ConnectionBox.SelectedItem = connections.FirstOrDefault(
            item => item.Source == row.Call.Source
                && item.Target == row.Call.Target);
        TargetBox.SelectedItem = targets.FirstOrDefault(
            item => string.Equals(
                    item.FlowKey,
                    row.Call.Child.FlowKey,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    item.Revision.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    row.Call.Child.Revision,
                    StringComparison.Ordinal)
                && string.Equals(
                    item.ContentHash,
                    row.Call.Child.ContentHash,
                    StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshRows()
    {
        rows.Clear();
        foreach (FlowSubflowCall call in workingSidecar.Calls)
        {
            string connection = connections.FirstOrDefault(
                    item => item.Source == call.Source
                        && item.Target == call.Target)
                ?.DisplayName
                ?? $"{call.Source.NodeId:N}:{call.Source.OptionIndex}"
                + $" → {call.Target.NodeId:N}:{call.Target.OptionIndex}";
            string target = targets.FirstOrDefault(item =>
                    string.Equals(
                        item.FlowKey,
                        call.Child.FlowKey,
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        item.Revision.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        call.Child.Revision,
                        StringComparison.Ordinal))
                ?.DisplayName
                ?? $"{call.Child.FlowKey} · r{call.Child.Revision}"
                + $" · {call.Child.ContentHash?[..8]}";
            rows.Add(new CallRow(
                call,
                connection,
                target));
        }
    }

    private string CreateNextCallId()
    {
        int number = 1;
        while (workingSidecar.Calls.Any(call =>
            string.Equals(
                call.CallId,
                $"subflow-{number}",
                StringComparison.Ordinal)))
        {
            number++;
        }
        return $"subflow-{number}";
    }

    private void UpdateStatus()
    {
        if (connections.Count == 0)
        {
            StatusText.Text = "当前画布没有连接，不能创建调用点。";
        }
        else if (targets.Count == 0)
        {
            StatusText.Text =
                "没有可用的目标版本；请先保存一次目标流程。";
        }
        else
        {
            StatusText.Text =
                $"已配置 {workingSidecar.Calls.Count} 条子流程调用";
        }
    }

    private sealed record CallRow(
        FlowSubflowCall Call,
        string Connection,
        string Target)
    {
        public string CallId => Call.CallId;
    }

    private sealed class FlowTargetKeyComparer :
        IEqualityComparer<(string FlowKey, int Revision)>
    {
        public static FlowTargetKeyComparer Instance { get; } =
            new();

        public bool Equals(
            (string FlowKey, int Revision) x,
            (string FlowKey, int Revision) y)
        {
            return x.Revision == y.Revision
                && string.Equals(
                    x.FlowKey,
                    y.FlowKey,
                    StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(
            (string FlowKey, int Revision) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(
                    obj.FlowKey),
                obj.Revision);
        }
    }
}
