using ColorVision.ImageEditor.Draw;
using cvColorVision;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LocalizedText = global::ColorVision.Engine.EngineLocalization;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views;

public partial class ViewSpectrum
{
    private SpectrumPlotCursor? relativeCursor;
    private SpectrumPlotCursor? absoluteCursor;
    private readonly SpectrumHoverReadout spectrumReadout = new();

    private void InitializeSpectrumAnalysis()
    {
        relativeCursor = new(wpfplot1, false, UpdateSpectrumReadout);
        absoluteCursor = new(wpfplot2, true, UpdateSpectrumReadout);
    }

    private void UpdateSpectrumReadout(SpectrumCursorReading? reading)
    {
        var result = listView1.SelectedItem as ViewResultSpectrum;
        spectrumReadout.Show(IsShowingAbsoluteSpectrum ? wpfplot2 : wpfplot1, reading, result?.AnalysisSummary,
            result?.AcquisitionFluxMode ?? DisplayConfig.IsLuminousFluxMode, $"#{result?.Id}  {result?.CreateTime:yyyy-MM-dd HH:mm:ss}");
    }

    private void SpectrumResult_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListViewItem { Content: ViewResultSpectrum result } row) return;
        var menu = new ContextMenu { DataContext = result };
        var analyze = new MenuItem { Header = LocalizedText.Get("分析此光谱"), DataContext = result };
        analyze.Click += AnalyzeSpectrum_Click;
        var export = new MenuItem { Header = LocalizedText.Get("导出此条测量结果 CSV"), DataContext = result };
        export.Click += Button_Click;
        var raw = new MenuItem { Header = LocalizedText.Get("导出此条完整原始光谱 CSV"), DataContext = result };
        raw.Click += ExportRawSpectrum_Click;
        menu.Items.Add(analyze); menu.Items.Add(new Separator()); menu.Items.Add(export); menu.Items.Add(raw);
        row.ContextMenu = menu;
        menu.PlacementTarget = row;
        menu.Placement = PlacementMode.MousePoint;
        e.Handled = true;
        menu.IsOpen = true;
    }

    private ViewResultSpectrum[] GetSpectrumExportResults(object sender) => sender is MenuItem { DataContext: ViewResultSpectrum result }
        ? new[] { result } : listView1.SelectedItems.OfType<ViewResultSpectrum>().ToArray();

    private void AnalyzeSpectrum_Click(object sender, RoutedEventArgs e)
    {
        var result = sender is MenuItem { DataContext: ViewResultSpectrum row } ? row : listView1.SelectedItem as ViewResultSpectrum;
        if (result == null) return;
        var window = new SpectrumAnalysisWindow(result.AnalysisSummary, result.fPL, result.fSpect1, result.fInterval, result.fPlambda,
            result.AcquisitionFluxMode ?? DisplayConfig.IsLuminousFluxMode, $"#{result.Id}  {result.CreateTime:yyyy-MM-dd HH:mm:ss}", static wavelength =>
            {
                var rgb = SpectrumChromaticity.DisplayRgb(wavelength);
                return new ScottPlot.Color(rgb.red, rgb.green, rgb.blue);
            }) { Owner = Window.GetWindow(this) };
        window.Show();
    }

    private void UpdateSpectrumCursors(ViewResultSpectrum result, bool absolute)
    {
        (absolute ? relativeCursor : absoluteCursor)?.Clear();
        (absolute ? absoluteCursor : relativeCursor)?.SetSpectrum(result.fPL, result.fSpect1, result.fInterval, result.fPlambda);
    }

    private void OpenSpectrumExportMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
        }
    }

    private async void ExportRawSpectrum_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSpectrumExportResults(sender);
        if (selected.Length == 0) { MessageBox.Show(LocalizedText.Get("请先选择要导出的数据。")); return; }
        using System.Windows.Forms.SaveFileDialog dialog = new()
        {
            Filter = "CSV files (*.csv)|*.csv", DefaultExt = "csv", AddExtension = true,
            FileName = $"SpectrumRaw-{DateTime.Now:yyyyMMdd-HHmmss}.csv", RestoreDirectory = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        try
        {
            var snapshot = selected.Select(result => result.CreateRawExportRecord(DisplayConfig.IsLuminousFluxMode)).ToArray();
            await SpectrumRawExporter.WriteAsync(dialog.FileName, snapshot);
        }
        catch (Exception ex) { MessageBox.Show(LocalizedText.Format($"导出失败：{ex.GetBaseException().Message}")); }
    }
}
