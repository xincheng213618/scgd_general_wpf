﻿using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Templates;
using Newtonsoft.Json;
using NPOI.XWPF.UserModel;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Services.Device.SMU.Views
{
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSMU : UserControl, IView
    {
        public ObservableCollection<ViewResultSMU> ViewResultSMUs { get; set; } = new ObservableCollection<ViewResultSMU>();
        public View View { get; set; }
        private ResultService spectumResult;
        public ViewSMU()
        {
            spectumResult = new ResultService();
            InitializeComponent();
        }

        static int ResultNum;
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            TextBox TextBox1 = new TextBox() { Width = 10, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            this.MouseDown += (s, e) =>  {TextBox1.Focus();};

            View = new View();

            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号","属性", "测量时间" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(string.Format("[{0}]", i)) });
            }
            listView1.View = gridView;

            List<string> headers2 = new List<string> { "电流","电压" };

            GridView gridView2 = new GridView();
            for (int i = 0; i < headers2.Count; i++)
            {
                gridView2.Columns.Add(new GridViewColumn() { Header = headers2[i], DisplayMemberBinding = new Binding(string.Format("[{0}]", i)) });
            }

            listView2.View = gridView2;

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
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using StreamWriter file = new StreamWriter(dialog.FileName, true, Encoding.UTF8);
                if (listView1.View is GridView gridView1)
                {
                    string headers = "";
                    foreach (var item in gridView1.Columns)
                    {
                        headers += item.Header.ToString() + ",";
                    }
                    file.WriteLine(headers);
                }
                string value = "";
                foreach (var item in ListContents[listView1.SelectedIndex])
                {
                    value += item + ",";
                }
                file.WriteLine(value);
            }

        }

        private List<List<string>> ListContents { get; set; } = new List<List<string>>() { };

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                //listView2.ItemsSource;
            }
        }



        bool MulComparison;
        ScatterPlot? LastMulSelectComparsion;

        private void DrawPlot()
        {
            if (MulComparison)
            {
                if (LastMulSelectComparsion != null)
                {
                    LastMulSelectComparsion.Color = Color.DarkGoldenrod;
                    LastMulSelectComparsion.LineWidth = 1;
                    LastMulSelectComparsion.MarkerSize = 1;
                }


                LastMulSelectComparsion = ScatterPlots[listView1.SelectedIndex];
                LastMulSelectComparsion.LineWidth = 3;
                LastMulSelectComparsion.MarkerSize = 3;
                LastMulSelectComparsion.Color = Color.Red;
                wpfplot1.Plot.Add(LastMulSelectComparsion);

            }
            else
            {
               
                var temp = ScatterPlots[listView1.SelectedIndex];
                temp.Color = Color.DarkGoldenrod;
                temp.LineWidth = 1;
                temp.MarkerSize = 1;



                ToggleButtonChoice.IsChecked = PassSxSources[listView1.SelectedIndex].IsSourceV;

                if (PassSxSources[listView1.SelectedIndex].IsSourceV)
                {
                    wpfplot2.Plot.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    wpfplot2.Plot.Remove(LastMulSelectComparsion);
                }
                else
                {
                    wpfplot1.Plot.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    wpfplot2.Plot.Remove(LastMulSelectComparsion);

                }

                LastMulSelectComparsion = temp;

            }
            wpfplot1.Refresh();
        }

        List<PassSxSource> PassSxSources = new List<PassSxSource>();

        public void DrawPlot(bool isSourceV, double endVal,double[] VList, double[] IList)
        {

            PassSxSource passSxSources = new PassSxSource();
            passSxSources.IsSourceV = isSourceV;
            PassSxSources.Add(passSxSources);


            List<double> listV = new List<double>();
            List<double> listI = new List<double>();
            double VMax = 0, IMax = 0, VMin = 10000, IMin = 10000;
            for (int i = 0; i < VList.Length; i++)
            {
                if (VList[i] > VMax) VMax = VList[i];
                if (IList[i] > IMax) IMax = IList[i];
                if (VList[i] < VMin) VMin = VList[i];
                if (IList[i] < IMin) IMin = IList[i];

                listV.Add(VList[i]);
                listI.Add(IList[i]);
            }
            int step = 10;
            double xMin = 0;
            double xMax = VMax + VMax / step;
            double yMin = 0 - IMax / step;
            double yMax = IMax + IMax / step;
            double[] xs, ys;
            if (isSourceV)
            {
                xMin = VMin - VMin / step;
                xMax = endVal + VMax / step;
                yMin = IMin - IMin / step;
                yMax = IMax + IMax / step;
                if (VMax < endVal)
                {
                    double addPointStep = (endVal - VMax) / 2.0;
                    listV.Add(VMax + addPointStep);
                    listV.Add(endVal);
                    listI.Add(IMax);
                    listI.Add(IMax);
                }
                xs = listV.ToArray();
                ys = listI.ToArray();

                ScatterPlot scatterPlot = new ScatterPlot(xs, ys)
                {
                    Color = Color.DarkGoldenrod,
                    LineWidth = 1,
                    MarkerSize = 1,
                    Label = null,
                    MarkerShape = MarkerShape.none,
                    LineStyle = LineStyle.Solid
                };

                wpfplot2.Plot.SetAxisLimitsX(xMin, xMax);
                wpfplot2.Plot.SetAxisLimitsY(yMin, yMax);
                wpfplot2.Plot.Add(scatterPlot);
                wpfplot2.Refresh();
                ScatterPlots.Add(scatterPlot);


            }
            else
            {
                endVal = endVal / 1000;
                xMin = IMin - IMin / step;
                xMax = endVal + IMax / step;
                yMin = VMin - VMin / step;
                yMax = VMax + VMax / step;
                if (IMax < endVal)
                {
                    double addPointStep = (endVal - IMax) / 2.0;
                    listI.Add(IMax + addPointStep);
                    listI.Add(endVal);
                    listV.Add(VMax);
                    listV.Add(VMax);
                }
                xs = listV.ToArray();
                ys = listI.ToArray();

                ScatterPlot scatterPlot = new ScatterPlot(xs, ys)
                {
                    Color = Color.DarkGoldenrod,
                    LineWidth = 1,
                    MarkerSize = 1,
                    Label = null,
                    MarkerShape = MarkerShape.none,
                    LineStyle = LineStyle.Solid
                };
                wpfplot1.Plot.SetAxisLimitsY(xMin, xMax);
                wpfplot1.Plot.SetAxisLimitsX(yMin, yMax);
                wpfplot1.Plot.Add(scatterPlot);
                wpfplot1.Refresh();
                ScatterPlots.Add(scatterPlot);

            }
            ToggleButtonChoice.IsChecked = isSourceV;


            ListViewItem listViewItem = new ListViewItem();
            ResultNum++;
            List<string> strings = new List<string>();
            strings.Add(ResultNum.ToString());
            strings.Add(isSourceV ? "V" : "I");
            strings.Add(DateTime.Now.ToString());
            listViewItem.Content = strings;
            ListContents.Add(strings);
            listView1.Items.Add(listViewItem);
            listView1.SelectedIndex = PassSxSources.Count - 1;
            listView1.ScrollIntoView(listViewItem);

        }

        private List<ScatterPlot> ScatterPlots { get; set; } = new List<ScatterPlot>();

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
            LastMulSelectComparsion = null;
            if (MulComparison)
            {
                listView1.SelectedIndex = listView1.Items.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                for (int i = 0; i < ScatterPlots.Count; i++)
                {
                    if (i == listView1.SelectedIndex)
                        continue;
                    var plot = ScatterPlots[i];
                    plot.Color = Color.DarkGoldenrod;
                    plot.LineWidth = 1;
                    plot.MarkerSize = 1;

                    wpfplot1.Plot.Add(plot);
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
            List<ListViewItem> lists = new List<ListViewItem>();
            foreach (var item in listView1.Items)
            {
                if (item is ListViewItem listViewItem)
                {
                    lists.Add(listViewItem);
                }
            }
            foreach (var item in lists)
            {
                if (item.IsSelected)
                {
                    int index = listView1.Items.IndexOf(item);
                    ScatterPlots.RemoveAt(index);
                }
            }


            if (listView1.Items.Count > 0)
            {
                listView1.SelectedIndex = 0;
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
                listView1.Items.RemoveAt(listView1.SelectedIndex);


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
            wpfplot1.Plot.Remove(markerPlot1);
            if (listView2.SelectedIndex > -1)
            {
                markerPlot1 = new MarkerPlot
                {
                    X = listView2.SelectedIndex + 380,
                    MarkerShape = MarkerShape.filledCircle,
                    MarkerSize = 10f,
                    Color = Color.Orange,
                    Label = null
                };
                wpfplot1.Plot.Add(markerPlot1);
            }
            wpfplot1.Refresh();
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
            ListContents.Clear();
            listView1.Items.Clear();

            foreach (var item in MRSmuScanDao.GetAll())
            {

                bool isSourceV = item.IsSourceV;
                double endVal = item.SrcEnd;

                double[] VList = JsonConvert.DeserializeObject<double[]> (item.VResult);
                double[] IList = JsonConvert.DeserializeObject<double[]> (item.IResult);

                DrawPlot(isSourceV, endVal, VList, IList);

            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SerchPopup.IsOpen = true;
            TextBoxType.SelectedIndex = -1;
            TextBoxId.Text = string.Empty;
            TextBoxBatch.Text = string.Empty;
            TextBoxFile.Text = string.Empty;
        }
    }
}
