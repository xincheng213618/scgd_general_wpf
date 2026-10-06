using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Editing;

public partial class CalibrationBinaryEditorWindow : Window
{
    private const int PageSize = 200;
    private CalibrationBinaryFileSnapshot snapshot;
    private readonly CalibrationBinaryDocument document;
    private readonly ObservableCollection<BinaryRow> rows = new();
    private int page;
    private bool dirty;
    private bool ready;
    private int Count => document.Type == ServiceTypes.DefectPoint ? document.PointCount : document.ValueCount;
    private int Pages => Math.Max(1, (Count + PageSize - 1) / PageSize);
    public CalibrationBinaryEditorWindow(ServiceTypes type, string path)
    {
        snapshot = CalibrationBinaryFileStore.Load(path);
        document = CalibrationBinaryDocument.Parse(type, snapshot.Bytes);
        InitializeComponent();
        this.ApplyCaption();
        Title = type + " · " + System.IO.Path.GetFileName(path);
        Values.ItemsSource = rows;
        bool points = type == ServiceTypes.DefectPoint;
        ValueColumn.Visibility = points ? Visibility.Collapsed : Visibility.Visible;
        RowColumn.Visibility = ColumnColumn.Visibility = points ? Visibility.Visible : Visibility.Collapsed;
        AddPoint.Visibility = RemovePoint.Visibility = points ? Visibility.Visible : Visibility.Collapsed;
        if (document.IsMap)
        {
            Summary.Text = $"{type} · V{document.Version} · {document.Width} × {document.Height} · {document.Channels} 通道 · 源位深 {document.SourceBits}\n范围 {document.Minimum:G8} 至 {document.Maximum:G8}；映射仅查看，预览为采样热力图，V1 按像素交错通道。";
            Values.Visibility = Curve.Visibility = Visibility.Collapsed;
            Grid.SetColumn(MapPreview.Parent as Grid ?? throw new InvalidOperationException(), 0);
            Grid.SetColumnSpan((UIElement)MapPreview.Parent, 2);
            Previous.Visibility = Next.Visibility = PageNumber.Visibility = GoPage.Visibility = PageInfo.Visibility = Visibility.Collapsed;
            Save.IsEnabled = false;
            // Real calibration maps have few channels; cap the dropdown, never allocate per pixel.
            for (int i = 0; i < Math.Min(document.Channels, 64); i++)
                ChannelSelector.Items.Add(document.Channels == 3 ? $"{i + 1}（{"BGR"[i]}）" : (i + 1).ToString(CultureInfo.InvariantCulture));
            ChannelSelector.SelectedIndex = 0;
        }
        else
        {
            ChannelSelector.Visibility = MapPreview.Visibility = Visibility.Collapsed;
            Summary.Text = points ? "缺陷点坐标 · 文件按 Y / X 顺序保存；坐标必须为非负 32 位整数。当前没有图像尺寸，无法检查是否越过图像边界。" : $"线性度逐像素乘数表 · {Count:N0} 项 · 每页 {PageSize} 项；曲线横轴为像素位置索引，采样最多 800 点。文件没有尺寸信息，需核对图像尺寸及适用条件。";
        }
        ready = true;
        ShowPage(); RenderPreview();
        Closing += Window_Closing;
    }

    public sealed class BinaryRow
    {
        public int Index { get; set; }
        public string Value { get; set; } = "";
        public string Row { get; set; } = "";
        public string Column { get; set; } = "";
    }

    private void ShowPage()
    {
        rows.Clear();
        if (!document.IsMap)
        {
            page = Math.Clamp(page, 0, Pages - 1);
            for (int i = page * PageSize; i < Math.Min(Count, (page + 1) * PageSize); i++)
            {
                var row = new BinaryRow { Index = i };
                if (document.Type == ServiceTypes.DefectPoint) { var point = document.GetPoint(i); row.Row = point.Row.ToString(CultureInfo.InvariantCulture); row.Column = point.Column.ToString(CultureInfo.InvariantCulture); }
                else row.Value = document.GetValue(i).ToString("R", CultureInfo.InvariantCulture);
                rows.Add(row);
            }
        }
        PageNumber.Text = (page + 1).ToString(CultureInfo.InvariantCulture);
        PageInfo.Text = $"/ {Pages} 页 · {Count:N0} 项";
        Previous.IsEnabled = page > 0; Next.IsEnabled = page + 1 < Pages;
    }

