﻿using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using NPOI.XWPF.UserModel;
using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using ColorVision.Device.Spectrum.Views;
using static cvColorVision.GCSDLL;
using Newtonsoft.Json;
using System.Linq;
using ColorVision.Util;
using ColorVision.Sorts;
using ColorVision.Services.Algorithm;
using MQTTMessageLib.Algorithm;
using System.Windows.Documents;
using ColorVision.Device.Spectrum.Configs;

namespace ColorVision.Device.Spectrum.Views
{
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSpectrum : UserControl,IView
    {
        private ResultService spectumResult = new ResultService();
        public ObservableCollection<ViewResultSpectrum> ViewResultSpectrums { get; set; } = new ObservableCollection<ViewResultSpectrum>();

        public View View { get; set; }

        public ViewSpectrum()
        {
            InitializeComponent();
        }

        static int ResultNum;
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            TextBox TextBox1 = new TextBox() { Width = 10, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = System.Windows.Media.Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            this.MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };
            View = new View();


            listView1.ItemsSource = ViewResultSpectrums;



            wpfplot1.Plot.Title("相对光谱曲线");
            wpfplot1.Plot.XLabel("波长[nm]");
            wpfplot1.Plot.YLabel("相对光谱");
            wpfplot1.Plot.Clear();
            wpfplot1.Plot.SetAxisLimitsX(380, 810);
            wpfplot1.Plot.SetAxisLimitsY(0, 1);
            wpfplot1.Plot.XAxis.SetBoundary(370, 1000);
            wpfplot1.Plot.YAxis.SetBoundary(0, 1);

            listView1.Visibility = Visibility.Collapsed;
            listView2.Visibility = Visibility.Collapsed;
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

            CsvWriter.WriteToCsv(ViewResultSpectrums[listView1.SelectedIndex], dialog.FileName);
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                listView2.ItemsSource = ViewResultSpectrums[listview.SelectedIndex].SpectralDatas;
            }
        }

        bool MulComparison;
        ScatterPlot? LastMulSelectComparsion;

        private void DrawPlot()
        {
            if (listView1.SelectedIndex < 0) return;
            if (ScatterPlots.Count > 0)
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

                    wpfplot1.Plot.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;

                }
            }

            wpfplot1.Refresh();
        }

        bool First;

        public void SpectrumDrawPlot(SpectumData data)
        {
            if (!First)
            {
                listView1.Visibility = Visibility.Visible;
                First = true;
            }

            ColorParam colorParam = data.Data;

            ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(colorParam);
            viewResultSpectrum.ID = data.ID;
            viewResultSpectrum.V = data.V;
            viewResultSpectrum.I = data.I;
            ViewResultSpectrums.Add(viewResultSpectrum);

            ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
            listView1.SelectedIndex = ViewResultSpectrums.Count - 1;
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
            if (listView1.SelectedIndex < 0) return;

            wpfplot1.Plot.Clear();
            wpfplot1.Plot.SetAxisLimitsX(ViewResultSpectrums[listView1.SelectedIndex].fSpect1, ViewResultSpectrums[listView1.SelectedIndex].fSpect2);
            wpfplot1.Plot.SetAxisLimitsY(0, 1);
            wpfplot1.Plot.XAxis.SetBoundary(ViewResultSpectrums[listView1.SelectedIndex].fSpect1-10, ViewResultSpectrums[listView1.SelectedIndex].fSpect2+10);
            wpfplot1.Plot.YAxis.SetBoundary(0, 1);
            LastMulSelectComparsion = null;
            if (MulComparison)
            {
                listView1.SelectedIndex = listView1.Items.Count > 0 && listView1.SelectedIndex == -1 ? 0 : listView1.SelectedIndex;
                for (int i = 0; i < ViewResultSpectrums.Count; i++)
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
            var selectedItems = listView1.SelectedItems;

            if(selectedItems.Count<=1)
            {
                ViewResultSpectrums.Clear();
                ScatterPlots.Clear();
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
                    if (item is ViewResultSpectrum result)
                    {
                        ViewResultSpectrums.Remove(result);
                        ScatterPlots.Remove(result.ScatterPlot);
                    }
                }
            }

            if (ViewResultSpectrums.Count > 0)
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
                ViewResultSpectrums.RemoveAt(listView1.SelectedIndex);


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
                    Y = ViewResultSpectrums[listView1.SelectedIndex].fPL[listView2.SelectedIndex * 10],
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
            listView1.Height = ListRow2.ActualHeight - 38;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            listView2.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }

        internal void Clear()
        {
            wpfplot1.Plot.Clear();
            listView1.Items.Clear();
            listView2.Items.Clear();
            ResultNum = 0;
        }

        SpectumResultDao spectumResultDao = new SpectumResultDao();


        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            ViewResultSpectrums.Clear();
            ScatterPlots.Clear();
            if (string.IsNullOrEmpty(TextBoxId.Text) && string.IsNullOrEmpty(TextBoxBatch.Text))
            {
                var list = spectumResultDao.GetAll();
                foreach (var item in list)
                {
                    ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(item);
                    viewResultSpectrum.V = float.NaN;
                    viewResultSpectrum.I = float.NaN;
                    ViewResultSpectrums.Add(viewResultSpectrum);
                    ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
                };
            }
            else
            {

                var list = spectumResultDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text);
                foreach (var item in list)
                {
                    ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(item);
                    viewResultSpectrum.V = float.NaN;
                    viewResultSpectrum.I = float.NaN;
                    ViewResultSpectrums.Add(viewResultSpectrum);
                    ScatterPlots.Add(viewResultSpectrum.ScatterPlot);
                };
            }
            if (ViewResultSpectrums.Count > 0)
            {
                listView1.Visibility = Visibility.Visible;
                First = true;
                listView1.SelectedIndex = 0;

            }

        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SerchPopup.IsOpen = true;
            TextBoxId.Text = string.Empty;
        }

        private void Order_Click(object sender, RoutedEventArgs e)
        {
            OrderPopup.IsOpen = true;
        }
        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (RadioID?.IsChecked == true)
            {
                if (RadioUp?.IsChecked ==true)
                {
                    ViewResultSpectrums.SortByID();
                }
                if (RadioDown?.IsChecked == true)
                {
                    ViewResultSpectrums.SortByID(true);
                }

            }
            OrderPopup.IsOpen = false;
        }
    }
}
