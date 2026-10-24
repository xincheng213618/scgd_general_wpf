using ColorVision.Themes;
using FlowEngineLib.Base;
using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.FlowProcessing.Editor;

public partial class FlowRepeatRegionWindow : Window
{
    private DataTable? table;
    private RepeatNodeEditModel? selectedNode;
    private bool loading;
    internal RepeatRegionEditSession Session { get; }

    public FlowRepeatRegionWindow(CVCommonNode[] members, int count)
    {
        InitializeComponent();
        this.ApplyCaption();
        Title = EngineLocalization.Get("编辑重复框");
        Session = new RepeatRegionEditSession(members, count);
        RepeatCountBox.Text = count.ToString(CultureInfo.CurrentCulture);
        NodeCountText.Text = EngineLocalization.Format($"{Session.Nodes.Count} 个节点支持逐轮参数；其它节点按自身配置执行。");
        NodeList.ItemsSource = Session.Nodes;
        NodeList.SelectedIndex = 0;
    }

    private void NodeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        if (!CommitGrid())
        {
            loading = true;
            NodeList.SelectedItem = selectedNode;
            loading = false;
            return;
        }
        selectedNode = NodeList.SelectedItem as RepeatNodeEditModel;
        RefreshTable();
    }

    private void RefreshTable()
    {
        if (selectedNode == null) return;
        loading = true;
        try
        {
            NodeTitle.Text = selectedNode.Title;
            ParameterOptions.Children.Clear();
            UnavailableText.Text = string.Join(Environment.NewLine, selectedNode.Parameters.Where(parameter => !string.IsNullOrEmpty(parameter.Definition.UnavailableReason))
                .Select(parameter => $"{parameter.Definition.DisplayName}：{parameter.Definition.UnavailableReason}"));
            foreach (var parameter in selectedNode.Parameters)
            {
                var option = new CheckBox { Content = parameter.Header, IsChecked = parameter.Enabled, Margin = new Thickness(0, 0, 18, 3),
                    IsEnabled = string.IsNullOrEmpty(parameter.Definition.UnavailableReason) || parameter.Enabled,
                    ToolTip = string.IsNullOrEmpty(parameter.Definition.UnavailableReason) ? EngineLocalization.Get("取消勾选后使用节点自身的固定值。") : parameter.Definition.UnavailableReason };
                option.Click += (_, _) =>
                {
                    ParameterGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    ParameterGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    parameter.Enabled = option.IsChecked == true;
                    int column = selectedNode.Parameters.ToList().IndexOf(parameter) + 1;
                    ParameterGrid.Columns[column].IsReadOnly = !parameter.Enabled || !string.IsNullOrEmpty(parameter.Definition.UnavailableReason);
                    option.IsEnabled = string.IsNullOrEmpty(parameter.Definition.UnavailableReason) || parameter.Enabled;
                    UpdateParameterSelector();
                };
                ParameterOptions.Children.Add(option);
            }
            table = new DataTable { Locale = CultureInfo.CurrentCulture };
            table.Columns.Add("Round", typeof(int));
            ParameterGrid.Columns.Clear();
            ParameterGrid.Columns.Add(new DataGridTextColumn { Header = EngineLocalization.Get("轮次"), Binding = new Binding("[Round]"), IsReadOnly = true, Width = 65 });
            foreach (var parameter in selectedNode.Parameters)
            {
                table.Columns.Add(parameter.Definition.Key, typeof(string));
                ParameterGrid.Columns.Add(new DataGridTextColumn { Header = parameter.Header, Binding = new Binding($"[{parameter.Definition.Key}]")
                    { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    IsReadOnly = !parameter.Enabled || !string.IsNullOrEmpty(parameter.Definition.UnavailableReason) });
            }
            for (int index = 0; index < Session.Count; index++)
            {
                DataRow row = table.NewRow();
                row["Round"] = index + 1;
                foreach (var parameter in selectedNode.Parameters)
                    row[parameter.Definition.Key] = index < parameter.Values.Length ? parameter.Values[index].ToString("G", CultureInfo.CurrentCulture) : string.Empty;
                table.Rows.Add(row);
            }
            ParameterGrid.ItemsSource = table.DefaultView;
            UpdateParameterSelector();
            FirstRoundBox.Text = "1";
            LastRoundBox.Text = Session.Count.ToString(CultureInfo.CurrentCulture);
            if (!string.IsNullOrEmpty(selectedNode.LoadError)) ErrorText.Text = selectedNode.LoadError;
        }
        finally { loading = false; }
    }

    private bool CommitGrid()
    {
        if (!TryReadGrid(out var parsed)) return false;
        foreach (var pair in parsed) pair.Key.Replace(pair.Value);
        ErrorText.Text = string.Empty;
        return true;
    }

    private bool TryReadGrid(out System.Collections.Generic.Dictionary<RepeatParameterEditSeries, double[]> parsed,
        System.Collections.Generic.ISet<int>? excluded = null)
    {
        parsed = new();
        if (selectedNode == null || table == null) return true;
        if (!ParameterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !ParameterGrid.CommitEdit(DataGridEditingUnit.Row, true)) return false;
        int[] indices = Enumerable.Range(0, table.Rows.Count).Where(index => excluded?.Contains(index) != true).ToArray();
        foreach (var parameter in selectedNode.Parameters)
        {
            double[] values = new double[indices.Length];
            bool valid = true;
            for (int index = 0; index < values.Length; index++)
            {
                if (!TryNumber(table.Rows[indices[index]][parameter.Definition.Key]?.ToString(), out double value)
                    || parameter.Enabled && !parameter.Definition.TryValidate(value, out _))
                {
                    if (!parameter.Enabled) { valid = false; break; }
                    ErrorText.Text = EngineLocalization.Format($"{selectedNode.Title} · 第 {indices[index] + 1} 轮的{parameter.Definition.DisplayName}无效。")
                        + (!string.IsNullOrEmpty(parameter.Definition.UnavailableReason) ? parameter.Definition.UnavailableReason : parameter.Definition.IsInteger ? EngineLocalization.Get("请输入有效的正整数。") : EngineLocalization.Get("请检查数值及允许范围。"));
                    return false;
                }
                values[index] = value;
            }
            if (valid) parsed.Add(parameter, values);
        }
        return true;
    }

    private void UpdateParameterSelector()
    {
        if (selectedNode == null) return;
        string? key = (ParameterSelector.SelectedItem as RepeatParameterEditSeries)?.Definition.Key;
        ParameterSelector.ItemsSource = selectedNode.Parameters.Where(parameter => parameter.Enabled && string.IsNullOrEmpty(parameter.Definition.UnavailableReason)).ToArray();
        ParameterSelector.SelectedItem = ParameterSelector.Items.Cast<RepeatParameterEditSeries>().FirstOrDefault(parameter => parameter.Definition.Key == key);
        if (ParameterSelector.SelectedIndex < 0) ParameterSelector.SelectedIndex = 0;
    }

    private static bool TryNumber(string? value, out double result)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private bool ApplyCount()
    {
        if (!int.TryParse(RepeatCountBox.Text, out int count) || count < 1 || count > RepeatRegionEditSession.MaximumEditableRounds)
        { ErrorText.Text = EngineLocalization.Format($"执行次数必须是 1 到 {RepeatRegionEditSession.MaximumEditableRounds:N0} 的整数。"); return false; }
        var excluded = Enumerable.Range(Math.Min(count, Session.Count), Math.Max(0, Session.Count - count)).ToHashSet();
        if (!TryReadGrid(out var parsed, excluded)) return false;
        foreach (var pair in parsed) pair.Key.Replace(pair.Value);
        if (count != Session.Count)
        {
            Session.Resize(count);
            RefreshTable();
        }
        return true;
    }

    private void ApplyCount_Click(object sender, RoutedEventArgs e) => ApplyCount();

    private void AddRound_Click(object sender, RoutedEventArgs e)
    {
        if (!ApplyCount()) return;
        if (Session.Count == RepeatRegionEditSession.MaximumEditableRounds) { ErrorText.Text = EngineLocalization.Get("已达到逐轮参数编辑的最大轮数。"); return; }
        Session.Resize(Session.Count + 1);
        RepeatCountBox.Text = Session.Count.ToString(CultureInfo.CurrentCulture);
        RefreshTable();
    }

    private void RemoveRounds_Click(object sender, RoutedEventArgs e)
    {
        int[] indices = ParameterGrid.SelectedItems.OfType<DataRowView>().Select(row => (int)row["Round"] - 1).ToArray();
        if (indices.Length == 0) { ErrorText.Text = EngineLocalization.Get("请先在表格中选中需要删除的轮次。"); return; }
        if (indices.Length == Session.Count) { ErrorText.Text = EngineLocalization.Get("至少保留一轮执行。"); return; }
        if (!TryReadGrid(out var parsed, indices.ToHashSet())) return;
        try { Session.RemoveRounds(indices); }
        catch (InvalidOperationException ex) { ErrorText.Text = ex.Message; return; }
        foreach (var pair in parsed) pair.Key.Replace(pair.Value);
        RepeatCountBox.Text = Session.Count.ToString(CultureInfo.CurrentCulture);
        RefreshTable();
    }

    private void ParameterSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ParameterSelector.SelectedItem is RepeatParameterEditSeries parameter)
            StartValueBox.Text = parameter.Definition.DefaultValue.ToString("G", CultureInfo.CurrentCulture);
    }

    private void ParameterGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        int[] rounds = ParameterGrid.SelectedItems.OfType<DataRowView>().Select(row => (int)row["Round"]).ToArray();
        if (rounds.Length == 0) return;
        FirstRoundBox.Text = rounds.Min().ToString(CultureInfo.CurrentCulture);
        LastRoundBox.Text = rounds.Max().ToString(CultureInfo.CurrentCulture);
    }

    private void Fill_Click(object sender, RoutedEventArgs e)
    {
        if (ParameterSelector.SelectedItem is not RepeatParameterEditSeries parameter || table == null) return;
        ParameterGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ParameterGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (!int.TryParse(FirstRoundBox.Text, out int first) || !int.TryParse(LastRoundBox.Text, out int last)
            || !TryNumber(StartValueBox.Text, out double start) || !TryNumber(IncrementBox.Text, out double increment))
        { ErrorText.Text = EngineLocalization.Get("请填写有效的轮次、起始值和增量。"); return; }
        try
        {
            double[] generated = parameter.GenerateFill(first - 1, last - 1, start, increment);
            for (int index = 0; index < generated.Length; index++)
                table.Rows[first + index - 1][parameter.Definition.Key] = generated[index].ToString("G", CultureInfo.CurrentCulture);
            CommitGrid();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { ErrorText.Text = ex.Message; }
    }

    internal bool ValidateEdits()
    {
        if (!ApplyCount()) return false;
        if (Session.TryValidate(out string error)) return true;
        ErrorText.Text = error;
        return false;
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateEdits()) DialogResult = true;
    }
}
