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
            ComboBoxDiagram.SelectedIndex = kind == CieDiagramKind.Cie1976uv ? 1 : 0;
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

        private void UpdateDiagramKind()
        {
            CieDiagramKind kind = ComboBoxDiagram.SelectedIndex == 1
                ? CieDiagramKind.Cie1976uv
                : CieDiagramKind.Cie1931xy;
            CieView.SetDiagram(kind);
        }

        private void UpdateDisplayedGamuts()
        {
            List<CieGamut> gamuts = new();

            if (CheckBoxSRgb.IsChecked == true)
            {
                gamuts.Add(CieGamuts.SRgb);
            }

            if (CheckBoxP3.IsChecked == true)
            {
                gamuts.Add(CieGamuts.DisplayP3);
            }

            if (CheckBoxRec2020.IsChecked == true)
            {
                gamuts.Add(CieGamuts.Rec2020);
            }

            CieView.SetGamuts(gamuts);
        }
    }
}
