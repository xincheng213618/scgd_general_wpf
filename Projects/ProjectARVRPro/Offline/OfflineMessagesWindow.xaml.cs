using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro.Offline;

public partial class OfflineMessagesWindow : Window
{
    private readonly ArvrOfflineDataSource _source;
    private readonly DateTime _from;
    private readonly DateTime _to;
    private OfflineMessageStore? _store;
    private int _page = 1, _loadVersion, _payloadVersion;
    private bool _ready;

    public OfflineMessagesWindow(ArvrOfflineDataSource source, ResultStatisticsRecordRow record)
    {
        _source = source;
        _from = record.StartTime.AddSeconds(-1);
        _to = record.EndTime.AddSeconds(1);
        InitializeComponent();
        ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
        SourceText.Text = $"{source.Label} · SN {record.SN} · 只读";
        SourceText.ToolTip = source.SourcePath;
        RangeText.Text = $"{_from:yyyy-MM-dd HH:mm:ss.fff} — {_to:HH:mm:ss.fff}\n按本轮前后各 1 秒筛选的候选消息；请结合 SN、MsgID 与连接地址核对关联。";
        _ready = true;
    }
    private async void Window_Loaded(object sender, RoutedEventArgs e) => await ReloadAsync();
    private async void Kind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _page = 1; await ReloadAsync();
    }
    private async Task ReloadAsync()
    {
        int version = ++_loadVersion;
        ++_payloadVersion;
        _store = null; MessagesGrid.ItemsSource = null; PayloadText.Clear();
        PreviousButton.IsEnabled = NextButton.IsEnabled = false;
        bool socket = KindBox.SelectedIndex == 0;
        string? path = socket ? _source.SocketDatabasePath : _source.MqttDatabasePath;
        if (path == null) { StatusText.Text = "这份资料未提供所选消息库。"; return; }
        StatusText.Text = "正在读取…";
        int page = _page;
        try
        {
            var result = await Task.Run(() => { var store = new OfflineMessageStore(path, socket); return (Store: store, Page: store.Query(_from, _to, page)); });
            if (version != _loadVersion) return;
            _store = result.Store;
            MessagesGrid.ItemsSource = result.Page.Rows;
            if (result.Page.Rows.Count > 0) MessagesGrid.SelectedIndex = 0;
            else PayloadText.Text = "此范围内没有消息正文可显示。";
            StatusText.Text = $"共 {result.Page.Count} 条 · 第 {page} 页 · 每页 200 条；所选时间范围内无记录时，可能未采集或不在导出范围内。";
            PreviousButton.IsEnabled = page > 1; NextButton.IsEnabled = page * 200 < result.Page.Count;
        }
        catch (Exception ex) { if (version == _loadVersion) StatusText.Text = $"读取失败：{ex.Message}"; }
    }
    private async void Message_Selected(object sender, SelectionChangedEventArgs e)
    {
        int version = ++_payloadVersion;
        if (MessagesGrid.SelectedItem is not OfflineMessageRow row || _store == null) { PayloadText.Clear(); return; }
        OfflineMessageStore store = _store;
        PayloadText.Text = "正在读取正文…";
        try
        {
            string payload = await Task.Run(() => store.LoadPayload(row.Id));
            if (version != _payloadVersion) return;
            try { payload = JToken.Parse(payload).ToString(Formatting.Indented); } catch (JsonException) { }
            PayloadText.Text = $"记录 {row.Id} · {row.Endpoint}\n\n{payload}";
        }
        catch (Exception ex) { if (version == _payloadVersion) PayloadText.Text = $"正文读取失败：{ex.Message}"; }
    }
    private async void Previous_Click(object sender, RoutedEventArgs e) { --_page; await ReloadAsync(); }
    private async void Next_Click(object sender, RoutedEventArgs e) { ++_page; await ReloadAsync(); }
    protected override void OnClosed(EventArgs e) { ++_loadVersion; ++_payloadVersion; base.OnClosed(e); }
}
