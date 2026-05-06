using ColorVision.ImageEditor.Cie;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// WindowCIE.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCIE : Window
    {
        private bool _isInitialized;

        public WindowCIE()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        public CieDiagramView DiagramView => CieView;

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            _isInitialized = true;
            UpdateDiagramKind();
            UpdateDisplayedGamuts();
            UpdateDisplayedIlluminants();
            CieView.ZoomUniform();
        }

        public void ChangeSelect(double x, double y)
        {
            CieView.SetSelectedXy(x, y);
        }

        public void ChangeSelect(ImageInfo imageInfo)
        {
            CieView.SetSelectedRgb(imageInfo.R, imageInfo.G, imageInfo.B);
        }

        public void SetDiagram(CieDiagramKind kind)
        {
            CieView.SetDiagram(kind);
            ComboBoxDiagram.SelectedIndex = kind switch
            {
                CieDiagramKind.Cie1960uv => 1,
                CieDiagramKind.Cie1976uv => 2,
                _ => 0
            };
        }

        public void SetGamuts(IEnumerable<CieGamut> gamuts)
        {
            CieView.SetGamuts(gamuts);
        }

        public void AddGamut(CieGamut gamut)
        {
            CieView.AddGamut(gamut);
        }

        public void ClearGamuts()
        {
            CieView.ClearGamuts();
        }

        private void ComboBoxDiagram_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateDiagramKind();
        }

        private void GamutCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateDisplayedGamuts();
        }

        private void IlluminantCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateDisplayedIlluminants();
        }

        private void CctCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            CieView.ShowCctReference = CheckBoxCct.IsChecked == true;
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
        }

        private void UpdateDisplayedGamuts()
        {
            List<CieGamut> gamuts = new();

            if (CheckBoxSRgb.IsChecked == true)
            {
                gamuts.Add(CieGamuts.SRgb);
            }

            if (CheckBoxAdobeRgb.IsChecked == true)
            {
                gamuts.Add(CieGamuts.AdobeRgb);
            }

            if (CheckBoxNtsc.IsChecked == true)
            {
                gamuts.Add(CieGamuts.Ntsc1953);
            }

            if (CheckBoxDciP3.IsChecked == true)
            {
                gamuts.Add(CieGamuts.DciP3);
            }

            if (CheckBoxPal.IsChecked == true)
            {
                gamuts.Add(CieGamuts.Pal);
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

            if (CheckBoxD50.IsChecked == true)
            {
                illuminants.Add(CieIlluminants.D50);
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
    }
}
