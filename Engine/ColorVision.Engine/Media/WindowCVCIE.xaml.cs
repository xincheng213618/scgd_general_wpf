#pragma warning disable CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Media
{
    public class CVCIEShowConfig : ViewModelBase, IConfig
    {
        public static CVCIEShowConfig Instance => ConfigService.Instance.GetRequiredService<CVCIEShowConfig>();
        public RelayCommand EditCommand { get; set; }

        public CVCIEShowConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }
        [Display(Name = "Engine_PG_ShowRecordData", ResourceType = typeof(Properties.Resources))]
        public bool IsShowString { get => _IsShowString; set { _IsShowString = value; OnPropertyChanged(); } }
        private bool _IsShowString = true;
        [Display(Name = "Engine_PG_DataDisplayTemplate", ResourceType = typeof(Properties.Resources))]
        public string Template { get => _Template;set { _Template = value;  OnPropertyChanged(); } }
        private string _Template = "X:@X:F1 Y:@Y:F1 Z:@Z:F1\\nx:@x:F4 y:@y:F4 u:@u:F4 v:@v:F4\\nCCT:@CCT:F1 Wave:@Wave:F1";
    }

    /// <summary>
    /// WindowCVCIE.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCVCIE : Window
    {
        private static readonly Color[] MarkerPalette =
        {
            Color.FromRgb(230, 74, 25),
            Color.FromRgb(46, 125, 50),
            Color.FromRgb(21, 101, 192),
            Color.FromRgb(123, 31, 162),
            Color.FromRgb(0, 131, 143),
            Color.FromRgb(173, 20, 87),
            Color.FromRgb(251, 140, 0),
            Color.FromRgb(93, 64, 55),
        };

        public ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }
        public CIExyuvStatistics CIExyuvStats { get; set; }
        private WindowCIE? cieWindow;

        bool IsPoiResultCIExyuvDatas = true;
        public WindowCVCIE(ObservableCollection<PoiResultCIExyuvData> poiResultCIExyuvDatas)
        {
            IsPoiResultCIExyuvDatas = true;
            PoiResultCIExyuvDatas = poiResultCIExyuvDatas;  
            
            // Calculate statistics when window is created
            CIExyuvStats = CIExyuvStatistics.Calculate(poiResultCIExyuvDatas);
            
            InitializeComponent();
            this.ApplyCaption();

            var cieBdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };
            var cieHeader = new List<string> { Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "CCT", "Wave", "X", "Y", "Z", "u'", "v'", "x", "y" };

            if (listViewSide.View is GridView gridViewPOI_XY_UV)
            {
                LeftGridViewColumnVisibilitys.Clear();
                gridViewPOI_XY_UV.Columns.Clear();
                for (int i = 0; i < cieHeader.Count; i++)
                    gridViewPOI_XY_UV.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
            }
            listViewSide.ItemsSource = PoiResultCIExyuvDatas;
            ButtonShowOnCieDiagram.IsEnabled = true;
        }
        public ObservableCollection<PoiResultCIEYData> PoiResultCIEYDatas { get; set; }
        public CIEYStatistics CIEYStats { get; set; }

        public WindowCVCIE(ObservableCollection<PoiResultCIEYData> poiResultCIEYDatas)
        {
            IsPoiResultCIExyuvDatas = false;
            PoiResultCIEYDatas = poiResultCIEYDatas;
            
            // Calculate statistics when window is created
            CIEYStats = CIEYStatistics.Calculate(poiResultCIEYDatas);
            
            InitializeComponent();
            this.ApplyCaption();
              
            var cieBdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "Y"};
            var cieHeader = new List<string> { Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "Y" };

            if (listViewSide.View is GridView gridViewPOI_XY_UV)
            {
                LeftGridViewColumnVisibilitys.Clear();
                gridViewPOI_XY_UV.Columns.Clear();
                for (int i = 0; i < cieHeader.Count; i++)
                    gridViewPOI_XY_UV.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
            }
            listViewSide.ItemsSource = poiResultCIEYDatas;
            ButtonShowOnCieDiagram.IsEnabled = false;
            ButtonShowOnCieDiagram.ToolTip = "仅包含 xy 色坐标的结果可显示到 CIE 色度图";
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = CVCIEShowConfig.Instance;

            listViewSide.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listViewSide.SelectAll(), (s, e) => e.CanExecute = true));
            listViewSide.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

            // Populate statistics panel
            PopulateStatisticsPanel();
        }

        private void PopulateStatisticsPanel()
        {
            StatisticsPanel.Children.Clear();

            if (IsPoiResultCIExyuvDatas && CIExyuvStats != null)
            {
                // Create WrapPanel for better layout
                var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };

                // Luminance statistics
                AddStatisticItem(wrapPanel, "Center Luminance:", $"{CIExyuvStats.CenterLuminance:F2} cd/m²");
                AddStatisticItem(wrapPanel, "Average Luminance:", $"{CIExyuvStats.AverageLuminance:F2} cd/m²");
                AddStatisticItem(wrapPanel, "Max Luminance:", $"{CIExyuvStats.MaxLuminance:F2} cd/m²");
                AddStatisticItem(wrapPanel, "Min Luminance:", $"{CIExyuvStats.MinLuminance:F2} cd/m²");
                
                // Uniformity
                AddStatisticItem(wrapPanel, "Uniformity (Min/Max):", $"{CIExyuvStats.UniformityMinDivMax:F2}%");
                AddStatisticItem(wrapPanel, "Uniformity ((Max-Min)/Avg):", $"{CIExyuvStats.UniformityDiffDivAvg:F2}%");
                AddStatisticItem(wrapPanel, "Uniformity ((Max-Min)/Max):", $"{CIExyuvStats.UniformityDiffDivMax:F2}%");
                AddStatisticItem(wrapPanel, "Uniformity 1-((Max-Avg)/Avg):", $"{CIExyuvStats.Uniformity:F2}%");

                // Standard Deviation
                if (!double.IsNaN(CIExyuvStats.StandardDeviation))
                {
                    AddStatisticItem(wrapPanel, "Standard Deviation:", $"{CIExyuvStats.StandardDeviation:F4}");
                    if (!double.IsNaN(CIExyuvStats.StandardDeviationPercent))
                        AddStatisticItem(wrapPanel, "Standard Deviation (%):", $"{CIExyuvStats.StandardDeviationPercent:F2}%");
                }

                // Color uniformity
                AddStatisticItem(wrapPanel, "Color Uniformity (Δuv):", $"{CIExyuvStats.ColorUniformityDeltaUv:F6}");
                AddStatisticItem(wrapPanel, "Color Uniformity (Δx):", $"{CIExyuvStats.ColorUniformityDeltaX:F6}");
                AddStatisticItem(wrapPanel, "Color Uniformity (Δy):", $"{CIExyuvStats.ColorUniformityDeltaY:F6}");

                // Center color coordinates
                if (CIExyuvStats.CenterLuminance > 0)
                {
                    AddStatisticItem(wrapPanel, "Center x:", $"{CIExyuvStats.CenterX:F4}");
                    AddStatisticItem(wrapPanel, "Center y:", $"{CIExyuvStats.CenterY:F4}");
                    AddStatisticItem(wrapPanel, "Center u':", $"{CIExyuvStats.CenterU:F4}");
                    AddStatisticItem(wrapPanel, "Center v':", $"{CIExyuvStats.CenterV:F4}");
                    AddStatisticItem(wrapPanel, "Center CCT:", $"{CIExyuvStats.CenterCCT:F1} K");
                    AddStatisticItem(wrapPanel, "Center Wave:", $"{CIExyuvStats.CenterWave:F1} nm");
                }

                AddStatisticItem(wrapPanel, "Delta Wave:", $"{CIExyuvStats.DeltaWave:F1} nm");

                StatisticsPanel.Children.Add(wrapPanel);
            }
            else if (!IsPoiResultCIExyuvDatas && CIEYStats != null)
            {
                // Create WrapPanel for better layout
                var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };

                // Luminance statistics
                AddStatisticItem(wrapPanel, "Center Luminance:", $"{CIEYStats.CenterLuminance:F2} cd/m²");
                AddStatisticItem(wrapPanel, "Average Luminance:", $"{CIEYStats.AverageLuminance:F2} cd/m²");
                AddStatisticItem(wrapPanel, "Max Luminance:", $"{CIEYStats.MaxLuminance:F2} cd/m²");
                AddStatisticItem(wrapPanel, "Min Luminance:", $"{CIEYStats.MinLuminance:F2} cd/m²");
                
                // Uniformity
                AddStatisticItem(wrapPanel, "Uniformity (Min/Max):", $"{CIEYStats.UniformityMinDivMax:F2}%");
                AddStatisticItem(wrapPanel, "Uniformity ((Max-Min)/Avg):", $"{CIEYStats.UniformityDiffDivAvg:F2}%");
                AddStatisticItem(wrapPanel, "Uniformity ((Max-Min)/Max):", $"{CIEYStats.UniformityDiffDivMax:F2}%");
                
                // Standard Deviation
                AddStatisticItem(wrapPanel, "Standard Deviation:", $"{CIEYStats.StandardDeviation:F4}");
                AddStatisticItem(wrapPanel, "Standard Deviation (%):", $"{CIEYStats.StandardDeviationPercent:F2}%");

                StatisticsPanel.Children.Add(wrapPanel);
            }
        }

        private static void AddStatisticItem(WrapPanel parent, string label, string value)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 2, 10, 2),
                Padding = new Thickness(5, 2, 5, 2)
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var labelText = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = System.Windows.Media.Brushes.Gray
            };

            var valueText = new TextBlock
            {
                Text = value,
                FontWeight = FontWeights.Normal
            };

            stackPanel.Children.Add(labelText);
            stackPanel.Children.Add(valueText);
            border.Child = stackPanel;
            parent.Children.Add(border);
        }


        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();


        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && listViewSide.View is GridView gridView && LeftGridViewColumnVisibilitys.Count == 0)
                GridViewColumnVisibility.GenContentMenuGridViewColumnZero(contextMenu, gridView.Columns, LeftGridViewColumnVisibilitys);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            if (IsPoiResultCIExyuvDatas)
            {
                PoiResultCIExyuvDatas.SaveCsv(dialog.FileName);
            }
            else
            {
                PoiResultCIEYData.SaveCsv(PoiResultCIEYDatas, dialog.FileName);
            }
        }

        private void ButtonShowOnCieDiagram_Click(object sender, RoutedEventArgs e)
        {
            if (!IsPoiResultCIExyuvDatas)
            {
                MessageBox.Show(this, ColorVision.Engine.Properties.Resources.Engine_Msg_ResultOnlyYNoColorCoords, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IReadOnlyList<CieMarker> markers = BuildCieMarkers();
            if (markers.Count == 0)
            {
                MessageBox.Show(this, ColorVision.Engine.Properties.Resources.Engine_Msg_NoDisplayableCiePoints, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            EnsureCieWindow();
            cieWindow!.SetMarkers(markers);
            cieWindow.SetSelectedMarker(null);
            UpdateCieSelectionFromList();
            cieWindow.FitDiagram();
            cieWindow.Show();
            cieWindow.Activate();
        }

        private void ListViewSide_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCieSelectionFromList();
        }

        private void EnsureCieWindow()
        {
            if (cieWindow != null)
            {
                return;
            }

            cieWindow = new WindowCIE
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            cieWindow.Closed += (_, _) => cieWindow = null;
        }

        private void UpdateCieSelectionFromList()
        {
            if (cieWindow == null || !IsPoiResultCIExyuvDatas)
            {
                return;
            }

            if (listViewSide.SelectedItem is not PoiResultCIExyuvData selectedItem || !TryCreateMarker(selectedItem, GetMarkerColor(PoiResultCIExyuvDatas.IndexOf(selectedItem)), out CieMarker? marker))
            {
                cieWindow.SetSelectedMarker(null);
                return;
            }

            cieWindow.SetSelectedMarker(new CieMarker(string.Empty, marker.Chromaticity, marker.Color));
        }

        private IReadOnlyList<CieMarker> BuildCieMarkers()
        {
            if (PoiResultCIExyuvDatas == null)
            {
                return Array.Empty<CieMarker>();
            }

            List<CieMarker> markers = new();
            for (int index = 0; index < PoiResultCIExyuvDatas.Count; index++)
            {
                PoiResultCIExyuvData item = PoiResultCIExyuvDatas[index];
                if (TryCreateMarker(item, GetMarkerColor(index), out CieMarker? marker))
                {
                    markers.Add(marker);
                }
            }

            return markers;
        }

        private static bool TryCreateMarker(PoiResultCIExyuvData item, Color color, out CieMarker? marker)
        {
            marker = null;
            if (item == null)
            {
                return false;
            }

            CieChromaticity chromaticity = new(item.x, item.y);
            if (!chromaticity.IsFinite)
            {
                return false;
            }

            string name = string.IsNullOrWhiteSpace(item.Name) ? "Point" : item.Name;
            marker = new CieMarker(name, chromaticity, color);
            return true;
        }

        private static Color GetMarkerColor(int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            return MarkerPalette[index % MarkerPalette.Length];
        }


    }
}
