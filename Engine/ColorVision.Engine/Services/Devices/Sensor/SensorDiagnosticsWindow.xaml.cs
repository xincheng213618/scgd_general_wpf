using ColorVision.Engine.Services.Devices.Sensor.Local;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices.Sensor;

public partial class SensorDiagnosticsWindow : Window
{
    private readonly LocalSensorSession session;
    private readonly Func<string> connectionSummary;
    private readonly ObservableCollection<LocalSensorDiagnosticEntry> entries = new();
    private readonly ICollectionView view;
    private readonly DispatcherTimer timer;
    private long revision = -1;

    public SensorDiagnosticsWindow(DeviceSensor device) : this(device.Config.Name, device.LocalSession,
        () => $"{(device.SensorBackend.OpensLocally ? "本地模式" : "服务模式（此窗口仅记录本地通信）")} · {device.LocalSession.EndpointIdentity ?? (device.Config.IsNet ? $"TCP {device.Config.Addr}:{device.Config.Port}" : $"串口 {device.Config.Addr} / {device.Config.Port} baud")}") { }

    internal SensorDiagnosticsWindow(string deviceName, LocalSensorSession session, Func<string> connectionSummary)
    {
        this.session = session;
        this.connectionSummary = connectionSummary;
        InitializeComponent();
        DeviceTitle.Text = deviceName + " · 通信调试";
        view = CollectionViewSource.GetDefaultView(entries);
        view.Filter = MatchesFilter;
        MessageGrid.ItemsSource = view;
        timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => RefreshRecords();
        Loaded += (_, _) => { RefreshRecords(); timer.Start(); };
        Closed += (_, _) => timer.Stop();
        RefreshRecords();
    }

    private bool MatchesFilter(object item)
    {
        var entry = (LocalSensorDiagnosticEntry)item;
        bool traffic = entry.Kind is LocalSensorDiagnosticKind.Sent or LocalSensorDiagnosticKind.Received;
        if (traffic != (RecordTabs.SelectedIndex == 0)) return false;
        if (traffic && DirectionFilter.SelectedIndex == 1 && entry.Kind != LocalSensorDiagnosticKind.Sent) return false;
        if (traffic && DirectionFilter.SelectedIndex == 2 && entry.Kind != LocalSensorDiagnosticKind.Received) return false;
        string query = SearchBox.Text.Trim();
        return query.Length == 0 || new[] { entry.DisplayText, entry.Text, entry.Hex, entry.CommandDisplay, entry.EventName, entry.Endpoint, entry.Direction }
            .Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    internal void RefreshRecords()
    {
        ConnectionText.Text = connectionSummary();
        StatusText.Text = session.Status switch
        {
            DeviceStatusType.Opened => "已连接", DeviceStatusType.Busy => "执行中", DeviceStatusType.Opening => "连接中",
            DeviceStatusType.Closing => "关闭中", _ => "未连接"
        };
        var snapshot = session.Diagnostics.GetSnapshot(revision, out revision);
        if (snapshot != null)
        {
            while (entries.Count > 0 && (snapshot.Length == 0 || entries[0].Sequence < snapshot[0].Sequence)) entries.RemoveAt(0);
            long last = entries.Count == 0 ? 0 : entries[^1].Sequence;
            foreach (var entry in snapshot) if (entry.Sequence > last) entries.Add(entry);
            CountText.Text = $"发送 {entries.Count(entry => entry.Kind == LocalSensorDiagnosticKind.Sent)} · 接收 {entries.Count(entry => entry.Kind == LocalSensorDiagnosticKind.Received)} · 日志 {entries.Count(entry => entry.Kind is LocalSensorDiagnosticKind.Log or LocalSensorDiagnosticKind.Error)}";
            if (AutoScroll.IsChecked == true && view.Cast<object>().LastOrDefault() is { } latest) MessageGrid.ScrollIntoView(latest);
        }
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        int count = view.Cast<object>().Count();
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = SearchBox.Text.Length > 0 ? "没有匹配的记录" : RecordTabs.SelectedIndex == 0 ? "暂无收发消息" : "暂无运行日志";
        VisibleCount.Text = $"当前显示 {count} 条 / 保留 {entries.Count} 条";
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => RefreshFilter();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == RecordTabs || e.Source == DirectionFilter) RefreshFilter();
    }
    private void RefreshFilter()
    {
        if (view == null) return; // XAML selection/text events run before construction finishes.
        DirectionFilter.IsEnabled = RecordTabs.SelectedIndex == 0;
        view.Refresh();
        UpdateEmptyState();
    }

    private void MessageGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowDetails();
    private void Format_Changed(object sender, RoutedEventArgs e) { if (DetailText != null) ShowDetails(); }
    private void ShowDetails()
    {
        if (MessageGrid.SelectedItem is not LocalSensorDiagnosticEntry entry)
        {
            DetailMeta.Text = "选择一条记录查看可读回显、原文和字节内容。";
            DetailText.Text = DetailOriginalText.Text = DetailHex.Text = "";
            CopyTextButton.IsEnabled = CopyHexButton.IsEnabled = false;
            return;
        }
        DetailMeta.Text = $"{entry.Time:yyyy-MM-dd HH:mm:ss.fff} · {entry.Direction}\n{entry.Endpoint}\n{entry.EventName}";
        if (entry.CommandDisplay.Length > 0) DetailMeta.Text += " · " + entry.CommandDisplay;
        if (entry.ByteCount > 0) DetailMeta.Text += $"\n编码 {entry.Encoding} · {entry.ByteCount} 字节";
        if (entry.IsTruncated) DetailMeta.Text += $"\n仅保留前 {entry.CapturedByteCount} 字节，复制内容同样已截断。";
        string text = entry.DisplayText;
        if (FormatJson.IsChecked == true && !entry.IsTruncated)
        {
            try { text = JToken.Parse(text).ToString(Formatting.Indented); }
            catch (JsonReaderException) { }
        }
        DetailText.Text = text;
        DetailOriginalText.Text = entry.Text;
        DetailHex.Text = string.Join(" ", Enumerable.Range(0, entry.Hex.Length / 2).Select(index => entry.Hex.Substring(index * 2, 2)));
        CopyTextButton.IsEnabled = true;
        CopyHexButton.IsEnabled = entry.Hex.Length > 0;
    }

    private void Clear_Click(object sender, RoutedEventArgs e) { session.Diagnostics.Clear(); RefreshRecords(); }
    private void CopyText_Click(object sender, RoutedEventArgs e) { if (MessageGrid.SelectedItem is LocalSensorDiagnosticEntry entry) Copy(entry.Text); }
    private void CopyHex_Click(object sender, RoutedEventArgs e) { if (MessageGrid.SelectedItem is LocalSensorDiagnosticEntry entry) Copy(entry.Hex); }
    private void Copy(string text)
    {
        try { Clipboard.SetText(text); }
        catch (ExternalException) { MessageBox.Show(this, "剪贴板正在使用，请稍后再试。", "复制消息"); }
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && !((Keyboard.FocusedElement as TextBox)?.SelectionLength > 0))
        { CopyText_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape)
        {
            if (SearchBox.Text.Length > 0) SearchBox.Clear();
            else Close();
            e.Handled = true;
        }
    }
}
