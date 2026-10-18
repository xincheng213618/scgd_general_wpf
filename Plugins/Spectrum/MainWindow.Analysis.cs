using ColorVision.ImageEditor.Draw;
using cvColorVision;
using Spectrum.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Spectrum;

public partial class MainWindow
{
    private SpectrumPlotCursor? relativeCursor;
    private SpectrumPlotCursor? absoluteCursor;
    private readonly SpectrumHoverReadout spectrumReadout = new();
    private GridLength expandedSpectrumResultsHeight = new(1, GridUnitType.Star);

    private void InitializeSpectrumAnalysis()
    {
        if (relativeCursor != null) return;
        relativeCursor = new(wpfplot1, false, UpdateSpectrumReadout);
        absoluteCursor = new(wpfplot2, true, UpdateSpectrumReadout);
    }

    private void UpdateSpectrumReadout(SpectrumCursorReading? reading)
    {
        var result = ViewResultList.SelectedItem as ViewResultSpectrum;
        spectrumReadout.Show(IsShowingAbsoluteSpectrum ? wpfplot2 : wpfplot1, reading, result?.AnalysisSummary,
            result?.AcquisitionFluxMode ?? Config.EqeEnabled, $"#{result?.Id}  {result?.CreateTime:yyyy-MM-dd HH:mm:ss}");
    }

    private void SpectrumResult_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListViewItem { Content: ViewResultSpectrum result } row) return;
        var menu = new ContextMenu { DataContext = result };
        var analyze = new MenuItem { Header = DisplayText.Get("分析此光谱"), DataContext = result };
        analyze.Click += AnalyzeSpectrum_Click;
        var export = new MenuItem { Header = DisplayText.Get("导出此条测量结果 CSV"), DataContext = result };
        export.Click += Export_Click;
        var raw = new MenuItem { Header = DisplayText.Get("导出此条完整原始光谱 CSV"), DataContext = result };
        raw.Click += ExportRawSpectrum_Click;
        menu.Items.Add(analyze); menu.Items.Add(new Separator()); menu.Items.Add(export); menu.Items.Add(raw);
        row.ContextMenu = menu;
        menu.PlacementTarget = row;
        menu.Placement = PlacementMode.MousePoint;
        e.Handled = true;
        menu.IsOpen = true;
    }

    private ViewResultSpectrum[] GetSpectrumExportResults(object sender) => sender is MenuItem { DataContext: ViewResultSpectrum result }
        ? new[] { result } : ViewResultList.SelectedItems.OfType<ViewResultSpectrum>().ToArray();

    private void AnalyzeSpectrum_Click(object sender, RoutedEventArgs e)
    {
        var result = sender is MenuItem { DataContext: ViewResultSpectrum row } ? row : ViewResultList.SelectedItem as ViewResultSpectrum;
        if (result == null) return;
        var window = new SpectrumAnalysisWindow(result.AnalysisSummary, result.fPL, result.fSpect1, result.fInterval, result.fPlambda,
            result.AcquisitionFluxMode ?? Config.EqeEnabled, $"#{result.Id}  {result.CreateTime:yyyy-MM-dd HH:mm:ss}", static wavelength =>
            {
                var rgb = SpectrumChromaticity.DisplayRgb(wavelength);
                return new ScottPlot.Color(rgb.red, rgb.green, rgb.blue);
            }) { Owner = this };
        window.Show();
    }

    private void ToggleSpectrumResults_Click(object sender, RoutedEventArgs e)
    {
        bool visible = ToggleSpectrumResults.IsChecked == true;
        if (!visible) expandedSpectrumResultsHeight = ListRow2.Height;
        ViewResultList.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SpectrumResultsSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ListRow2.MinHeight = visible ? 120 : 0;
        ListRow2.Height = visible ? expandedSpectrumResultsHeight : GridLength.Auto;
    }

    private void UpdateSpectrumCursors(ViewResultSpectrum result, bool absolute)
    {
        InitializeSpectrumAnalysis();
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
        if (selected.Length == 0) { MessageBox.Show(this, DisplayText.Get("请先选择要导出的数据。")); return; }
        using System.Windows.Forms.SaveFileDialog dialog = new()
        {
            Filter = "CSV files (*.csv)|*.csv", DefaultExt = "csv", AddExtension = true,
            FileName = $"SpectrumRaw-{DateTime.Now:yyyyMMdd-HHmmss}.csv", RestoreDirectory = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        try
        {
            var snapshot = await ViewResultManager.CreateRawExportSnapshotAsync(selected, Config.EqeEnabled);
            await SpectrumRawExporter.WriteAsync(dialog.FileName, snapshot);
        }
        catch (Exception ex)
        {
            log.Error("完整光谱导出失败", ex);
            MessageBox.Show(this, DisplayText.Format($"导出失败：{ex.GetBaseException().Message}"));
        }
    }
}
