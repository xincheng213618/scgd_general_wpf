#pragma warning disable CS8604
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSpectrum : UserControl,IView
    {
        public ObservableCollection<ViewResultSpectrum> ViewResults { get; set; } = new ObservableCollection<ViewResultSpectrum>();
        public View View { get; set; }

        public static ViewSpectrumConfig Config => ViewSpectrumConfig.Instance;

        public ViewSpectrum()
        {
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
            View = new View();


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
            wpfplot1.Plot.Axes.SetLimitsY(0, 1);
            wpfplot1.Plot.Axes.Bottom.Min = 370;
            wpfplot1.Plot.Axes.Bottom.Max = 1000;
            wpfplot1.Plot.Axes.Left.Min = 0;
            wpfplot1.Plot.Axes.Left.Max = 1;
            string titleAbsolute = ColorVision.Engine.Properties.Resources.AbsoluteSpectrumCurve;
            wpfplot2.Plot.XLabel(ColorVision.Engine.Properties.Resources.WavelengthNm);
            wpfplot2.Plot.YLabel(ColorVision.Engine.Properties.Resources.AbsoluteSpectrum);
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
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("SpectrometerExportyyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var csvBuilder = new StringBuilder();

            List<string> properties = new();
            properties.Add("No");
            properties.Add("Lot");
            properties.Add("IP");
            properties.Add("Luminace（Lv）(cd/m²)");
            properties.Add("Blue Light Intensity");
            properties.Add("Cx");
            properties.Add("Cy");
            properties.Add("u'");
            properties.Add("v'");
            properties.Add("Correlated Color Temperature(CCT)（K）");
            properties.Add("DW（λd）（nm）");
            properties.Add("Color Purity(%)");
            properties.Add("Peak Wavelength(λp)(nm)");
            properties.Add("Color Rendering (Ra)");
            properties.Add("FWHM");
            properties.Add("Voltgage(V) (V)");
            properties.Add("Current(I) (mA)");

            for (int i = 380; i <= 780; i++)
            {
                properties.Add(i.ToString());
            }
            for (int i = 380; i <= 780; i++)
            {
                properties.Add("sp" + i.ToString());
            }
            // 写入列头
            for (int i = 0; i < properties.Count; i++)
            {
                // 添加列名
                csvBuilder.Append(properties[i]);

                // 如果不是最后一列，则添加逗号
                if (i < properties.Count - 1)
                    csvBuilder.Append(',');
            }
            // 添加换行符
            csvBuilder.AppendLine();


            var selectedItemsCopy = new List<object>();
            foreach (var item in listView1.SelectedItems)
            {
                selectedItemsCopy.Add(item);
            }

            foreach (var item in selectedItemsCopy)
            {
                if (item is ViewResultSpectrum result)
                {
                    csvBuilder.Append(result.Id + ",");
                    csvBuilder.Append(result.BatchID + ",");
                    csvBuilder.Append(result.IP + ",");
                    csvBuilder.Append(result.Lv + ",");
                    csvBuilder.Append(result.Blue + ",");
                    csvBuilder.Append(result.fx + ",");
                    csvBuilder.Append(result.fy + ",");
                    csvBuilder.Append(result.fu + ",");
                    csvBuilder.Append(result.fv + ",");
                    csvBuilder.Append(result.fCCT + ",");
                    csvBuilder.Append(result.fLd + ",");
                    csvBuilder.Append(result.fPur + ",");
                    csvBuilder.Append(result.fLp + ",");
                    csvBuilder.Append(result.fRa + ",");
                    csvBuilder.Append(result.fHW + ",");
                    csvBuilder.Append(result.V + ",");
                    csvBuilder.Append(result.I + ",");

                    for (int i = 0; i < result.SpectralDatas.Count; i++)
                    {
                        csvBuilder.Append(result.SpectralDatas[i].AbsoluteSpectrum);
                        csvBuilder.Append(',');
                    }
                    for (int i = 0; i < result.SpectralDatas.Count; i++)
                    {
                        csvBuilder.Append(result.SpectralDatas[i].RelativeSpectrum);
                        if (i < result.SpectralDatas.Count - 1)
                            csvBuilder.Append(',');
                    }
                    csvBuilder.AppendLine();
                }
            }
            File.WriteAllText(dialog.FileName, csvBuilder.ToString(), Encoding.UTF8);
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
            wpfplot1.Plot.Axes.SetLimitsY(0, 1);
            wpfplot1.Plot.Axes.Bottom.Min = ViewResults[listView1.SelectedIndex].fSpect1;
            wpfplot1.Plot.Axes.Bottom.Max = ViewResults[listView1.SelectedIndex].fSpect2;
            wpfplot1.Plot.Axes.Left.Min = 0;
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

        public void Clear()
        {
            ViewResults.Clear();
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();
        }

        public void AddViewResultSpectrum(ViewResultSpectrum viewResultSpectrum)
        {
            viewResultSpectrum.V = float.NaN;
            viewResultSpectrum.I = float.NaN;
            ViewResults.Add(viewResultSpectrum);
            ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
            AbsoluteScatterPlots.Add(viewResultSpectrum.AbsoluteScatterPlot);
            listView1.SelectedIndex = ViewResults.Count - 1;
        }
        private void Inquire_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();

            var query = MySqlControl.GetInstance().DB.Queryable<SpectumResultEntity>();
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
            GenericQuery<SpectumResultEntity, ViewResultSpectrum> genericQuery = new GenericQuery<SpectumResultEntity, ViewResultSpectrum>(MySqlControl.GetInstance().DB, ViewResults, 
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
}
