#pragma warning disable CS8604
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
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
using System.Reflection;
using System.Text;
using System.Threading;
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
    public partial class ViewSpectrum : UserControl
    {

        public ObservableCollection<ViewResultSpectrum> ViewResults { get; set; } = new ObservableCollection<ViewResultSpectrum>();
        public static ViewSpectrumConfig Config => ViewSpectrumConfig.Instance;

        public DisplaySpectrumConfig DisplayConfig => Device.DisplayConfig;
        public DeviceSpectrum Device { get; set; }
        public ViewSpectrum(DeviceSpectrum device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;

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
            wpfplot1.Plot.Axes.Bottom.Min = 370;
            wpfplot1.Plot.Axes.Bottom.Max = 1000;
            wpfplot1.Plot.Axes.Left.Min = -0.05;
            wpfplot1.Plot.Axes.Left.Max = 1;
            AddSpectrumColorBar(wpfplot1);
            string titleAbsolute = ColorVision.Engine.Properties.Resources.AbsoluteSpectrumCurve;
            wpfplot2.Plot.XLabel(ColorVision.Engine.Properties.Resources.WavelengthNm);
            wpfplot2.Plot.YLabel(ColorVision.Engine.Properties.Resources.AbsoluteSpectrum + "W/nm");
            wpfplot2.Plot.Axes.Title.Label.Text = titleAbsolute;
            wpfplot2.Plot.Axes.Title.Label.FontName = Fonts.Detect(titleAbsolute);
            wpfplot2.Plot.Axes.Left.Label.FontName = Fonts.Detect(titleAbsolute);
            wpfplot2.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(titleAbsolute);

            wpfplot2.Plot.Axes.SetLimitsX(380, 780);
            wpfplot2.Plot.Axes.Bottom.Min = 370;
            wpfplot2.Plot.Axes.Bottom.Max = 1000;

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                ViewSpectrumConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                ViewSpectrumConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            if (listView2.View is GridView gridView1)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView1.Columns, LeftGridViewColumnVisibilitys);

                ViewSpectrumConfig.Instance.LeftGridViewColumnVisibilitys.CopyToGridView(LeftGridViewColumnVisibilitys);
                ViewSpectrumConfig.Instance.LeftGridViewColumnVisibilitys = LeftGridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView1.Columns, LeftGridViewColumnVisibilitys);
            }

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, Button_Click,(s, e) => e.CanExecute = true));

            DisplayConfig_IsIsLuminousFluxModeChanged();
            DisplayConfig.IsIsLuminousFluxModeChanged +=(s,e) => DisplayConfig_IsIsLuminousFluxModeChanged();
        }

        private void DisplayConfig_IsIsLuminousFluxModeChanged()
        {
            List<string> EQE = new List<string>();
            EQE.Add("EQE");
            EQE.Add("光通量(lm)");
            EQE.Add("辐射通量(W)");
            EQE.Add("光效(lm/W)");
            List<string> Nomarl = new List<string>();
            Nomarl.Add(Properties.Resources.Lv);

            if (DisplayConfig.IsLuminousFluxMode)
            {
                wpfplot2.Plot.YLabel(ColorVision.Engine.Properties.Resources.AbsoluteSpectrum + "W/nm");
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (EQE.Contains(item.ColumnName))
                    {
                        item.IsVisible = true;
                    }
                    if (Nomarl.Contains(item.ColumnName))
                    {
                        item.IsVisible = false;
                    }
                }

            }
            else
            {
                wpfplot2.Plot.YLabel(ColorVision.Engine.Properties.Resources.AbsoluteSpectrum + "w/sr·m^2·nm");

                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (EQE.Contains(item.ColumnName))
                    {
                        item.IsVisible = false;
                    }
                    if (Nomarl.Contains(item.ColumnName))
                    {
                        item.IsVisible = true;
                    }
                }

            }
            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AdjustGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);

            }

        }

        private void Delete()
        {
            if (listView1.SelectedItems.Count == listView1.Items.Count)
                ViewResults.Clear();
            else
            {
                listView1.SelectedIndex = -1;
                foreach (var item in listView1.SelectedItems.Cast<ViewResultSpectrum>().ToList())
                    ViewResults.Remove(item);
            }
        }


        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();


        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }

        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView2.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, LeftGridViewColumnVisibilitys);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show("SelectDataFirst");
                return;
            }

            List<ViewResultSpectrum> selectedResults = listView1.SelectedItems.Cast<ViewResultSpectrum>().ToList();

            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = (DisplayConfig.IsLuminousFluxMode ? "EQE" : "SpectrometerExport") + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            if (DisplayConfig.IsLuminousFluxMode)
                SpectrumCsvExportHelper.ExportLuminousFluxMode(dialog.FileName, selectedResults);
            else
                SpectrumCsvExportHelper.ExportLuminanceMode(dialog.FileName, selectedResults);
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                listView2.ItemsSource = ViewResults[listview.SelectedIndex].SpectralDatas;
            }
        }

        bool MulComparison;

        Scatter? LastMulSelectComparsion;

        private void DrawPlot()
        {
            if (listView1.SelectedIndex < 0) return;
            
            if (IsShowingAbsoluteSpectrum)
            {
                DrawAbsolutePlot();
                return;
            }
            
            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(-0.05, 1);
            wpfplot1.Plot.Axes.Bottom.Min = ViewResults[listView1.SelectedIndex].fSpect1;
            wpfplot1.Plot.Axes.Bottom.Max = ViewResults[listView1.SelectedIndex].fSpect2;
            wpfplot1.Plot.Axes.Left.Min = -0.05;
            wpfplot1.Plot.Axes.Left.Max = 1;

            if (ScatterPlots.Count > 0)
            {
                if (MulComparison)
                {
                    if (LastMulSelectComparsion != null)
                    {
                        LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        LastMulSelectComparsion.LineWidth = 1;
                        LastMulSelectComparsion.MarkerSize = 1;
                    }

                    LastMulSelectComparsion = ScatterPlots[listView1.SelectedIndex];
                    LastMulSelectComparsion.LineWidth = 3;
                    LastMulSelectComparsion.MarkerSize = 3;
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.Red);
                    wpfplot1.Plot.PlottableList.Add(LastMulSelectComparsion);

                }
                else
                {
                    var temp = ScatterPlots[listView1.SelectedIndex];
                    if (temp == null) return;
                    temp.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    temp.LineWidth = 1;
                    temp.MarkerSize = 1;

                    wpfplot1.Plot.PlottableList.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;

                }
            }

            wpfplot1.Refresh();
        }

        private void DrawAbsolutePlot()
        {
            if (listView1.SelectedIndex < 0) return;
            
            wpfplot2.Plot.Axes.SetLimitsX(380, 780);
            wpfplot2.Plot.Axes.Bottom.Min = ViewResults[listView1.SelectedIndex].fSpect1;
            wpfplot2.Plot.Axes.Bottom.Max = ViewResults[listView1.SelectedIndex].fSpect2;

            if (AbsoluteScatterPlots.Count > 0)
            {
                if (MulComparison)
                {
                    if (LastMulSelectComparsion != null)
                    {
                        LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        LastMulSelectComparsion.LineWidth = 1;
                        LastMulSelectComparsion.MarkerSize = 1;
                    }

                    LastMulSelectComparsion = AbsoluteScatterPlots[listView1.SelectedIndex];
                    LastMulSelectComparsion.LineWidth = 3;
                    LastMulSelectComparsion.MarkerSize = 3;
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.Red);
                    wpfplot2.Plot.PlottableList.Add(LastMulSelectComparsion);

                }
                else
                {
                    var temp = AbsoluteScatterPlots[listView1.SelectedIndex];
                    temp.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    temp.LineWidth = 1;
                    temp.MarkerSize = 1;

                    wpfplot2.Plot.PlottableList.Add(temp);
                    wpfplot2.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;

                }
            }

            wpfplot2.Refresh();
        }

        private List<Scatter> ScatterPlots { get; set; } = new List<Scatter>();
        private List<Scatter> AbsoluteScatterPlots { get; set; } = new List<Scatter>();
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
                        var plot = AbsoluteScatterPlots[i];
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
                AddSpectrumColorBar(wpfplot1);
                LastMulSelectComparsion = null;
                if (MulComparison)
                {
                    listView1.SelectedIndex = listView1.Items.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                    for (int i = 0; i < ViewResults.Count; i++)
                    {
                        if (i == listView1.SelectedIndex)
                            continue;
                        var plot = ScatterPlots[i];
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;

                        wpfplot1.Plot.PlottableList.Add(plot);
                    }
                }
            }
            DrawPlot();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();

            wpfplot1.Plot.Clear();
            AddSpectrumColorBar(wpfplot1);
            wpfplot1.Refresh();
            wpfplot2.Plot.Clear();
            wpfplot2.Refresh();

            ReDrawPlot();
        }

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
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResults.RemoveAt(listView1.SelectedIndex);


                if (listView1.Items.Count > 0)
                {
                    listView1.SelectedIndex = temp - 1; ;
                    DrawPlot();
                }
                else
                {
                    wpfplot1.Plot.Clear();
                    AddSpectrumColorBar(wpfplot1);
                    wpfplot1.Refresh();
                    wpfplot2.Plot.Clear();
                    wpfplot2.Refresh();
                }
            }

        }

        Marker markerPlot1;
        Marker markerPlot2;

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            wpfplot1.Plot.Remove(markerPlot1);
            wpfplot2.Plot.Remove(markerPlot2);
            
            if (listView2.SelectedIndex > -1)
            {
                markerPlot1 = new Marker
                {
                    X = listView2.SelectedIndex + 380,
                    Y = ViewResults[listView1.SelectedIndex].fPL[listView2.SelectedIndex * 10],
                    MarkerShape = MarkerShape.FilledCircle,
                    MarkerSize = 10f,
                    Color = Color.FromColor(System.Drawing.Color.Orange),
                };
                wpfplot1.Plot.PlottableList.Add(markerPlot1);
                
                markerPlot2 = new Marker
                {
                    X = listView2.SelectedIndex + 380,
                    Y = ViewResults[listView1.SelectedIndex].fPL[listView2.SelectedIndex * 10] * ViewResults[listView1.SelectedIndex].fPlambda,
                    MarkerShape = MarkerShape.FilledCircle,
                    MarkerSize = 10f,
                    Color = Color.FromColor(System.Drawing.Color.Orange),
                };
                wpfplot2.Plot.PlottableList.Add(markerPlot2);
            }
            wpfplot1.Refresh();
            wpfplot2.Refresh();

        }

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            var listView = !IsExchange ? listView1 : listView2;

            listView1.Height = ListRow2.ActualHeight - 32;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            var listView = IsExchange ? listView1 : listView2;

            listView.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }

        /// <summary>
        /// Adds a visible spectrum rainbow color bar to the bottom of the chart.
        /// </summary>
        private static void AddSpectrumColorBar(ScottPlot.WPF.WpfPlot plotControl)
        {
            for (int wl = 380; wl < 780; wl += 2)
            {
                var color = WavelengthToColor.Convert(wl);
                var scottColor = new ScottPlot.Color(color.R, color.G, color.B);

                var rect = plotControl.Plot.Add.Rectangle(wl, wl + 2, -0.01, -0.06);
                rect.FillColor = scottColor;
                rect.LineColor = scottColor;
                rect.LineWidth = 0;
            }
        }

        public void Clear()
        {
            ViewResults.Clear();
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();
        }

        public void AddViewResultSpectrum(ViewResultSpectrum viewResultSpectrum)
        {
            ViewResults.Add(viewResultSpectrum);
            ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
            AbsoluteScatterPlots.Add(viewResultSpectrum.AbsoluteScatterPlot);
            listView1.SelectedIndex = ViewResults.Count - 1;
            listView1.ScrollIntoView(viewResultSpectrum);
        }
        private void Inquire_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            var query = DB.Queryable<SpectumResultEntity>().Where(x => x.DataType == DisplayConfig.IsLuminousFluxMode);
            query = query.OrderBy(x => x.Id, Config.OrderByType);
            var dbList = Config.Count > 0 ? query.Take(Config.Count).ToList() : query.ToList();
            foreach (var item in dbList)
            {
                ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(item);
                ViewResults.Add(viewResultSpectrum);
                ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
                AbsoluteScatterPlots.Add(viewResultSpectrum.AbsoluteScatterPlot);
            }
            if (ViewResults.Count > 0)
                listView1.SelectedIndex = 0;
        }
        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            GenericQuery<SpectumResultEntity, ViewResultSpectrum> genericQuery = new GenericQuery<SpectumResultEntity, ViewResultSpectrum>(DB, ViewResults, 
                t =>
                {
                    ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(t);

                    ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
                    AbsoluteScatterPlots.Add(viewResultSpectrum.AbsoluteScatterPlot);
                    return viewResultSpectrum;
                }
                );
            genericQuery.PreQuery += (s, e) =>
            {
                ScatterPlots.Clear();
                AbsoluteScatterPlots.Clear();
            };
            genericQuery.QueryCompleted += (s, e) =>
            {
                if (ViewResults.Count > 0)
                {
                    listView1.Visibility = Visibility.Visible;
                    listView2.Visibility = Visibility.Visible;
                    listView1.SelectedIndex = 0;
                }
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

        private bool IsExchange;

        private void Exchange_Click(object sender, RoutedEventArgs e)
        {
            IsExchange = !IsExchange;
            var listD = IsExchange ? listView1 : listView2;
            var listL = IsExchange ? listView2 : listView1;
            if (listD.Parent is Grid parent1 && listL.Parent is Grid parent2)
            {
                var tempCol = Grid.GetColumn(listD);
                var tempRow = Grid.GetRow(listD);

                var tempCol1 = Grid.GetColumn(listL);
                var tempRow1 = Grid.GetRow(listL);

                parent1.Children.Remove(listD);
                parent2.Children.Remove(listL);

                parent1.Children.Add(listL);
                parent2.Children.Add(listD);

                Grid.SetColumn(listD, tempCol1);
                Grid.SetRow(listD, tempRow1);

                Grid.SetColumn(listL, tempCol);
                Grid.SetRow(listL, tempRow);


                listD.Width = listL.ActualWidth;
                listL.Height = listD.ActualHeight;
                listD.Height = double.NaN;
                listL.Width = double.NaN;
            }
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ViewResultImage);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    if (attribute != null)
                    {
                        string displayName = attribute.DisplayName;
                        displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                        if (displayName == gridViewColumnHeader.Content.ToString())
                        {
                            var item = GridViewColumnVisibilitys.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                            if (item != null)
                            {
                                item.IsSortD = !item.IsSortD;
                                ViewResults.SortByProperty(property.Name, item.IsSortD);
                            }
                        }
                    }
                }
            }
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
                "Dominant Wavelength Color",
                "Voltgage(V) (V)",
                "Current(I) (mA)"
            };

            AddSpectrumHeaders(headers);

            StringBuilder csvBuilder = new();
            AppendCsvLine(csvBuilder, headers);

            foreach (ViewResultSpectrum result in results)
            {
                List<string> row = new()
                {
                    result.Id.ToString(),
                    result.BatchID?.ToString() ?? string.Empty,
                    result.IP ?? string.Empty,
                    result.Lv ?? string.Empty,
                    result.Blue ?? string.Empty,
                    result.fx.ToString(),
                    result.fy.ToString(),
                    result.fu.ToString(),
                    result.fv.ToString(),
                    result.fCCT.ToString(),
                    result.fLd.ToString(),
                    result.fPur.ToString(),
                    result.fLp.ToString(),
                    result.fRa.ToString(),
                    result.fHW.ToString(),
                    result.ExcitationPurity.ToString(),
                    result.DominantWavelengthHex ?? string.Empty,
                    result.V?.ToString() ?? string.Empty,
                    result.I?.ToString() ?? string.Empty,
                };

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
                "EQE",
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

            AddSpectrumHeaders(headers);

            StringBuilder csvBuilder = new();
            AppendCsvLine(csvBuilder, headers);

            foreach (ViewResultSpectrum result in results)
            {
                List<string> row = new()
                {
                    result.Id.ToString(),
                    result.BatchID?.ToString() ?? string.Empty,
                    result.IP ?? string.Empty,
                    result.Eqe?.ToString() ?? string.Empty,
                    result.LuminousFlux?.ToString() ?? string.Empty,
                    result.RadiantFlux?.ToString() ?? string.Empty,
                    result.LuminousEfficacy?.ToString() ?? string.Empty,
                    result.fx.ToString(),
                    result.fy.ToString(),
                    result.fCCT.ToString(),
                    result.fLp.ToString(),
                    result.ExcitationPurity.ToString(),
                    result.DominantWavelengthHex ?? string.Empty,
                    result.V?.ToString() ?? string.Empty,
                    result.I?.ToString() ?? string.Empty,
                };

                AddSpectrumValues(row, result.SpectralDatas);
                AppendCsvLine(csvBuilder, row);
            }

            File.WriteAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
        }

        private static void AddSpectrumHeaders(List<string> headers)
        {
            for (int wl = StartWavelength; wl <= EndWavelength; wl++)
            {
                headers.Add(wl.ToString());
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
                row.Add(index < spectralCount ? safeSpectralDatas[index].AbsoluteSpectrum.ToString() : string.Empty);
            }

            for (int wl = StartWavelength; wl <= EndWavelength; wl++)
            {
                int index = wl - StartWavelength;
                row.Add(index < spectralCount ? safeSpectralDatas[index].RelativeSpectrum.ToString() : string.Empty);
            }
        }

        private static void AppendCsvLine(StringBuilder csvBuilder, List<string> values)
        {
            csvBuilder.AppendLine(string.Join(",", values));
        }
    }
}
