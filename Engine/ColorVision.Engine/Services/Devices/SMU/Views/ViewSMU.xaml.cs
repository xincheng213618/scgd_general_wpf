using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Themes.Controls;
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

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSMU : UserControl, IView
    {
        public ObservableCollection<ViewResultSMU> ViewResults { get; set; } = new ObservableCollection<ViewResultSMU>();
        public View View { get; set; }

        public ViewSMU()
        {
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ViewSMUConfig.Instance;
            TextBox TextBox1 = new() { Width = 10, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            MouseDown += (s, e) =>  {TextBox1.Focus();};
            View = new View();
            ViewGridManager.GetInstance().AddView(this);

            listView1.ItemsSource = ViewResults;

            wpfplot1.Plot.Clear();
            wpfplot2.Plot.Clear();


            string title = ColorVision.Engine.Properties.Resources.CurrentCurve;

            wpfplot1.Plot.XLabel(ColorVision.Engine.Properties.Resources.ElectricCurrent);
            wpfplot1.Plot.YLabel(ColorVision.Engine.Properties.Resources.Voltage);
            wpfplot1.Plot.Title(ColorVision.Engine.Properties.Resources.CurrentCurve);
            wpfplot1.Plot.Axes.Title.Label.FontName = Fonts.Detect(title);
            wpfplot1.Plot.Axes.Left.Label.FontName = Fonts.Detect(title);
            wpfplot1.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(title);

            wpfplot2.Plot.XLabel(ColorVision.Engine.Properties.Resources.Voltage);
            wpfplot2.Plot.YLabel(ColorVision.Engine.Properties.Resources.ElectricCurrent);
            wpfplot2.Plot.Title(ColorVision.Engine.Properties.Resources.VoltageCurve);

            wpfplot2.Plot.Axes.Title.Label.FontName = Fonts.Detect(title);
            wpfplot2.Plot.Axes.Left.Label.FontName = Fonts.Detect(title);
            wpfplot2.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(title);

            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                ViewSMUConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                ViewSMUConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
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
                foreach (var item in listView1.SelectedItems.Cast<ViewResultSMU>().ToList())
                    ViewResults.Remove(item);
            }
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && listView2.View is GridView gridView && contextMenu.Items.Count ==0)
                GridViewColumnVisibility.GenContentMenuGridViewColumnZero(contextMenu, gridView.Columns);
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && listView1.View is GridView gridView && contextMenu.Items.Count == 0)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox1.Show(ColorVision.Engine.Properties.Resources.SelectDataFirst);
                return;
            }
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*. csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var viewResult = ViewResults[listView1.SelectedIndex];
            ExportToCsv(viewResult, dialog.FileName);

        }
        private void ExportToCsv(ViewResultSMU viewResult, string fileName)
        {
            using var writer = new StreamWriter(fileName, false, Encoding.UTF8);

            // 写入通用头部信息
            writer.WriteLine($"ID,{viewResult.Id}");
            writer.WriteLine($"创建时间,{viewResult.CreateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");
            writer.WriteLine($"批次ID,{viewResult.BatchID}");
            writer.WriteLine($"测量类型,{(viewResult.MeasurementType == MeasurementType.Voltage ? "电压源" : "电流源")}");
            writer.WriteLine($"数据类型,{viewResult.Type}");
            writer.WriteLine();  // 空行分隔

            if (viewResult.Type == "GetData")
            {
                // GetData模式：导出单点测量结果
                writer.WriteLine("参数,数值");
                writer.WriteLine($"限制值,{viewResult.LimitEnd}");
                writer.WriteLine($"测量电压(V),{viewResult.VResult}");
                writer.WriteLine($"测量电流(A),{viewResult.IResult}");
            }
            else if (viewResult.Type == "Scan")
            {
                // Scan模式：导出扫描数据表
                writer.WriteLine($"扫描起始值,{viewResult.LimitStart}");
                writer.WriteLine($"扫描结束值,{viewResult.LimitEnd}");
                writer.WriteLine($"数据点数,{viewResult.SMUDatas.Count}");
                writer.WriteLine();  // 空行分隔

                writer.WriteLine("序号,电压(V),电流(A)");
                int index = 1;
                foreach (var data in viewResult.SMUDatas)
                {
                    writer.WriteLine($"{index},{data.Voltage},{data.Current}");
                    index++;
                }
            }

            MessageBox1.Show("导出成功！");
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                listView2.ItemsSource = ViewResults[listView1.SelectedIndex].SMUDatas;
            }
        }



        bool MulComparison;
        Scatter? LastMulSelectComparsion;

        private void DrawPlot()
        {
            if (listView1.SelectedIndex < 0)  return;

            if (MulComparison)
            {
                if (LastMulSelectComparsion != null)
                {
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    LastMulSelectComparsion.LineWidth = 1;
                    LastMulSelectComparsion.MarkerSize = 1;
                }


                LastMulSelectComparsion = ViewResults[listView1.SelectedIndex].ScatterPlot;
                LastMulSelectComparsion.LineWidth = 3;
                LastMulSelectComparsion.MarkerSize = 3;
                LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.Red);

                if (ViewResults[listView1.SelectedIndex].IsSourceV)
                {
                    wpfplot2.Plot.PlottableList.Add(LastMulSelectComparsion);
                    wpfplot2.Refresh();

                }
                else
                {
                    wpfplot1.Plot.PlottableList.Add(LastMulSelectComparsion);
                    wpfplot1.Refresh();
                }
            }
            else
            {
               
                var temp = ViewResults[listView1.SelectedIndex].ScatterPlot;
                temp.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                temp.LineWidth = 1;
                temp.MarkerSize = 1;


                ToggleButtonChoice.IsChecked = ViewResults[listView1.SelectedIndex].IsSourceV;

                if (LastMulSelectComparsion != null)
                {
                    if (ViewResults[listView1.SelectedIndex].IsSourceV)
                    {
                        wpfplot2.Plot.PlottableList.Add(temp);
                        wpfplot1.Plot.Remove(LastMulSelectComparsion);
                        wpfplot2.Plot.Remove(LastMulSelectComparsion);
                        wpfplot2.Refresh();

                    }
                    else
                    {
                        wpfplot1.Plot.PlottableList.Add(temp);
                        wpfplot1.Plot.Remove(LastMulSelectComparsion);
                        wpfplot2.Plot.Remove(LastMulSelectComparsion);
                        wpfplot1.Refresh();
                    }

                }


                LastMulSelectComparsion = temp;

            }
        }

        List<PassSxSource> PassSxSources = new();

        public void AddViewResultSMU(ViewResultSMU viewResultSMU)
        {
            ViewResults.Add(viewResultSMU);

            ToggleButtonChoice.IsChecked = viewResultSMU.MeasurementType == MeasurementType.Voltage;

            if (viewResultSMU.MeasurementType == MeasurementType.Voltage)
            {
                ToggleButtonChoice.IsChecked = true;
                wpfplot2.Plot.Axes.Bottom.Min = viewResultSMU.xMin;
                wpfplot2.Plot.Axes.Bottom.Max = viewResultSMU.xMax;
                wpfplot2.Plot.Axes.Left.Min = viewResultSMU.yMin;
                wpfplot2.Plot.Axes.Left.Max = viewResultSMU.yMax;

                wpfplot2.Plot.PlottableList.Add(viewResultSMU.ScatterPlot);
                wpfplot2.Refresh();
            }
            else
            {
                ToggleButtonChoice.IsChecked = false;

                wpfplot1.Plot.Axes.Bottom.Min = viewResultSMU.xMin;
                wpfplot1.Plot.Axes.Bottom.Max = viewResultSMU.xMax;
                wpfplot1.Plot.Axes.Left.Min = viewResultSMU.yMin;
                wpfplot1.Plot.Axes.Left.Max = viewResultSMU.yMax;
                wpfplot1.Plot.PlottableList.Add(viewResultSMU.ScatterPlot);
                wpfplot1.Refresh();
            }
            ToggleButtonChoice.IsChecked = viewResultSMU.IsSourceV;
            listView1.SelectedIndex = ViewResults.Count - 1;
            listView1.ScrollIntoView(viewResultSMU);
        }

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


        private void ReDrawPlot()
        {
            wpfplot1.Plot.Clear();
            wpfplot2.Plot.Clear();

            LastMulSelectComparsion = null;
            if (MulComparison)
            {
                listView1.SelectedIndex = ViewResults.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                for (int i = 0; i < ViewResults.Count; i++)
                {
                    if (i == listView1.SelectedIndex)
                        continue;
                    var plot = ViewResults[i].ScatterPlot;
                    plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    plot.LineWidth = 1;
                    plot.MarkerSize = 1;

                    if (ViewResults[i].IsSourceV)
                    {
                        wpfplot1.Plot.PlottableList.Add(plot);
                    }
                    else
                    {
                        wpfplot2.Plot.PlottableList.Add(plot);
                    }

                }
            }
            DrawPlot();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();

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
                }
            }

        }

        Marker markerPlot1;

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex >= 0)
            {
                if (ViewResults[listView1.SelectedIndex].IsSourceV)
                {
                    wpfplot2.Plot.Remove(markerPlot1);

                    if (listView2.SelectedIndex > -1)
                    {
                        markerPlot1 = new Marker
                        {
                            X = ViewResults[listView1.SelectedIndex].SMUDatas[listView2.SelectedIndex].Voltage,
                            Y = ViewResults[listView1.SelectedIndex].SMUDatas[listView2.SelectedIndex].Current,
                            MarkerShape = MarkerShape.FilledCircle,
                            MarkerSize = 10f,
                            Color = Color.FromColor(System.Drawing.Color.Orange),
                        };
                        wpfplot2.Plot.PlottableList.Add(markerPlot1);
                    }
                    wpfplot2.Refresh();
                }
                else
                {
                    wpfplot1.Plot.Remove(markerPlot1);

                    if (listView2.SelectedIndex > -1)
                    {
                        markerPlot1 = new Marker
                        {
                            X = ViewResults[listView1.SelectedIndex].SMUDatas[listView2.SelectedIndex].Voltage,
                            Y = ViewResults[listView1.SelectedIndex].SMUDatas[listView2.SelectedIndex].Current,
                            MarkerShape = MarkerShape.FilledCircle,
                            MarkerSize = 10f,
                            Color = Color.FromColor(System.Drawing.Color.Orange),
                        };
                        wpfplot1.Plot.PlottableList.Add(markerPlot1);
                    }
                    wpfplot1.Refresh();
                }

            }

        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                Visibility visibility = button.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                listView1.Visibility = visibility;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                Visibility visibility = button.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                listView2.Visibility = visibility;
            }
        }
        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            var listView = !IsExchange ? listView1 : listView2;

            listView1.Height = ListRow2.ActualHeight - 38;
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

        private void Button3_Click(object sender, RoutedEventArgs e)
        {

        }
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            foreach (var item in MySqlControl.GetInstance().DB.Queryable<SMUResultModel>().ToList())
            {
                ViewResultSMU viewResultSMU = new ViewResultSMU(item);
                ViewResults.Add(viewResultSMU);
            }

            foreach (var item in MySqlControl.GetInstance().DB.Queryable<SmuScanModel>().ToList())
            {
                ViewResultSMU viewResultSMU = new ViewResultSMU(item);
                ViewResults.Add(viewResultSMU);
            }
        }


        private void Search1_Click(object sender, RoutedEventArgs e)
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
                Type type = typeof(ViewResultSMU);

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
