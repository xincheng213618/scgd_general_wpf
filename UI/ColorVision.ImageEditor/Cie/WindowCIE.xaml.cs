using ColorVision.ImageEditor.Cie;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// WindowCIE.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCIE : Window
    {
        private bool _isInitialized;
        private bool _isUpdatingOptions;
        private CieChromaticity? _selectedXy;
        private CieAnalysisSample? _analysisSample;

        public WindowCIE()
        {
            InitializeComponent();
            CieView.CursorTextChanged += CieView_CursorTextChanged;
            this.ApplyCaption();
            Loaded += (_, _) => EnsurePageSpace();
            // Owned WPF windows bypass Closing when their owner closes. A sample
            // session must retain its own save/cancel lifecycle once it has data.
            SampleAnalysis.SessionChanged += (_, _) =>
            {
                if (SampleAnalysis.HasUnsavedChanges || SampleAnalysis.Rows.Count > 0) Owner = null;
                SyncReferenceWhite();
            };
            InitializeReferenceWhite();
            SampleAnalysis.PrimarySelected += (primary, xy) =>
            {
                GamutView.SetPrimary(primary, xy);
                CieTabs.SelectedIndex = 1;
            };
        }

        public CieDiagramView DiagramView => CieView;

        public CieSampleAnalysisView SampleAnalysisView => SampleAnalysis;

        public void ShowSampleAnalysis() => CieTabs.SelectedItem = SampleAnalysisTab;
        public void ShowChromaticity() => CieTabs.SelectedIndex = 0;

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel) e.Cancel = !SampleAnalysis.ConfirmClose();
        }

        public void ChangeSelect(CieXyz xyz, string name, string source)
        {
            CieChromaticity xy = CieColorConverter.XyzToCie1931xy(xyz);
            SetSelectedXy(xy, Colors.Black, name);
            try
            {
                CieAnalysisMath.ValidateXyz(xyz);
                _analysisSample = new(Guid.NewGuid(), name, "", source, xyz, CieSampleBasis.Absolute);
                _analysisSample.Validate();
            }
            catch (ArgumentException) { _analysisSample = null; }
            SampleAnalysis.SetSourceSample(_analysisSample);
            UpdateSelectedReadout();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _isInitialized = true;
            UpdateDiagramKind();
            UpdateDisplayedGamuts();
            UpdateDisplayedIlluminants();
            CieView.ShowCctReference = CheckBoxCct.IsChecked == true;
            CieView.ShowDaylightReference = CheckBoxDaylight.IsChecked == true;
            UpdateDiagramSummary();
            UpdateSelectedReadout();
            CieView.ZoomUniform();
        }

        public void ChangeSelect(double x, double y)
        {
            SetSelectedXy(new CieChromaticity(x, y), Colors.Black, "Current");
        }

        public void ChangeSelect(ImagePixelSample pixelSample)
        {
            if (!pixelSample.HasRgbSourceChannels)
            {
                _selectedXy = null;
                _analysisSample = null;
                SampleAnalysis.SetSourceSample(null);
                CieView.ClearSelection();
                UpdateSelectedReadout();
                return;
            }

            CieChromaticity xy = CieColorConverter.RgbToCie1931xy(pixelSample.PreviewColor.R, pixelSample.PreviewColor.G, pixelSample.PreviewColor.B);
            SetSelectedXy(xy, pixelSample.PreviewColor, "RGB");
            _analysisSample = CieAnalysisSample.Create("RGB", "", "sRGB 推算 / D65", CieInputSpace.SRgb,
                pixelSample.PreviewColor.R, pixelSample.PreviewColor.G, pixelSample.PreviewColor.B, CieSampleBasis.Relative, new());
            SampleAnalysis.SetSourceSample(_analysisSample);
            UpdateSelectedReadout();
        }

        public void SetDiagram(CieDiagramKind kind)
        {
            _isUpdatingOptions = true;
            ComboBoxDiagram.SelectedIndex = kind switch
            {
                CieDiagramKind.Cie1960uv => 1,
                CieDiagramKind.Cie1976uv => 2,
                _ => 0
            };
            _isUpdatingOptions = false;

            CieView.SetDiagram(kind);
            UpdateDiagramSummary();
        }

        public void SetGamuts(IEnumerable<CieGamut> gamuts)
        {
            CieView.SetGamuts(gamuts);
        }

        public void SetMarkers(IEnumerable<CieMarker> markers)
        {
            CieView.SetMarkers(markers);
        }

        public void ClearMarkers()
        {
            CieView.ClearMarkers();
        }

        public void SetSelectedMarker(CieMarker? marker)
        {
            if (marker == null)
            {
                _selectedXy = null;
                _analysisSample = null;
                SampleAnalysis.SetSourceSample(null);
                CieView.ClearSelection();
                UpdateSelectedReadout();
                return;
            }

            SetSelectedXy(marker.Chromaticity, marker.Color, marker.Name);
        }

        public void FitDiagram()
        {
            CieView.ZoomUniform();
        }

        public void AddGamut(CieGamut gamut)
        {
            CieView.AddGamut(gamut);
        }

        public void ClearGamuts()
        {
            CieView.ClearGamuts();
        }

        private void ComboBoxDiagram_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingOptions)
            {
                return;
            }

            UpdateDiagramKind();
        }

        private void GamutCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingOptions)
            {
                return;
            }

            UpdateDisplayedGamuts();
        }

        private void IlluminantCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingOptions)
            {
                return;
            }

            UpdateDisplayedIlluminants();
        }

        private void CctCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingOptions)
            {
                return;
            }

            CieView.ShowCctReference = CheckBoxCct.IsChecked == true;
        }

        private void DaylightCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _isUpdatingOptions)
            {
                return;
            }

            CieView.ShowDaylightReference = CheckBoxDaylight.IsChecked == true;
        }

        private void CieView_CursorTextChanged(object? sender, string text)
        {
            TextBlockCursor.Text = text;
        }

        private void ButtonFit_Click(object sender, RoutedEventArgs e)
        {
            CieView.ZoomUniform();
        }

        private void ButtonZoomIn_Click(object sender, RoutedEventArgs e) => CieView.Zoom(1.25);

        private void ButtonZoomOut_Click(object sender, RoutedEventArgs e) => CieView.Zoom(0.8);

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not string preset)
            {
                return;
            }

            ApplyPreset(preset);
        }

        private void UpdateDiagramKind()
        {
            CieDiagramKind kind = ComboBoxDiagram.SelectedIndex switch
            {
                1 => CieDiagramKind.Cie1960uv,
                2 => CieDiagramKind.Cie1976uv,
                _ => CieDiagramKind.Cie1931xy
            };
            CieView.SetDiagram(kind);
            UpdateDiagramSummary();
        }

        private void UpdateDiagramSummary()
        {
            ComboBoxDiagram.ToolTip = CieView.Profile.Name;
        }

        private void UpdateDisplayedGamuts()
        {
            List<CieGamut> gamuts = new();

            if (CheckBoxSRgb.IsChecked == true)
            {
                gamuts.Add(CieGamuts.SRgb);
            }

            if (CheckBoxRec709.IsChecked == true)
            {
                gamuts.Add(CieGamuts.Rec709);
            }

            if (CheckBoxAdobeRgb.IsChecked == true)
            {
                gamuts.Add(CieGamuts.AdobeRgb);
            }

            if (CheckBoxDisplayP3.IsChecked == true)
            {
                gamuts.Add(CieGamuts.DisplayP3);
            }

            if (CheckBoxNtsc.IsChecked == true)
            {
                gamuts.Add(CieGamuts.Ntsc1953);
            }

            if (CheckBoxDciP3.IsChecked == true)
            {
                gamuts.Add(CieGamuts.DciP3);
            }

            if (CheckBoxRec2020.IsChecked == true)
            {
                gamuts.Add(CieGamuts.Rec2020);
            }

            if (CheckBoxPal.IsChecked == true)
            {
                gamuts.Add(CieGamuts.EbuPal);
            }

            if (CheckBoxSmpteC.IsChecked == true)
            {
                gamuts.Add(CieGamuts.SmpteC);
            }

            if (CheckBoxProPhoto.IsChecked == true)
            {
                gamuts.Add(CieGamuts.ProPhotoRgb);
            }

            if (CheckBoxAcesCg.IsChecked == true)
            {
                gamuts.Add(CieGamuts.AcesCg);
            }

            CieView.SetGamuts(gamuts);
        }

        private void UpdateDisplayedIlluminants()
        {
            List<CieMarker> illuminants = new();

            if (CheckBoxD65.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.D65);
            }

            if (CheckBoxE.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.E);
            }

            if (CheckBoxD50.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.D50);
            }

            if (CheckBoxD55.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.D55);
            }

            if (CheckBoxD60.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.D60);
            }

            if (CheckBoxC.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.C);
            }

            if (CheckBoxA.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.A);
            }

            if (CheckBoxD75.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.D75);
            }

            CieView.SetReferenceMarkers(illuminants);
        }

        private void ApplyPreset(string preset)
        {
            _isUpdatingOptions = true;
            try
            {
                SetChecked(GetGamutCheckBoxes(), false);
                SetChecked(GetIlluminantCheckBoxes(), false);
                CheckBoxCct.IsChecked = false;
                CheckBoxDaylight.IsChecked = false;

                switch (preset)
                {
                    case "Display":
                        CheckBoxSRgb.IsChecked = true;
                        CheckBoxRec709.IsChecked = true;
                        CheckBoxAdobeRgb.IsChecked = true;
                        CheckBoxDisplayP3.IsChecked = true;
                        CheckBoxRec2020.IsChecked = true;
                        CheckBoxD65.IsChecked = true;
                        CheckBoxD50.IsChecked = true;
                        CheckBoxCct.IsChecked = true;
                        CheckBoxDaylight.IsChecked = true;
                        break;
                    case "Cinema":
                        CheckBoxRec709.IsChecked = true;
                        CheckBoxDisplayP3.IsChecked = true;
                        CheckBoxDciP3.IsChecked = true;
                        CheckBoxRec2020.IsChecked = true;
                        CheckBoxPal.IsChecked = true;
                        CheckBoxSmpteC.IsChecked = true;
                        CheckBoxD65.IsChecked = true;
                        CheckBoxD60.IsChecked = true;
                        CheckBoxCct.IsChecked = true;
                        CheckBoxDaylight.IsChecked = true;
                        break;
                    case "All":
                        SetChecked(GetGamutCheckBoxes(), true);
                        SetChecked(GetIlluminantCheckBoxes(), true);
                        CheckBoxCct.IsChecked = true;
                        CheckBoxDaylight.IsChecked = true;
                        break;
                }
            }
            finally
            {
                _isUpdatingOptions = false;
            }

            UpdateDisplayedGamuts();
            UpdateDisplayedIlluminants();
            CieView.ShowCctReference = CheckBoxCct.IsChecked == true;
            CieView.ShowDaylightReference = CheckBoxDaylight.IsChecked == true;
        }

        private IEnumerable<CheckBox> GetGamutCheckBoxes()
        {
            yield return CheckBoxSRgb;
            yield return CheckBoxRec709;
            yield return CheckBoxAdobeRgb;
            yield return CheckBoxDisplayP3;
            yield return CheckBoxNtsc;
            yield return CheckBoxDciP3;
            yield return CheckBoxRec2020;
            yield return CheckBoxPal;
            yield return CheckBoxSmpteC;
            yield return CheckBoxProPhoto;
            yield return CheckBoxAcesCg;
        }

        private IEnumerable<CheckBox> GetIlluminantCheckBoxes()
        {
            yield return CheckBoxD65;
            yield return CheckBoxE;
            yield return CheckBoxD50;
            yield return CheckBoxD55;
            yield return CheckBoxD60;
            yield return CheckBoxC;
            yield return CheckBoxA;
            yield return CheckBoxD75;
        }

        private static void SetChecked(IEnumerable<CheckBox> checkBoxes, bool isChecked)
        {
            foreach (CheckBox checkBox in checkBoxes)
            {
                checkBox.IsChecked = isChecked;
            }
        }

        private void SetSelectedXy(CieChromaticity xy, Color color, string name)
        {
            _selectedXy = xy.IsFinite ? xy : null;
            _analysisSample = null;
            if (xy.IsFinite && xy.X >= 0 && xy.Y > 0 && xy.X + xy.Y <= 1)
                _analysisSample = CieAnalysisSample.Create(string.IsNullOrWhiteSpace(name) ? "当前点" : name[..Math.Min(name.Length, 200)], "", "CIE 当前色坐标", CieInputSpace.Xy, xy.X, xy.Y, 0, CieSampleBasis.ChromaticityOnly, new());
            SampleAnalysis.SetSourceSample(_analysisSample);
            if (_selectedXy.HasValue)
            {
                CieView.SetSelectedXy(xy, color, name);
            }
            else
            {
                CieView.ClearSelection();
            }

            UpdateSelectedReadout();
        }

        private void UpdateSelectedReadout()
        {
            UpdateSelectedColorValues();
            if (!_selectedXy.HasValue || !_selectedXy.Value.IsFinite)
            {
                TextBlockSelectedXy.Text = "xy: --";
                TextBlockSelectedUv1960.Text = "uv: --";
                TextBlockSelectedUv1976.Text = "CIE 1976 u′v′: --";
                TextBlockSelectedCct.Text = "相关色温 CCT: --";
                TextBlockSelectedWavelength.Text = "主 / 补波长: —";
                TextBlockSelectedPurity.Text = "激发纯度: —";
                TextBlockSelectedWhiteDistance.Text = $"距 {CiePointReadout.GetWhiteName(_referenceSettings.White)} Δu′v′: —";
                return;
            }

            CieChromaticity xy = _selectedXy.Value;
            var details = new CiePointReadout(xy, _referenceSettings.White);
            CieChromaticity uv1960 = details.Uv1960;
            CieChromaticity uv1976 = details.Uv1976;

            TextBlockSelectedXy.Text = $"xy: x={xy.X:F5}  y={xy.Y:F5}";
            TextBlockSelectedUv1960.Text = uv1960.IsFinite
                ? $"uv: u={uv1960.X:F5}  v={uv1960.Y:F5}"
                : "uv: --";
            TextBlockSelectedUv1976.Text = uv1976.IsFinite
                ? $"CIE 1976 u′v′: u'={uv1976.X:F5}  v'={uv1976.Y:F5}"
                : "CIE 1976 u′v′: --";
            TextBlockSelectedCct.Text = details.CctText;
            TextBlockSelectedWavelength.Text = details.WavelengthText;
            TextBlockSelectedPurity.Text = details.PurityText;
            TextBlockSelectedWhiteDistance.Text = $"距 {details.WhiteName} Δu′v′: {CieAnalysisRow.Format(details.DistanceToWhite, "F6")}";
        }

        private void UpdateSelectedColorValues()
        {
            SelectedColorValues.Visibility = Visibility.Collapsed;
            string whiteName = CiePointReadout.GetWhiteName(_referenceSettings.White);
            TextBlockSelectedConditions.Text = $"计算参考白：{whiteName} (x={_referenceSettings.WhiteX:F5}, y={_referenceSettings.WhiteY:F5})。白点与光谱边界外不定义波长；CCT 为近似值。";
            if (_analysisSample is not { Basis: not CieSampleBasis.ChromaticityOnly } sample) return;
            CieXyz white = _referenceSettings.WhiteFor(sample.Basis);
            CieLab lab = CieAnalysisMath.XyzToLab(sample.Xyz, white);
            CieLuv luv = CieAnalysisMath.XyzToLuv(sample.Xyz, white);
            string F(double value) => CieAnalysisRow.Format(value, "F3");
            SelectedColorValues.Visibility = Visibility.Visible;
            TextBlockSelectedXyz.Text = $"XYZ: {F(sample.Xyz.X)}  {F(sample.Xyz.Y)}  {F(sample.Xyz.Z)}\nY: {F(sample.Xyz.Y)} {(sample.Basis == CieSampleBasis.Absolute ? "cd/m²" : "（sRGB 推算，相对值）")}";
            TextBlockSelectedLab.Text = $"Lab: {F(lab.L)}  {F(lab.A)}  {F(lab.B)}";
            TextBlockSelectedLuv.Text = $"Luv: {F(luv.L)}  {F(luv.U)}  {F(luv.V)}";
            TextBlockSelectedChroma.Text = $"彩度 C*ab: {F(CieAnalysisMath.Chroma(lab))}  色相 hab: {F(CieAnalysisMath.Hue(lab))}°";
            TextBlockSelectedConditions.Text += sample.Basis == CieSampleBasis.Absolute
                ? $" Lab/Luv 参考白亮度 Yn={F(_referenceSettings.AbsoluteWhiteLuminance)} cd/m²。"
                : " Lab/Luv 使用相对参考白 Y=100；sRGB 推算值不是仪器测量。";
        }
    }
}