    private bool CommitPage()
    {
        try
        {
            Values.CommitEdit(DataGridEditingUnit.Cell, true); Values.CommitEdit(DataGridEditingUnit.Row, true);
            // Validate the complete page before applying any of its edits.
            foreach (var row in rows)
            {
                if (document.Type == ServiceTypes.DefectPoint)
                {
                    if (!uint.TryParse(row.Row, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || !uint.TryParse(row.Column, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) throw new FormatException($"坐标 {row.Index} 必须为非负 32 位整数。");
                }
                else if (!float.TryParse(row.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !float.IsFinite(n)) throw new FormatException($"系数 {row.Index} 必须是有限浮点数。");
            }
            foreach (var row in rows)
            {
                if (document.Type == ServiceTypes.DefectPoint)
                {
                    var point = new CalibrationDefectPoint(uint.Parse(row.Row, CultureInfo.InvariantCulture), uint.Parse(row.Column, CultureInfo.InvariantCulture));
                    if (document.GetPoint(row.Index) != point) { document.SetPoint(row.Index, point); dirty = true; }
                }
                else { float value = float.Parse(row.Value, CultureInfo.InvariantCulture); if (document.GetValue(row.Index) != value) { document.SetValue(row.Index, value); dirty = true; } }
            }
            return true;
        }
        catch (Exception ex) { Status.Text = ex.Message; return false; }
    }

    private void Previous_Click(object sender, RoutedEventArgs e) { if (CommitPage()) { page--; ShowPage(); RenderPreview(); } }
    private void Next_Click(object sender, RoutedEventArgs e) { if (CommitPage()) { page++; ShowPage(); RenderPreview(); } }
    private void GoPage_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PageNumber.Text, out int requested) || requested < 1 || requested > Pages) { Status.Text = $"请输入 1 至 {Pages} 的页码。"; return; }
        if (CommitPage()) { page = requested - 1; ShowPage(); RenderPreview(); }
    }
    private void AddPoint_Click(object sender, RoutedEventArgs e) { if (CommitPage()) { document.AddPoint(new(0, 0)); dirty = true; page = Pages - 1; ShowPage(); RenderPreview(); } }
    private void RemovePoint_Click(object sender, RoutedEventArgs e) { if (Values.SelectedItem is BinaryRow selected && CommitPage()) { document.RemovePoint(selected.Index); dirty = true; ShowPage(); RenderPreview(); } }
    private void ChannelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ready) RenderPreview(); }
    private void ChannelSelector_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e) { if (ready) RenderPreview(); }
    private void Curve_SizeChanged(object sender, SizeChangedEventArgs e) { if (ready) RenderPreview(); }

    private void RenderPreview()
    {
        if (document.IsMap)
        {
            int width = (int)Math.Min(document.Width, 640), height = (int)Math.Min(document.Height, 480);
            var pixels = new byte[width * height * 4];
            int channel = Math.Max(0, ChannelSelector.SelectedIndex);
            if (int.TryParse(ChannelSelector.Text, out int requested)) channel = requested - 1;
            if (channel < 0 || channel >= document.Channels) { Status.Text = $"通道编号必须在 1 至 {document.Channels} 之间。"; return; }
            double range = document.Maximum - document.Minimum;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                long sourceX = x * (long)document.Width / width, sourceY = y * (long)document.Height / height;
                int index = checked((int)((sourceY * document.Width + sourceX) * document.Channels + channel));
                double t = range == 0 ? 0.5 : Math.Clamp((document.GetValue(index) - document.Minimum) / range, 0, 1);
                int p = (y * width + x) * 4; pixels[p] = (byte)(255 * (1 - t)); pixels[p + 1] = (byte)(255 * (1 - Math.Abs(2 * t - 1))); pixels[p + 2] = (byte)(255 * t); pixels[p + 3] = 255;
            }
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); bitmap.Freeze(); MapPreview.Source = bitmap;
            return;
        }
        Curve.Children.Clear();
        if (Count == 0 || Curve.ActualWidth <= 0 || Curve.ActualHeight <= 0) return;
        int samples = Math.Min(Count, 800);
        double min = double.PositiveInfinity, max = double.NegativeInfinity, maxX = 1, maxY = 1;
        for (int n = 0; n < samples; n++) { int i = (int)(n * (long)(Count - 1) / Math.Max(1, samples - 1)); if (document.Type == ServiceTypes.DefectPoint) { var p = document.GetPoint(i); maxX = Math.Max(maxX, p.Column); maxY = Math.Max(maxY, p.Row); } else { double value = document.GetValue(i); min = Math.Min(min, value); max = Math.Max(max, value); } }
        var line = new Polyline { Stroke = Brushes.Teal, StrokeThickness = 1.5 };
        for (int n = 0; n < samples; n++)
        {
            int i = (int)(n * (long)(Count - 1) / Math.Max(1, samples - 1));
            if (document.Type == ServiceTypes.DefectPoint)
            {
                var p = document.GetPoint(i); var dot = new Ellipse { Width = 4, Height = 4, Fill = Brushes.OrangeRed };
                Canvas.SetLeft(dot, p.Column / maxX * Math.Max(0, Curve.ActualWidth - 4)); Canvas.SetTop(dot, p.Row / maxY * Math.Max(0, Curve.ActualHeight - 4)); Curve.Children.Add(dot);
            }
            else line.Points.Add(new(n * Curve.ActualWidth / Math.Max(1, samples - 1), (max == min ? 0.5 : 1 - (document.GetValue(i) - min) / (max - min)) * Curve.ActualHeight));
        }
        if (document.Type == ServiceTypes.LineArity) Curve.Children.Add(line);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrent();
    }
    private bool SaveCurrent()
    {
        if (!CommitPage()) return false;
        try { var bytes = document.BuildBytes(); _ = CalibrationBinaryDocument.Parse(document.Type, bytes); snapshot = CalibrationBinaryFileStore.Save(snapshot, bytes); dirty = false; Status.Text = "已保存；原文件备份：" + snapshot.LastBackupPath; RenderPreview(); return true; }
        catch (Exception ex) { Status.Text = ex.Message; return false; }
    }
    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (!CommitPage()) return;
        var dialog = new SaveFileDialog { FileName = System.IO.Path.GetFileNameWithoutExtension(snapshot.Path) + "_edited.dat", Filter = "校正文件|*.dat|所有文件|*.*", OverwritePrompt = false };
        if (dialog.ShowDialog(this) != true) return;
        try { var bytes = document.BuildBytes(); _ = CalibrationBinaryDocument.Parse(document.Type, bytes); snapshot = CalibrationBinaryFileStore.SaveAs(dialog.FileName, bytes, snapshot); dirty = false; Title = document.Type + " · " + System.IO.Path.GetFileName(snapshot.Path); Status.Text = "已另存为 " + snapshot.Path; }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!CommitPage()) { e.Cancel = MessageBox.Show(this, "存在无效输入，关闭将放弃编辑。继续关闭？", "校正编辑", MessageBoxButton.YesNo) != MessageBoxResult.Yes; return; }
        if (dirty)
        {
            var answer = MessageBox.Show(this, "修改尚未保存。是否保存后关闭？", "校正编辑", MessageBoxButton.YesNoCancel);
            e.Cancel = answer == MessageBoxResult.Cancel || (answer == MessageBoxResult.Yes && !SaveCurrent());
        }
    }
}
