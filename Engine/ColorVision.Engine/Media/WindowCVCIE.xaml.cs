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
using System.Windows.Documents;

namespace ColorVision.Engine.Media
{
    public class CVCIEShowConfig : ViewModelBase, IConfig
    {
        private static readonly CVCIEShowConfig DefaultConfig = new();
        public static CVCIEShowConfig Instance => ConfigService.Instance?.GetRequiredService<CVCIEShowConfig>() ?? DefaultConfig;
        public RelayCommand EditCommand { get; set; }

        public CVCIEShowConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }
        [Display(Name = "Engine_PG_ShowRecordData", ResourceType = typeof(Properties.Resources))]
        public bool IsShowString { get => _IsShowString; set { _IsShowString = value; OnPropertyChanged(); } }
        private bool _IsShowString = true;

        [Category("结果显示"), DisplayName("小数位数")]
        [Description("仅控制界面显示，不改变计算值及 CSV 导出。")]
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set { if (value < 0 || value > 9 || value == _decimalPlaces) return; _decimalPlaces = value; OnPropertyChanged(); }
        }
        private int _decimalPlaces = 3;
        [Display(Name = "Engine_PG_DataDisplayTemplate", ResourceType = typeof(Properties.Resources))]
        public string Template { get => _Template;set { _Template = value;  OnPropertyChanged(); } }
        private string _Template = "X:@X:F3  Y:@Y:F3  Z:@Z:F3\\nx:@x:F3  y:@y:F3\\nu′:@u:F3  v′:@v:F3\\nCCT:@CCT:F3 K  λd:@Wave:F3 nm";

        [Category("计算结果"), DisplayName("启用非正值替换")]
        [Description("替换非正 XYZ，并重算 xy、u′v′、CCT 和主波长；关闭后显示实际值。")]
        public bool ClampNonPositiveValues { get => _clampNonPositiveValues; set { _clampNonPositiveValues = value; OnPropertyChanged(); } }
        private bool _clampNonPositiveValues = true;

        [Category("计算结果"), DisplayName("最小值")]
        [Description("非正 XYZ 值的替换值，默认 0.0001，必须大于或等于 0。")]
        [PropertyVisibility(nameof(ClampNonPositiveValues), true)]
        public double MinimumValue
        {
            get => _minimumValue;
            set
            {
                if (!double.IsFinite(value) || value < 0) return;
                _minimumValue = value;
                OnPropertyChanged();
            }
        }
        private double _minimumValue = 0.0001;

        internal Func<double, double> CreateValueNormalizer()
        {
            bool enabled = ClampNonPositiveValues;
            double minimum = MinimumValue;
            return value => enabled && !(value > 0) ? minimum : value;
        }
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

            listViewSide.ItemsSource = PoiResultCIExyuvDatas;
            ConfigureResultView();
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
              
            listViewSide.ItemsSource = poiResultCIEYDatas;
            ConfigureResultView();
            ButtonShowOnCieDiagram.IsEnabled = false;
            ButtonShowOnCieDiagram.ToolTip = "仅包含 xy 色坐标的结果可显示到 CIE 色度图";
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = CVCIEShowConfig.Instance;
            PrecisionCombo.ItemsSource = Enumerable.Range(0, 10);

            listViewSide.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listViewSide.SelectAll(), (s, e) => e.CanExecute = true));
            listViewSide.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, _) => ColorVision.Common.Clipboard.SetText(CreateClipboardText()), (_, e) => e.CanExecute = listViewSide.SelectedItems.Count > 0));

            // Populate statistics panel
            PopulateStatisticsPanel();
        }

        private void ConfigureResultView()
        {
            RebuildColumns();
            PopulateStatisticsPanel();
            CVCIEShowConfig config = (CVCIEShowConfig)DataContext;
            config.PropertyChanged += DisplayConfigChanged;
            Closed += (_, _) => config.PropertyChanged -= DisplayConfigChanged;
            ResultCount.Text = $"{listViewSide.Items.Count} 个区域";
        }

        private void DisplayConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CVCIEShowConfig.DecimalPlaces)) return;
            RebuildColumns();
            PopulateStatisticsPanel();
        }

        private readonly Dictionary<GridViewColumn, Func<object, string>> clipboardColumns = new();

        internal string CreateClipboardText()
        {
            if (listViewSide.View is not GridView grid) return "";
            var columns = grid.Columns.Where(c => c.Width != 0).ToArray();
            var selected = listViewSide.SelectedItems.Cast<object>().ToHashSet();
            var lines = new List<string> { string.Join("\t", columns.Select(c => ClipboardCell(c.Header.ToString() ?? ""))) };
            foreach (object row in listViewSide.Items)
                if (selected.Contains(row)) lines.Add(string.Join("\t", columns.Select(c => ClipboardCell(clipboardColumns[c](row)))));
            return string.Join(Environment.NewLine, lines);
        }

        private static string ClipboardCell(string value) => value.IndexOfAny(['\t', '\r', '\n', '"']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

        private void RebuildColumns()
        {
            if (listViewSide.View is not GridView grid) return;
            var visibility = grid.Columns.ToDictionary(c => c.Header.ToString() ?? "", c => c.Width == 0);
            grid.Columns.Clear();
            clipboardColumns.Clear();
            LeftGridViewColumnVisibilitys.Clear();
            string format = "F" + ((CVCIEShowConfig)DataContext).DecimalPlaces;
            (string Header, string Path, int Width, string Hint)[] columns =
            {
                ("名称", "Name", 90, "区域名称"), ("中心坐标 (px)", "Point", 130, "图形中心；多边形为外接框中心"),
                ("尺寸 (px)", "Point", 125, "旋转前宽度、高度"), ("形状", "Shapes", 65, "测量区域形状"),
                ("CIE X", "X", 90, "CIE X 三刺激值"), ("CIE Y", "Y", 90, "亮度（cd/m²）"), ("CIE Z", "Z", 90, "CIE Z 三刺激值"),
                ("CIE x", "x", 70, "CIE 1931 x 色度坐标"), ("CIE y", "y", 70, "CIE 1931 y 色度坐标"),
                ("CIE u′", "u", 70, "CIE 1976 u′ 色度坐标"), ("CIE v′", "v", 70, "CIE 1976 v′ 色度坐标"),
                ("CCT (K)", "CCT", 90, "相关色温"), ("λd (nm)", "Wave", 90, "主波长")
            };
            foreach (var column in columns)
            {
                if (!IsPoiResultCIExyuvDatas && column.Path is not ("Name" or "Point" or "Shapes" or "Y")) continue;
                var binding = new Binding(column.Path);
                if (column.Path == "Point") binding.Converter = new CvciePointDisplayConverter(column.Header.StartsWith("尺寸"), format);
                else if (column.Path is not ("Name" or "Shapes")) binding.StringFormat = format;
                var text = new FrameworkElementFactory(typeof(TextBlock));
                text.SetBinding(TextBlock.TextProperty, binding);
                text.SetBinding(ToolTipProperty, new Binding(column.Path == "Point" ? (column.Header.StartsWith("尺寸") ? "PixelSize" : "PixelPos") : column.Path));
                text.SetValue(TextBlock.TextAlignmentProperty, column.Path is "Name" or "Shapes" ? TextAlignment.Left : TextAlignment.Right);
                text.SetValue(MarginProperty, new Thickness(4, 3, 6, 3));
                text.SetValue(MinWidthProperty, (double)column.Width - 14);
                var viewColumn = new GridViewColumn
                {
                    Header = column.Header,
                    Width = visibility.TryGetValue(column.Header, out bool hidden) && hidden ? 0 : double.NaN,
                    CellTemplate = new DataTemplate { VisualTree = text },
                    HeaderContainerStyle = new Style(typeof(GridViewColumnHeader), grid.ColumnHeaderContainerStyle)
                    { Setters = { new Setter(ToolTipProperty, column.Hint) } }
                };
                grid.Columns.Add(viewColumn);
                clipboardColumns[viewColumn] = row =>
                {
                    object? value = row.GetType().GetProperty(column.Path)?.GetValue(row);
                    if (binding.Converter != null) return binding.Converter.Convert(value, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture)?.ToString() ?? "";
                    return value is IFormattable number ? number.ToString(format, System.Globalization.CultureInfo.CurrentCulture) : value?.ToString() ?? "";
                };
            }
        }

        private string Number(double value) => value.ToString("F" + ((CVCIEShowConfig)DataContext).DecimalPlaces, System.Globalization.CultureInfo.CurrentCulture);

        private StackPanel statisticsDetails = new();
        private Expander? statisticsExpander;
        private readonly List<string> statisticsClipboard = new();

        private void PopulateStatisticsPanel()
        {
            bool expanded = statisticsExpander?.IsExpanded == true;
            StatisticsPanel.Children.Clear();
            statisticsDetails = new StackPanel();
            statisticsClipboard.Clear();
            statisticsClipboard.Add("统计项\t数值\t单位");
            if (IsPoiResultCIExyuvDatas && CIExyuvStats != null)
            {
                var s = CIExyuvStats;
                AddStatisticsGroup("亮度", false, ("中心", s.CenterLuminance, "cd/m²"), ("平均", s.AverageLuminance, "cd/m²"), ("最大", s.MaxLuminance, "cd/m²"), ("最小", s.MinLuminance, "cd/m²"));
                AddStatisticsGroup("均匀性", true, ("Min/Max", s.UniformityMinDivMax, "%"), ("(Max−Min)/Avg", s.UniformityDiffDivAvg, "%"), ("(Max−Min)/Max", s.UniformityDiffDivMax, "%"), ("1−(Max−Avg)/Avg", s.Uniformity, "%"), ("标准差", s.StandardDeviation, ""), ("标准差/平均", s.StandardDeviationPercent, "%"), ("色度差 Δu′v′", s.ColorUniformityDeltaUv, ""), ("色度差 Δx", s.ColorUniformityDeltaX, ""), ("色度差 Δy", s.ColorUniformityDeltaY, ""));
                AddStatisticsGroup("中心色度", true, ("CIE 1931 x", s.CenterX, ""), ("CIE 1931 y", s.CenterY, ""), ("CIE 1976 u′", s.CenterU, ""), ("CIE 1976 v′", s.CenterV, ""), ("相关色温 CCT", s.CenterCCT, "K"), ("主波长 λd", s.CenterWave, "nm"), ("波长差 Δλd", s.DeltaWave, "nm"));
            }
            else if (CIEYStats != null)
            {
                var s = CIEYStats;
                AddStatisticsGroup("亮度", false, ("中心", s.CenterLuminance, "cd/m²"), ("平均", s.AverageLuminance, "cd/m²"), ("最大", s.MaxLuminance, "cd/m²"), ("最小", s.MinLuminance, "cd/m²"));
                AddStatisticsGroup("均匀性", true, ("Min/Max", s.UniformityMinDivMax, "%"), ("(Max−Min)/Avg", s.UniformityDiffDivAvg, "%"), ("(Max−Min)/Max", s.UniformityDiffDivMax, "%"), ("标准差", s.StandardDeviation, ""), ("标准差/平均", s.StandardDeviationPercent, "%"));
            }
            statisticsExpander = new Expander
            {
                Header = "统计详情 · 均匀性 / 色度", IsExpanded = expanded,
                Content = new ScrollViewer { Content = statisticsDetails, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                Margin = new Thickness(8, 0, 8, 2)
            };
            StatisticsPanel.Children.Add(statisticsExpander);
        }

        private void AddStatisticsGroup(string title, bool collapsible, params (string Label, double Value, string Unit)[] values)
        {
            Panel panel = collapsible ? new WrapPanel() : new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };
            panel.Margin = new Thickness(0, 0, 0, 2);
            foreach (var item in values)
            {
                var content = new StackPanel { Margin = new Thickness(8, 3, 12, 3), MinWidth = collapsible ? 130 : 0 };
                content.Children.Add(new TextBlock { Text = item.Label + (collapsible ? "" : "亮度"), Opacity = 0.65, FontSize = 12 });
                var value = new TextBlock { FontSize = collapsible ? 13 : 17, FontWeight = FontWeights.SemiBold, ToolTip = "右键复制数值或全部统计" };
                statisticsClipboard.Add(title + " · " + item.Label + "\t" + Number(item.Value) + "\t" + item.Unit);
                var menu = new ContextMenu();
                var copyValue = new MenuItem { Header = "复制数值" };
                copyValue.Click += (_, _) => ColorVision.Common.Clipboard.SetText(Number(item.Value));
                var copyStats = new MenuItem { Header = "复制全部统计" };
                copyStats.Click += (_, _) => ColorVision.Common.Clipboard.SetText(string.Join(Environment.NewLine, statisticsClipboard));
                menu.Items.Add(copyValue);
                menu.Items.Add(copyStats);
                value.ContextMenu = menu;
                value.Inlines.Add(new Run(Number(item.Value)));
                if (item.Unit.Length > 0) value.Inlines.Add(new Run(" " + item.Unit) { FontSize = 12, FontWeight = FontWeights.Normal });
                content.Children.Add(value);
                panel.Children.Add(content);
            }
            if (collapsible)
            {
                statisticsDetails.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 5, 0, 0) });
                statisticsDetails.Children.Add(panel);
            }
            else StatisticsPanel.Children.Add(panel);
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
