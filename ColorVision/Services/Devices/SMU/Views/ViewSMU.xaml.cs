using ColorVision.Common.MVVM;
using ColorVision.Services.Devices.SMU.Dao;
using ColorVision.Sorts;
using ColorVision.Common.Utilities;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Services.Devices.SMU.Views
{
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSMU : UserControl, IView
    {
        public ObservableCollection<ViewResultSMU> ViewResultSMUs { get; set; } = new ObservableCollection<ViewResultSMU>();
        public View View { get; set; }
        public ViewSMU()
        {
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            TextBox TextBox1 = new TextBox() { Width = 10, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            this.MouseDown += (s, e) =>  {TextBox1.Focus();};
            View = new View();
            ViewGridManager.GetInstance().AddView(this);

            listView1.ItemsSource = ViewResultSMUs;

            wpfplot1.Plot.Clear();
            wpfplot2.Plot.Clear();

            listView1.Visibility = Visibility.Collapsed;
            listView2.Visibility = Visibility.Collapsed;

            wpfplot1.Plot.XLabel("电流(A)");
            wpfplot1.Plot.YLabel("电压(V)");
            wpfplot1.Plot.Title("电流曲线");

            wpfplot2.Plot.XLabel("电压(V)");
            wpfplot2.Plot.YLabel("电流(A)");
            wpfplot2.Plot.Title("电压曲线");
            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
            GridViewColumnVisibilityListView.ItemsSource = GridViewColumnVisibilitys;
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        private void OpenColumnVisibilityPopupButton_Click(object sender, RoutedEventArgs e)
        {
            ColumnVisibilityPopup.IsOpen = true;
        }
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AdjustGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
        }
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
                MessageBox.Show("您需要先选择数据");
                return;
            }

            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                CsvWriter.WriteToCsv(ViewResultSMUs[listView1.SelectedIndex], dialog.FileName);

        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                listView2.ItemsSource = ViewResultSMUs[listView1.SelectedIndex].SMUDatas;
            }
        }



        bool MulComparison;
        ScatterPlot? LastMulSelectComparsion;

        private void DrawPlot()
        {
            if (listView1.SelectedIndex < 0)  return;

            if (MulComparison)
            {
                if (LastMulSelectComparsion != null)
                {
                    LastMulSelectComparsion.Color = Color.DarkGoldenrod;
                    LastMulSelectComparsion.LineWidth = 1;
                    LastMulSelectComparsion.MarkerSize = 1;
                }


                LastMulSelectComparsion = ViewResultSMUs[listView1.SelectedIndex].ScatterPlot;
                LastMulSelectComparsion.LineWidth = 3;
                LastMulSelectComparsion.MarkerSize = 3;
                LastMulSelectComparsion.Color = Color.Red;

                if (ViewResultSMUs[listView1.SelectedIndex].IsSourceV)
                {
                    wpfplot2.Plot.Add(LastMulSelectComparsion);
                    wpfplot2.Refresh();

                }
                else
                {
                    wpfplot1.Plot.Add(LastMulSelectComparsion);
                    wpfplot1.Refresh();
                }
            }
            else
            {
               
                var temp = ViewResultSMUs[listView1.SelectedIndex].ScatterPlot;
                temp.Color = Color.DarkGoldenrod;
                temp.LineWidth = 1;
                temp.MarkerSize = 1;


                ToggleButtonChoice.IsChecked = ViewResultSMUs[listView1.SelectedIndex].IsSourceV;

                if (ViewResultSMUs[listView1.SelectedIndex].IsSourceV)
                {
                    wpfplot2.Plot.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    wpfplot2.Plot.Remove(LastMulSelectComparsion);
                    wpfplot2.Refresh();

                }
                else
                {
                    wpfplot1.Plot.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    wpfplot2.Plot.Remove(LastMulSelectComparsion);
                    wpfplot1.Refresh();
                }

                LastMulSelectComparsion = temp;

            }
        }

        List<PassSxSource> PassSxSources = new List<PassSxSource>();

        public void DrawPlot(bool isSourceV, double endVal,double[] VList, double[] IList)
        {

            ViewResultSMU viewResultSMU = new ViewResultSMU(isSourceV? MeasurementType.Voltage: MeasurementType.Current, (float)endVal, VList, IList);
            ViewResultSMUs.Add(viewResultSMU);
            ToggleButtonChoice.IsChecked = viewResultSMU.MeasurementType == MeasurementType.Voltage;

            if (viewResultSMU.MeasurementType == MeasurementType.Voltage)
            {
                ToggleButtonChoice.IsChecked = true;

                wpfplot2.Plot.SetAxisLimitsX(viewResultSMU.xMin, viewResultSMU.xMax);
                wpfplot2.Plot.SetAxisLimitsY(viewResultSMU.yMin, viewResultSMU.yMax);
                wpfplot2.Plot.Add(viewResultSMU.ScatterPlot);
                wpfplot2.Refresh();
            }
            else
            {
                ToggleButtonChoice.IsChecked = false;

                wpfplot1.Plot.SetAxisLimitsY(viewResultSMU.xMin, viewResultSMU.xMax);
                wpfplot1.Plot.SetAxisLimitsX(viewResultSMU.yMin, viewResultSMU.yMax);
                wpfplot1.Plot.Add(viewResultSMU.ScatterPlot);
                wpfplot1.Refresh();
            }

            ToggleButtonChoice.IsChecked = isSourceV;
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
                listView1.SelectedIndex = ViewResultSMUs.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                for (int i = 0; i < ViewResultSMUs.Count; i++)
                {
                    if (i == listView1.SelectedIndex)
                        continue;
                    var plot = ViewResultSMUs[i].ScatterPlot;
                    plot.Color = Color.DarkGoldenrod;
                    plot.LineWidth = 1;
                    plot.MarkerSize = 1;

                    if (ViewResultSMUs[i].IsSourceV)
                    {
                        wpfplot1.Plot.Add(plot);
                    }
                    else
                    {
                        wpfplot2.Plot.Add(plot);
                    }

                }
            }
            DrawPlot();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show("您需要先选择数据");
                return;
            }
            var selectedItems = listView1.SelectedItems;

            if (selectedItems.Count <= 1)
            {
                ViewResultSMUs.Clear();
            }
            else
            {

                var selectedItemsCopy = new List<object>();

                foreach (var item in selectedItems)
                {
                    selectedItemsCopy.Add(item);
                }

                foreach (var item in selectedItemsCopy)
                {
                    if (item is ViewResultSMU result)
                    {
                        ViewResultSMUs.Remove(result);
                    }
                }
            }

            if (ViewResultSMUs.Count > 0)
            {
                listView1.SelectedIndex = 0;
            }
            else
            {
                wpfplot1.Plot.Clear();
                wpfplot1.Refresh();
            }
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

                ViewResultSMUs.RemoveAt(listView1.SelectedIndex);

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

        MarkerPlot markerPlot1;

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex >= 0)
            {
                if (ViewResultSMUs[listView1.SelectedIndex].IsSourceV)
                {
                    wpfplot2.Plot.Remove(markerPlot1);

                    if (listView2.SelectedIndex > -1)
                    {
                        markerPlot1 = new MarkerPlot
                        {
                            X = ViewResultSMUs[listView1.SelectedIndex].SMUDatas[listView2.SelectedIndex].Voltage,
                            Y = ViewResultSMUs[listView1.SelectedIndex].SMUDatas[listView2.SelectedIndex].Current,
                            MarkerShape = MarkerShape.filledCircle,
                            MarkerSize = 10f,
                            Color = Color.Orange,
                            Label = null
                        };
                        wpfplot2.Plot.Add(markerPlot1);
                    }
                    wpfplot2.Refresh();
                }
                else
                {
                    wpfplot1.Plot.Remove(markerPlot1);

                    if (listView2.SelectedIndex > -1)
                    {
                        markerPlot1 = new MarkerPlot
                        {
                            X = ViewResultSMUs[listView1.SelectedIndex].SMUDatas[listView2.SelectedIndex].Voltage,
                            Y = ViewResultSMUs[listView1.SelectedIndex].SMUDatas[listView2.SelectedIndex].Current,
                            MarkerShape = MarkerShape.filledCircle,
                            MarkerSize = 10f,
                            Color = Color.Orange,
                            Label = null
                        };
                        wpfplot1.Plot.Add(markerPlot1);
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
            listView1.Height = ListRow2.ActualHeight -38;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            listView2.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {

        }


        MRSmuScanDao MRSmuScanDao = new MRSmuScanDao();
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResultSMUs.Clear();
            foreach (var item in MRSmuScanDao.GetAll())
            {
                ViewResultSMU viewResultSMU = new ViewResultSMU(item);
                ViewResultSMUs.Add(viewResultSMU);
                ToggleButtonChoice.IsChecked = viewResultSMU.MeasurementType == MeasurementType.Voltage;
            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            ViewResultSMUs.Clear();
            if (string.IsNullOrEmpty(TextBoxId.Text) && string.IsNullOrEmpty(TextBoxBatch.Text) && SearchTimeSart.SelectedDateTime==DateTime.MinValue)
            {
                foreach (var item in MRSmuScanDao.GetAll())
                {
                    ViewResultSMU viewResultSMU = new ViewResultSMU(item);
                    ViewResultSMUs.Add(viewResultSMU);
                };
            }
            else
            {

                var list = MRSmuScanDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text ,SearchTimeSart.SelectedDateTime, SearchTimeEnd.SelectedDateTime);
                foreach (var item in list)
                {
                    ViewResultSMU viewResultSMU = new ViewResultSMU(item);
                    ViewResultSMUs.Add(viewResultSMU);
                };
            }
            if (ViewResultSMUs.Count > 0)
            {
                listView1.Visibility = Visibility.Visible;
                listView1.SelectedIndex = 0;
            }
            SerchPopup.IsOpen = false;
        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SerchPopup.IsOpen = true;
            TextBoxId.Text = string.Empty;
            TextBoxBatch.Text = string.Empty;
        }

        private void Exchange_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.Parent is Grid parent1 && listView2.Parent is Grid parent2)
            {
                var tempCol = Grid.GetColumn(listView1);
                var tempRow = Grid.GetRow(listView1);

                var tempCol1 = Grid.GetColumn(listView2);
                var tempRow1 = Grid.GetRow(listView2);

                parent1.Children.Remove(listView1);
                parent2.Children.Remove(listView2);

                var tempwidth = listView1.ActualWidth;
                var tempheight = listView1.ActualHeight;

                listView1.Width = listView2.ActualWidth;
                listView1.Height = listView2.ActualHeight;

                listView2.Width = tempwidth;
                listView2.Height = tempheight;

                parent1.Children.Add(listView2);
                parent2.Children.Add(listView1);

                Grid.SetColumn(listView1, tempCol1);
                Grid.SetRow(listView1, tempRow1);

                Grid.SetColumn(listView2, tempCol);
                Grid.SetRow(listView2, tempRow);

            }
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content !=null)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        switch (item.ColumnName)
                        {
                            case "序号":
                                item.IsSortD = !item.IsSortD;
                                ViewResultSMUs.SortByID(item.IsSortD);
                                break;
                            case "测量时间":
                                item.IsSortD = !item.IsSortD;
                                ViewResultSMUs.SortByCreateTime(item.IsSortD);
                                break;
                            case "批次号":
                                item.IsSortD = !item.IsSortD;
                                ViewResultSMUs.SortByBatchID(item.IsSortD);
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                }
            }
        }
    }
}
