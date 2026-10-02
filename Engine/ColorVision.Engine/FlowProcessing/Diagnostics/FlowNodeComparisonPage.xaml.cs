using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

internal partial class FlowNodeComparisonPage : Page
{
    private readonly FlowExecutionAnalysisSession _session;
    private readonly FlowNodeRecord _record;
    private readonly Action _back;
    private IReadOnlyList<FlowNodeComparisonRow> _rows = Array.Empty<FlowNodeComparisonRow>();
    private FlowNodeComparisonRow? _baseline;
    private int _lifetimeVersion;
    private int _selectionVersion;
    private int _baselineVersion;

    internal FlowNodeComparisonPage(FlowExecutionAnalysisSession session, FlowNodeRecord record, Action back)
    {
        _session = session;
        _record = record;
        _back = back;
        InitializeComponent();
        TitleText.Text = EngineLocalization.Format($"{record.NodeName} · 跨批次比对");
        HintText.Text = EngineLocalization.Get("正在加载同流程、同节点的消息…");
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        int version = ++_lifetimeVersion;
        try
        {
            FlowNodeComparisonHistory history = await Task.Run(() => _session.DataSource.GetNodeComparisonHistory(_session, _record));
            if (version != _lifetimeVersion) return;
            _rows = history.BuildRows();
            int runCount = history.Records.Select(item => (item.BatchId, item.SerialNumber)).Distinct().Count();
            HintText.Text = history.HasFlowIdentity
                ? EngineLocalization.Format($"同流程、同节点 · {runCount} 次运行 · {history.Messages.Count} 条消息 · 查询最近 500 次流程运行及当前运行；选择一行查看，设为基准后并排比对。")
                : EngineLocalization.Get("当前记录缺少流程身份或节点 ID，仅显示本次执行，无法可靠关联其他批次。");
            FlowNodeComparisonRow? initial = _rows.FirstOrDefault(row => row.Record.Id == _record.Id && row.Message != null);
            _baseline = initial ?? _rows.FirstOrDefault(row => row.Message != null);
            ApplyFilter();
            HistoryListView.SelectedItem = _baseline;
            await ShowPayloadAsync(_baseline, true);
        }
        catch (Exception ex)
        {
            if (version == _lifetimeVersion)
                HintText.Text = EngineLocalization.Format($"历史记录加载失败：{ex.Message}");
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        ++_lifetimeVersion;
        ++_selectionVersion;
        ++_baselineVersion;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (HistoryListView != null) ApplyFilter();
    }

    private void ApplyFilter()
    {
        string search = SearchTextBox.Text.Trim();
        FlowNodeComparisonRow? selected = HistoryListView.SelectedItem as FlowNodeComparisonRow;
        var rows = _rows.Where(row => search.Length == 0
            || row.Label.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.MessageId.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.StatusText.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        HistoryListView.ItemsSource = rows;
        HistoryListView.SelectedItem = selected != null && rows.Contains(selected) ? selected : rows.FirstOrDefault();
    }

    private async void HistoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = HistoryListView.SelectedItem as FlowNodeComparisonRow;
        BaselineButton.IsEnabled = row?.Message != null;
        await ShowPayloadAsync(row, false);
    }

    private async void BaselineButton_Click(object sender, RoutedEventArgs e)
    {
        _baseline = HistoryListView.SelectedItem as FlowNodeComparisonRow;
        await ShowPayloadAsync(_baseline, true);
    }

    private async Task ShowPayloadAsync(FlowNodeComparisonRow? row, bool baseline)
    {
        int version = baseline ? ++_baselineVersion : ++_selectionVersion;
        TextBlock label = baseline ? BaselineLabelText : SelectedLabelText;
        TextBlock sendTopic = baseline ? BaselineSendTopicText : SelectedSendTopicText;
        TextBlock receiveTopic = baseline ? BaselineReceiveTopicText : SelectedReceiveTopicText;
        TextBox send = baseline ? BaselineSendTextBox : SelectedSendTextBox;
        TextBox receive = baseline ? BaselineReceiveTextBox : SelectedReceiveTextBox;
        label.Text = row?.Label ?? EngineLocalization.Get("请选择消息");
        sendTopic.Text = row?.Message?.SendTopic ?? string.Empty;
        receiveTopic.Text = row?.Message?.RecvTopic ?? string.Empty;
        send.Text = receive.Text = row?.Message == null ? EngineLocalization.Get("无消息记录") : EngineLocalization.Get("正在加载 Payload…");
        if (row?.Message is not FlowNodeMessage message) return;
        try
        {
            FlowNodeMessagePayloads payloads = message.Id > 0
                ? await Task.Run(() => _session.DataSource.GetMessagePayloads(message.Id))
                : new(message.SendPayload, message.RecvPayload);
            if (version != (baseline ? _baselineVersion : _selectionVersion)) return;
            send.Text = FormatPayload(payloads.SendPayload);
            receive.Text = FormatPayload(payloads.RecvPayload);
        }
        catch (Exception ex)
        {
            if (version == (baseline ? _baselineVersion : _selectionVersion))
                send.Text = receive.Text = EngineLocalization.Format($"Payload 读取失败：{ex.Message}");
        }
    }

    private static string FormatPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return EngineLocalization.Get("未记录正文");
        try { return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(payload), Formatting.Indented); }
        catch (JsonException) { return payload; }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _back();
}
