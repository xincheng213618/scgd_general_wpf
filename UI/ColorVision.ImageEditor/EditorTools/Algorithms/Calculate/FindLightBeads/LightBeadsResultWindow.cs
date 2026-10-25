using ColorVision.Core;
using Microsoft.Win32;
using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LocalizedText = global::ColorVision.ImageEditor.DisplayText;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FindLightBeads;

public sealed class LightBeadsResultWindow : Window
{
    public LightBeadsResultWindow(LightBeadsInspectionResult result)
    {
        Title = LocalizedText.Get("灯珠检测结果");
        Width = 860; Height = 540; MinWidth = 620; MinHeight = 360;
        SetResourceReference(BackgroundProperty, "GlobalBackground");
        SetResourceReference(ForegroundProperty, "GlobalTextBrush");
        var root = new DockPanel { Margin = new Thickness(12) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock
        {
            Text = LocalizedText.Format($"{result.Grid.Rows} 行 × {result.Grid.Columns} 列    总数 {result.TotalCount}    正常 {result.NormalCount}    不亮/偏暗 {result.BadCount}    未确定 {result.UnknownCount}"),
            FontWeight = FontWeights.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap
        });
        header.Children.Add(new TextBlock
        {
            Text = LocalizedText.Format($"不亮阈值 {result.DarkThreshold}，偏暗阈值 {result.DimThreshold:F2}，同图参考 {result.ReferenceBrightness:F2}。"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
        });
        header.Children.Add(new TextBlock
        {
            Text = LocalizedText.Get(result.InferredSize
                ? "行列数由当前亮点自动估计，整行或边缘全暗可能漏算；可在检测参数中输入标准行列数校验。"
                : "已核对指定行列数。坐标为源图像像素；不亮点使用网格预测坐标。"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8)
        });
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var defectsOnly = new CheckBox { Content = LocalizedText.Get("只看不良/异常点"), IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        actions.Children.Add(defectsOnly);
        foreach (bool onlyBad in new[] { false, true })
        {
            var export = new Button { Content = LocalizedText.Get(onlyBad ? "导出不良 CSV" : "导出全部 CSV"), Margin = new Thickness(0, 0, 8, 0) };
            export.Click += (_, _) => Export(result, onlyBad);
            actions.Children.Add(export);
        }
        header.Children.Add(actions);
        root.Children.Add(header);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, EnableRowVirtualization = true, EnableColumnVirtualization = true };
        VirtualizingPanel.SetIsVirtualizing(grid, true);
        ScrollViewer.SetCanContentScroll(grid, true);
        foreach (var (caption, path, format) in new[]
        {
            ("行", "Row", "{0}"), ("列", "Column", "{0}"), ("X", "X", "{0:F2}"), ("Y", "Y", "{0:F2}"),
            ("状态", "Status", "{0}"), ("亮度", "Brightness", "{0:F2}"), ("检出中心", "Detected", "{0}")
        }) grid.Columns.Add(new DataGridTextColumn { Header = LocalizedText.Get(caption), Binding = new Binding(path)
        {
            StringFormat = format, Converter = path == "Status" ? new StatusConverter() : null
        }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        void Refresh() => grid.ItemsSource = result.Points.Where(point => defectsOnly.IsChecked != true || point.Status != LightBeadStatus.Normal).ToArray();
        defectsOnly.Checked += (_, _) => Refresh();
        defectsOnly.Unchecked += (_, _) => Refresh();
        Refresh();
        root.Children.Add(grid);
        Content = root;
    }

    private void Export(LightBeadsInspectionResult result, bool defectsOnly)
    {
        var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = defectsOnly ? "LED_defects.csv" : "LED_all.csv", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true));
            result.WriteCsv(writer, defectsOnly);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private sealed class StatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => LocalizedText.Get(value switch
        {
            LightBeadStatus.Normal => "正常", LightBeadStatus.Dark => "不亮", LightBeadStatus.Dim => "偏暗", _ => "未确定"
        });
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
