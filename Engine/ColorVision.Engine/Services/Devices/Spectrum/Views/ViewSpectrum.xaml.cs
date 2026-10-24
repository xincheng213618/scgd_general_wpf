using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using cvColorVision;
#pragma warning disable CA1805,CA1859,CS8604
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.UI.Sorts;
using ScottPlot;
using ScottPlot.Plottables;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{
    public class BoolToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = (bool)value;
            // If parameter is "Inverse", flip the logic
            if (parameter != null && parameter.ToString() == "Inverse")
            {
                isVisible = !isVisible;
            }

            return isVisible ? double.NaN : 0.0; // double.NaN is equivalent to "Auto"
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSpectrum : UserControl, IDisposable
    {

        public ObservableCollection<ViewResultSpectrum> ViewResults { get; set; } = new ObservableCollection<ViewResultSpectrum>();
        public static ViewSpectrumConfig Config => ViewSpectrumConfig.Instance;

        public DisplaySpectrumConfig DisplayConfig => Device.DisplayConfig;
        public DeviceSpectrum Device { get; set; }
        private bool _initializationStarted;
        private bool _isInitialized;

        public ViewSpectrum(DeviceSpectrum device) : this(device, false) { }

        internal ViewSpectrum(DeviceSpectrum device, bool deferInitialization)
        {
            Device = device;
            Loaded += View_Loaded;
            IsVisibleChanged += View_IsVisibleChanged;
            if (!deferInitialization)
                EnsureInitialized();
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsVisible) EnsureInitialized();
        }

        private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsLoaded && IsVisible) EnsureInitialized();
        }

        internal void EnsureInitialized()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(EnsureInitialized);
                return;
            }
            if (_initializationStarted) return;
            _initializationStarted = true;
            Loaded -= View_Loaded;
            IsVisibleChanged -= View_IsVisibleChanged;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Establish the view context before bindings can inherit the dock host's configuration.
            DataContext = this;
            InitializeComponent();
            UserControl_Initialized(this, EventArgs.Empty);
            log4net.LogManager.GetLogger(typeof(ViewSpectrum)).Info($"Device view initialized. View={nameof(ViewSpectrum)}, Duration={stopwatch.ElapsedMilliseconds}ms.");
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;
            TextBox TextBox1 = new() { Width = 10, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };

            listView1.ItemsSource = ViewResults;

            string title = ColorVision.Engine.Properties.Resources.RelativeSpectrumCurve;
            wpfplot1.Plot.XLabel(ColorVision.Engine.Properties.Resources.WavelengthNm);
            wpfplot1.Plot.YLabel(ColorVision.Engine.Properties.Resources.RelativeSpectrum);
            wpfplot1.Plot.Axes.Title.Label.Text = title;
            wpfplot1.Plot.Axes.Title.Label.FontName = Fonts.Detect(title);
            wpfplot1.Plot.Axes.Title.Label.Text = title;
            wpfplot1.Plot.Axes.Left.Label.FontName = Fonts.Detect(title);
            wpfplot1.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(title);

            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(-0.05, 1);
            wpfplot1.Plot.Axes.Bottom.Min = 380;
            wpfplot1.Plot.Axes.Bottom.Max = 780;
            wpfplot1.Plot.Axes.Left.Min = -0.05;
            wpfplot1.Plot.Axes.Left.Max = 1;

            string titleAbsolute = ColorVision.Engine.Properties.Resources.AbsoluteSpectrumCurve;
            wpfplot2.Plot.XLabel(ColorVision.Engine.Properties.Resources.WavelengthNm);
            wpfplot2.Plot.YLabel(ColorVision.Engine.Properties.Resources.AbsoluteSpectrum + "W/nm");
            wpfplot2.Plot.Axes.Title.Label.Text = titleAbsolute;
            wpfplot2.Plot.Axes.Title.Label.FontName = Fonts.Detect(titleAbsolute);
            wpfplot2.Plot.Axes.Left.Label.FontName = Fonts.Detect(titleAbsolute);
            wpfplot2.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(titleAbsolute);

            wpfplot2.Plot.Axes.SetLimitsX(380, 780);
            wpfplot2.Plot.Axes.Bottom.Min = 380;
            wpfplot2.Plot.Axes.Bottom.Max = 780;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                ViewSpectrumConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                ViewSpectrumConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, Button_Click,(s, e) => e.CanExecute = true));

            DisplayConfig_IsIsLuminousFluxModeChanged();
            DisplayConfig.IsIsLuminousFluxModeChanged += LuminousFluxModeChanged;
            PropertyChangedEventManager.AddHandler(Config, SpectrumConfig_PropertyChanged, nameof(ViewSpectrumConfig.IsShowSpectrumFill));
            ThemeManager.Current.CurrentUIThemeChanged += ApplySpectrumTheme;
            ApplySpectrumTheme(ThemeManager.Current.CurrentUITheme);
            InitializeSpectrumAnalysis();
        }

        private void LuminousFluxModeChanged(object? sender, bool value) => DisplayConfig_IsIsLuminousFluxModeChanged();

        public void Dispose()
        {
            Loaded -= View_Loaded;
            IsVisibleChanged -= View_IsVisibleChanged;
            if (_isInitialized)
            {
                DisplayConfig.IsIsLuminousFluxModeChanged -= LuminousFluxModeChanged;
                PropertyChangedEventManager.RemoveHandler(Config, SpectrumConfig_PropertyChanged, nameof(ViewSpectrumConfig.IsShowSpectrumFill));
                ThemeManager.Current.CurrentUIThemeChanged -= ApplySpectrumTheme;
            }
            relativeCursor?.Dispose();
            absoluteCursor?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void DisplayConfig_IsIsLuminousFluxModeChanged()
        {
            bool isLuminousFluxMode = DisplayConfig.IsLuminousFluxMode;

            wpfplot2.Plot.YLabel(ColorVision.Engine.Properties.Resources.AbsoluteSpectrum + (isLuminousFluxMode ? "W/nm" : "w/sr·m^2·nm"));

            SetColumnVisibility(new[] { EqeColumn, LuminousFluxColumn, RadiantFluxColumn, LuminousEfficacyColumn, VoltageColumn, CurrentColumn }, isLuminousFluxMode);
            SetColumnVisibility(new[] { LvColumn }, !isLuminousFluxMode);

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AdjustGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);

            }

        }

        private void SetColumnVisibility(IEnumerable<GridViewColumn> columns, bool isVisible)
        {
            HashSet<GridViewColumn> targetColumns = new(columns);

            foreach (var item in GridViewColumnVisibilitys)
            {
                if (targetColumns.Contains(item.GridViewColumn))
                {
                    item.IsVisible = isVisible;
                }
            }
        }

        private void Delete()
        {
            var selected = listView1.SelectedItems.Cast<ViewResultSpectrum>().ToArray();
            foreach (var result in selected) ViewResults.Remove(result);
            if (ViewResults.Count == 0) Clear();
            else
            {
                if (listView1.SelectedIndex < 0) listView1.SelectedIndex = 0;
                ReDrawPlot();
            }
        }


        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();



        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu || listView1.View is not GridView gridView) return;
            if (contextMenu.Items.Count == 0)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
            foreach (var item in contextMenu.Items.OfType<MenuItem>())
            {
                if (BindingOperations.GetBinding(item, MenuItem.IsCheckedProperty)?.Source is GridViewColumnVisibility column
                    && (column.GridViewColumn == VoltageColumn || column.GridViewColumn == CurrentColumn))
                    item.Visibility = DisplayConfig.IsLuminousFluxMode ? Visibility.Visible : Visibility.Collapsed;
            }
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var selectedResults = GetSpectrumExportResults(sender);
            if (selectedResults.Length == 0)
            {
                MessageBox.Show("SelectDataFirst");
                return;
            }


            bool fluxMode = selectedResults.Any(result => result.AcquisitionFluxMode ?? DisplayConfig.IsLuminousFluxMode);
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = (fluxMode ? "EQE" : "SpectrometerExport") + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            if (fluxMode)
                SpectrumCsvExportHelper.ExportLuminousFluxMode(dialog.FileName, selectedResults);
            else
                SpectrumCsvExportHelper.ExportLuminanceMode(dialog.FileName, selectedResults);
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
            }
            else { relativeCursor?.Clear(); absoluteCursor?.Clear(); }
        }

        bool MulComparison;

        Scatter? LastMulSelectComparsion;

        private Color spectrumCurveColor = Color.FromHex("#303030");

        private void ApplySpectrumTheme(Theme theme)
        {
            foreach (var control in new[] { wpfplot1, wpfplot2 })
            {
                spectrumCurveColor = SpectrumPlotStyle.ApplyTheme(control.Plot, theme == Theme.Dark);
                if (!MulComparison)
                    foreach (var result in ViewResults)
                    {
                        if (result.ScatterPlot != null) result.ScatterPlot.Color = spectrumCurveColor;
                        if (result.AbsoluteScatterPlot != null) result.AbsoluteScatterPlot.Color = spectrumCurveColor;
                    }
                control.Refresh();
            }
        }

        private void DrawPlot()
        {
            if (listView1.SelectedItem is not ViewResultSpectrum result) return;
            if (IsShowingAbsoluteSpectrum) { DrawAbsolutePlot(); return; }
            DrawSpectrum(wpfplot1, result, result.ScatterPlot, false);
        }

        private void SpectrumConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SpectrumPlotStyle.ClearFill(wpfplot1.Plot);
                SpectrumPlotStyle.ClearFill(wpfplot2.Plot);
                DrawPlot();
                wpfplot1.Refresh();
                wpfplot2.Refresh();
            });
        }

        private void DrawAbsolutePlot()
        {
            if (listView1.SelectedItem is not ViewResultSpectrum result) return;
            DrawSpectrum(wpfplot2, result, result.AbsoluteScatterPlot, true);
        }

        private void DrawSpectrum(ScottPlot.WPF.WpfPlot control, ViewResultSpectrum result, Scatter curve, bool absolute)
        {
            if (curve == null) return;
            UpdateSpectrumCursors(result, absolute);
            control.Plot.Axes.SetLimitsX(result.fSpect1, result.fSpect2);
            if (!absolute) control.Plot.Axes.SetLimitsY(-0.05, 1.05);
            SpectrumPlotStyle.ClearFill(control.Plot);
            if (MulComparison)
            {
                if (LastMulSelectComparsion != null)
                {
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    LastMulSelectComparsion.LineWidth = 1;
                }
                curve.Color = Colors.Red;
                curve.LineWidth = 2;
            }
            else
            {
                control.Plot.Remove(LastMulSelectComparsion);
                control.Plot.Remove(curve);
                if (Config.IsShowSpectrumFill)
                    SpectrumPlotStyle.AddFill(control.Plot, curve, wavelength =>
                    {
                        var rgb = SpectrumChromaticity.DisplayRgb(wavelength);
                        return new Color(rgb.red, rgb.green, rgb.blue);
                    });
                curve.Color = spectrumCurveColor;
                curve.LineWidth = 1.2f;
            }
            curve.MarkerSize = 0;
            if (!control.Plot.PlottableList.Contains(curve)) control.Plot.PlottableList.Add(curve);
            LastMulSelectComparsion = curve;
            if (absolute) control.Plot.Axes.AutoScaleY();
            control.Refresh();
        }

        private bool IsShowingAbsoluteSpectrum { get; set; } = false;

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            MulComparison = !MulComparison;
            if (listView1.SelectedIndex <= -1)
            {
                if (listView1.Items.Count == 0)
                    return;
                listView1.SelectedIndex = 0;
            }
            ReDrawPlot();
        }

        private void ToggleSpectrumType_Click(object sender, RoutedEventArgs e)
        {
            IsShowingAbsoluteSpectrum = !IsShowingAbsoluteSpectrum;
            
            if (IsShowingAbsoluteSpectrum)
            {
                wpfplot1.Visibility = Visibility.Collapsed;
                wpfplot2.Visibility = Visibility.Visible;
                SpectrumTypeText.Text = ColorVision.Engine.Properties.Resources.Absolute;
            }
            else
            {
                wpfplot1.Visibility = Visibility.Visible;
                wpfplot2.Visibility = Visibility.Collapsed;
                SpectrumTypeText.Text = ColorVision.Engine.Properties.Resources.Relative;
            }
            
            ReDrawPlot();
        }


        private void ReDrawPlot()
        {
            if (listView1.SelectedIndex < 0) return;

            if (IsShowingAbsoluteSpectrum)
            {
                wpfplot2.Plot.Clear();
                LastMulSelectComparsion = null;
                if (MulComparison)
                {
                    listView1.SelectedIndex = listView1.Items.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                    for (int i = 0; i < ViewResults.Count; i++)
                    {
                        if (i == listView1.SelectedIndex)
                            continue;
                        var plot = ViewResults[i].AbsoluteScatterPlot;
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;

                        wpfplot2.Plot.PlottableList.Add(plot);
                    }
                }
            }
            else
            {
                wpfplot1.Plot.Clear();

                LastMulSelectComparsion = null;
                if (MulComparison)
                {
                    listView1.SelectedIndex = listView1.Items.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                    for (int i = 0; i < ViewResults.Count; i++)
                    {
                        if (i == listView1.SelectedIndex)
                            continue;
                        var plot = ViewResults[i].ScatterPlot;
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;

                        wpfplot1.Plot.PlottableList.Add(plot);
                    }
                }
            }
            DrawPlot();
        }

        private void Button2_Click(object sender, RoutedEventArgs e) => Clear();

        private void ToolBar1_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ToolBar toolBar)
            {
                if (toolBar.Template.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid)
                    overflowGrid.Visibility = Visibility.Collapsed;
                if (toolBar.Template.FindName("MainPanelBorder", toolBar) is FrameworkElement mainPanelBorder)
                    mainPanelBorder.Margin = new Thickness(0);
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex >= 0)
            {
                Delete();
                e.Handled = true;
            }
        }

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!Config.IsShowListView) return;
            Config.Height = Math.Max(50, ListRow2.ActualHeight - 32);
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }



        public void Clear()
        {
            ViewResults.Clear();
            LastMulSelectComparsion = null;
            if (!_isInitialized) return;
            relativeCursor?.Clear();
            absoluteCursor?.Clear();
            wpfplot1.Plot.Clear();
            wpfplot2.Plot.Clear();
            wpfplot1.Refresh();
            wpfplot2.Refresh();
        }

        public void AddViewResultSpectrum(ViewResultSpectrum viewResultSpectrum)
        {
            EnsureInitialized();
            if (Config.InsertAtBeginning)
                ViewResults.Insert(0, viewResultSpectrum);
            else
                ViewResults.Add(viewResultSpectrum);
            listView1.SelectedItem = viewResultSpectrum;
            listView1.ScrollIntoView(viewResultSpectrum);
        }

        internal void ReplaceHistoryResults(IEnumerable<ViewResultSpectrum> results)
        {
            var snapshot = results.ToArray();
            ViewResults.Clear();
            foreach (var result in snapshot) ViewResults.Add(result);
            if (ViewResults.Count > 0) listView1.SelectedIndex = 0;
        }
        private void Inquire_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                var query = DB.Queryable<SpectumResultEntity>().Where(x => x.DataType == DisplayConfig.IsLuminousFluxMode);
                query = query.OrderBy(x => x.Id, Config.OrderByType);
                var dbList = Config.Count > 0 ? query.Take(Config.Count).ToList() : query.ToList();
                ReplaceHistoryResults(dbList.Select(item => new ViewResultSpectrum(item)));
            }
            catch (Exception ex)
            {
                log4net.LogManager.GetLogger(typeof(ViewSpectrum)).Error("Spectrum history query failed; existing results retained.", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), EngineLocalization.Get("光谱查询失败，当前列表已保留。详情请查看日志。"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            ObservableCollection<ViewResultSpectrum> queryResults = new();
            GenericQuery<SpectumResultEntity, ViewResultSpectrum> genericQuery = new GenericQuery<SpectumResultEntity, ViewResultSpectrum>(DB, queryResults,
                t =>
                {
                    ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(t);

                    return viewResultSpectrum;
                }
                );
            genericQuery.QueryCompleted += (s, e) =>
            {
                ReplaceHistoryResults(queryResults);
            };
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
            DB.Dispose();

        }


        private void ContextMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            e.Handled = ViewResults.SortByGridViewColumn<ViewResultImage>(sender, GridViewColumnVisibilitys, Properties.Resources.ResourceManager);
        }
    }

    public static class SpectrumCsvExportHelper
    {
        private const int StartWavelength = 380;
        private const int EndWavelength = 780;

        public static void ExportLuminanceMode(string filePath, IEnumerable<ViewResultSpectrum> results)
        {
            List<string> headers = new()
            {
                "No",
                "Lot",
                "IP",
                "Luminace（Lv）(cd/m²)",
                "Blue Light Intensity",
                "Cx",
                "Cy",
                "u'",
                "v'",
                "Correlated Color Temperature(CCT)（K）",
                "DW（λd）（nm）",
                "Color Purity(%)",
                "Peak Wavelength(λp)(nm)",
                "Color Rendering (Ra)",
                "FWHM",
                "Excitation Purity",
                "Dominant Wavelength Color"
            };

            AddRenderingHeaders(headers, includeRa: false);
            AddSpectrumHeaders(headers);

            StringBuilder csvBuilder = new();
            AppendCsvLine(csvBuilder, headers);

            foreach (ViewResultSpectrum result in results)
            {
                List<string> row = new()
                {
                    result.Id.ToString(CultureInfo.InvariantCulture),
                    result.BatchID?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.IP ?? string.Empty,
                    result.Lv ?? string.Empty,
                    result.Blue ?? string.Empty,
                    result.fx.ToString(CultureInfo.InvariantCulture),
                    result.fy.ToString(CultureInfo.InvariantCulture),
                    result.fu.ToString(CultureInfo.InvariantCulture),
                    result.fv.ToString(CultureInfo.InvariantCulture),
                    result.Chromaticity.ColorTemperature?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.Chromaticity.DominantWavelength?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.PurityPercent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.fLp.ToString(CultureInfo.InvariantCulture),
                    result.ColorRendering.Ra?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.fHW.ToString(CultureInfo.InvariantCulture),
                    result.Chromaticity.ExcitationPurity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.DominantWavelengthHex ?? string.Empty,
                };

                AddRenderingValues(row, result, includeRa: false);
                AddSpectrumValues(row, result.SpectralDatas);
                AppendCsvLine(csvBuilder, row);
            }

            File.WriteAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
        }

        public static void ExportLuminousFluxMode(string filePath, IEnumerable<ViewResultSpectrum> results)
        {
            List<string> headers = new()
            {
                "No",
                "Lot",
                "IP",
                "EQE(%)",
                "LuminousFlux(lm)",
                "RadiantFlux(W)",
                "LuminousEfficacy(lm/W)",
                "Cx",
                "Cy",
                "Correlated Color Temperature(CCT)（K）",
                "Peak Wavelength(λp)(nm)",
                "Excitation Purity",
                "Dominant Wavelength Color",
                "Voltgage(V) (V)",
                "Current(I) (mA)"
            };

            AddRenderingHeaders(headers, includeRa: true);
            AddSpectrumHeaders(headers);

            StringBuilder csvBuilder = new();
            AppendCsvLine(csvBuilder, headers);

            foreach (ViewResultSpectrum result in results)
            {
                List<string> row = new()
                {
                    result.Id.ToString(CultureInfo.InvariantCulture),
                    result.BatchID?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.IP ?? string.Empty,
                    result.EqePercent.ToString(CultureInfo.InvariantCulture),
                    result.LuminousFlux?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.RadiantFlux?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.LuminousEfficacy?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.fx.ToString(CultureInfo.InvariantCulture),
                    result.fy.ToString(CultureInfo.InvariantCulture),
                    result.Chromaticity.ColorTemperature?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.fLp.ToString(CultureInfo.InvariantCulture),
                    result.Chromaticity.ExcitationPurity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.DominantWavelengthHex ?? string.Empty,
                    result.AcquisitionFluxMode == false ? string.Empty : result.V?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    result.AcquisitionFluxMode == false ? string.Empty : result.I?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                };

                AddRenderingValues(row, result, includeRa: true);
                AddSpectrumValues(row, result.SpectralDatas);
                AppendCsvLine(csvBuilder, row);
            }

            File.WriteAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
        }

        private static void AddRenderingHeaders(List<string> headers, bool includeRa)
        {
            if (includeRa) headers.Add("Color Rendering (Ra)");
            headers.Add("Mean Wavelength(nm)");
            headers.AddRange(Enumerable.Range(1, 15).Select(index => $"R{index}"));
            headers.Add("Color Rendering Status");
            headers.AddRange(new[] { "Duv", "Complementary Wavelength(nm)", "Wavelength Status", "Temperature Status", "Purity Reference White" });
            if (includeRa) headers.AddRange(new[] { "DW(Ld)(nm)", "Color Purity(%)" });
        }

        private static void AddRenderingValues(List<string> row, ViewResultSpectrum result, bool includeRa)
        {
            if (includeRa) row.Add(result.ColorRendering.Ra?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            row.Add(result.ColorRendering.MeanWavelength?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            row.AddRange(result.RenderingIndices.Select(value => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            row.Add(result.ColorRendering.Status.ToString());
            row.Add(result.Chromaticity.Duv?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            row.Add(result.Chromaticity.ComplementaryWavelength?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            row.Add(result.Chromaticity.WavelengthStatus.ToString());
            row.Add(result.Chromaticity.TemperatureStatus.ToString());
            row.Add("D65 (0.3127; 0.3290)");
            if (includeRa)
            {
                row.Add(result.Chromaticity.DominantWavelength?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                row.Add(result.PurityPercent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        private static void AddSpectrumHeaders(List<string> headers)
        {
            for (int wl = StartWavelength; wl <= EndWavelength; wl++)
            {
                headers.Add(wl.ToString(CultureInfo.InvariantCulture));
            }

            for (int wl = StartWavelength; wl <= EndWavelength; wl++)
            {
                headers.Add("sp" + wl);
            }
        }

        private static void AddSpectrumValues(List<string> row, IList<SpectralData> spectralDatas)
        {
            int spectralCount = spectralDatas?.Count ?? 0;
            IList<SpectralData> safeSpectralDatas = spectralDatas ?? new List<SpectralData>();

            for (int wl = StartWavelength; wl <= EndWavelength; wl++)
            {
                int index = wl - StartWavelength;
                row.Add(index < spectralCount ? safeSpectralDatas[index].AbsoluteSpectrum.ToString(CultureInfo.InvariantCulture) : string.Empty);
            }

            for (int wl = StartWavelength; wl <= EndWavelength; wl++)
            {
                int index = wl - StartWavelength;
                row.Add(index < spectralCount ? safeSpectralDatas[index].RelativeSpectrum.ToString(CultureInfo.InvariantCulture) : string.Empty);
            }
        }

        private static void AppendCsvLine(StringBuilder csvBuilder, List<string> values)
        {
            csvBuilder.AppendLine(string.Join(",", values.Select(value => value.IndexOfAny(['"', ',', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value)));
        }
    }
}
