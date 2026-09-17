using ColorVision.ImageEditor.Cie;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor;

public partial class WindowCIE
{
    private CieAnalysisSettings _referenceSettings = new();
    private bool _updatingReference;
    private readonly CieMarker _customWhite = new("自定义", CieChromaticity.Empty, Colors.Gray);

    private void InitializeReferenceWhite()
    {
        ReferenceWhitePreset.ItemsSource = new[] { CieIlluminants.D65 }
            .Concat(CieIlluminants.Defaults.Where(w => w != CieIlluminants.D65)).Append(_customWhite).ToArray();
        SyncReferenceWhite(true);
    }

    private void SyncReferenceWhite(bool force = false)
    {
        CieAnalysisSettings settings = SampleAnalysis.CalculationSettings;
        if (!force && _referenceSettings.White == settings.White && _referenceSettings.AbsoluteWhiteLuminance == settings.AbsoluteWhiteLuminance) return;
        _referenceSettings = settings;
        _updatingReference = true;
        ReferenceWhitePreset.SelectedItem = CieIlluminants.Defaults.FirstOrDefault(w => CieAnalysisMath.Distance(w.Chromaticity, settings.White) < 1e-9) ?? _customWhite;
        ReferenceWhiteX.Text = settings.WhiteX.ToString("G", CultureInfo.InvariantCulture);
        ReferenceWhiteY.Text = settings.WhiteY.ToString("G", CultureInfo.InvariantCulture);
        ReferenceWhiteLuminance.Text = settings.AbsoluteWhiteLuminance.ToString("G", CultureInfo.InvariantCulture);
        _updatingReference = false;
        ReferenceWhiteStatus.Text = $"已应用：{CiePointReadout.GetWhiteName(settings.White)} · Yn={settings.AbsoluteWhiteLuminance:G} cd/m²";
        CieView.SetReferenceWhite(settings.White);
        UpdateSelectedReadout();
    }

    private void ReferenceWhitePreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingReference || ReferenceWhitePreset.SelectedItem is not CieMarker marker) return;
        if (marker == _customWhite)
        {
            // Opening the custom editor must not change the applied calculation conditions.
            SyncReferenceWhite(true);
            ShowDisplayOptions.IsChecked = true;
            ReferenceWhiteX.Focus();
            ReferenceWhiteX.SelectAll();
            return;
        }
        ApplyCalculationWhite(marker.Chromaticity, _referenceSettings.AbsoluteWhiteLuminance);
    }

    private void ApplyReferenceWhite_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(ReferenceWhiteX.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(ReferenceWhiteY.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
            !double.TryParse(ReferenceWhiteLuminance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double luminance) ||
            !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(luminance) || x <= 0 || y <= 0 || x + y >= 1 || luminance <= 0)
        {
            ReferenceWhiteStatus.Text = "未应用：请输入有效数字，要求 x>0、y>0、x+y<1、Yn>0。";
            return;
        }
        ApplyCalculationWhite(new(x, y), luminance);
    }

    private void ApplyCalculationWhite(CieChromaticity white, double luminance)
    {
        try
        {
            SampleAnalysis.SetReferenceWhite(white, luminance);
            SyncReferenceWhite(true);
        }
        catch (ArgumentException ex)
        {
            SyncReferenceWhite(true);
            ReferenceWhiteStatus.Text = $"未应用：{ex.Message}";
        }
    }
}
