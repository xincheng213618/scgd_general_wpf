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

        public WindowCIE()
        {
            InitializeComponent();
            CieView.CursorTextChanged += CieView_CursorTextChanged;
            this.ApplyCaption();
        }

        public CieDiagramView DiagramView => CieView;

        private void Window_Initialized(object sender, EventArgs e)
        {
            _isInitialized = true;
            UpdateDiagramKind();
            UpdateDisplayedGamuts();
            UpdateDisplayedIlluminants();
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
                CieView.ClearSelection();
                UpdateSelectedReadout();
                return;
            }

            CieChromaticity xy = CieColorConverter.RgbToCie1931xy(pixelSample.PreviewColor.R, pixelSample.PreviewColor.G, pixelSample.PreviewColor.B);
            SetSelectedXy(xy, pixelSample.PreviewColor, "RGB");
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
            TextBlockCursor.Text = string.IsNullOrWhiteSpace(text) ? "Cursor: --" : text;
        }

        private void ButtonFit_Click(object sender, RoutedEventArgs e)
        {
            CieView.ZoomUniform();
        }

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
            TextBlockDiagramSummary.Text = CieView.Profile.Name;
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
            if (!_selectedXy.HasValue || !_selectedXy.Value.IsFinite)
            {
                TextBlockSelectedXy.Text = "xy: --";
                TextBlockSelectedUv1960.Text = "uv: --";
                TextBlockSelectedUv1976.Text = "u'v': --";
                TextBlockSelectedCct.Text = "CCT: --";
                return;
            }

            CieChromaticity xy = _selectedXy.Value;
            CieChromaticity uv1960 = CieColorConverter.XyToCie1960uv(xy);
            CieChromaticity uv1976 = CieColorConverter.XyToCie1976uv(xy);
            CieCctResult cct = CieColorConverter.EstimateCctAndDuv(xy);

            TextBlockSelectedXy.Text = $"xy: x={xy.X:F5}  y={xy.Y:F5}";
            TextBlockSelectedUv1960.Text = uv1960.IsFinite
                ? $"uv: u={uv1960.X:F5}  v={uv1960.Y:F5}"
                : "uv: --";
            TextBlockSelectedUv1976.Text = uv1976.IsFinite
                ? $"u'v': u'={uv1976.X:F5}  v'={uv1976.Y:F5}"
                : "u'v': --";
            TextBlockSelectedCct.Text = cct.IsFinite
                ? $"CCT: {cct.TemperatureKelvin:F0}K  Duv={cct.Duv:+0.00000;-0.00000;0.00000}"
                : "CCT: --";
        }
    }
}
