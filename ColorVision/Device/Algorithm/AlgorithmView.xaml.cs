#pragma  warning disable CA1708, CS8602， CS8604
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Template;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
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

namespace ColorVision.Device.Algorithm
{

    public class PoiResult
    {
        public double CCT { get; set; }
        public double Wave { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double u { get; set; }
        public double v { get; set; }
        public double x { get; set; }
        public double y { get; set; }


    }

    /// <summary>
    /// SpectrumView.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IView
    {
        public View View { get; set; }
        public AlgorithmView()
        {
            InitializeComponent();
        }

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
            View.ViewIndexChangedEvent += (s, e) =>
            {
                if (e == -2)
                {
                    MenuItem menuItem3 = new MenuItem { Header = "还原到主窗口中" };
                    menuItem3.Click += (s, e) =>
                    {
                        if (ViewGridManager.GetInstance().IsGridEmpty(View.PreViewIndex))
                        {
                            View.ViewIndex = View.PreViewIndex;
                        }
                        else
                        {
                            View.ViewIndex = -1;
                        }
                    };
                    this.ContextMenu = new ContextMenu();
                    this.ContextMenu.Items.Add(menuItem3);

                }
                else
                {
                    MenuItem menuItem = new MenuItem() { Header = "设为主窗口" };
                    menuItem.Click += (s, e) => { ViewGridManager.GetInstance().SetOneView(this); };
                    MenuItem menuItem1 = new MenuItem() { Header = "展示全部窗口" };
                    menuItem1.Click += (s, e) => { ViewGridManager.GetInstance().SetViewNum(-1); };
                    MenuItem menuItem2 = new MenuItem() { Header = "独立窗口中显示" };
                    menuItem2.Click += (s, e) => { View.ViewIndex = -2; };
                    MenuItem menuItem3 = new MenuItem() { Header = Properties.Resource.WindowHidden };
                    menuItem3.Click += (s, e) => { View.ViewIndex = -1; };
                    this.ContextMenu = new ContextMenu();
                    this.ContextMenu.Items.Add(menuItem);
                    this.ContextMenu.Items.Add(menuItem1);
                    this.ContextMenu.Items.Add(menuItem2);
                    this.ContextMenu.Items.Add(menuItem3);

                }
            };

            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号","属性", "测量时间" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(string.Format("[{0}]", i)) });
            }
            listView1.View = gridView;
            List<string> headers2 = new List<string> { "CCT","Wave","X","Y","Z","u","v","x","y" };

            GridView gridView2 = new GridView();
            for (int i = 0; i < headers2.Count; i++)
            {
                gridView2.Columns.Add(new GridViewColumn() { Header = headers2[i], DisplayMemberBinding = new Binding(headers2[i]) });
            }
            listView2.View = gridView2;

            listView2.ItemsSource = PoiResults;

        }

        public ObservableCollection<PoiResult> PoiResults { get; set; } = new ObservableCollection<PoiResult>();

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

        public void PoiDataDraw(List<PoiResultModel> poiResultModels)
        {
            foreach (var item in poiResultModels)
            {
                try
                {
                    PoiResult poiResult = JsonConvert.DeserializeObject<PoiResult>(item.Value.ToString());
                    PoiResults.Add(poiResult);
                }
                catch
                {

                }

            }

        }





        private List<List<string>> ListContents { get; set; } = new List<List<string>>() { };

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {

        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
