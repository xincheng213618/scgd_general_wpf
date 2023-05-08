#pragma warning disable CA1707
using ColorVision.Extension;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Template
{
    //Aoi检测部分
    public class AoiParam
    {
        public bool filter_by_area { set; get; }
        public int max_area { set; get; }
        public int min_area { set; get; }
        public bool filter_by_contrast { set; get; }
        public float max_contrast { set; get; }
        public float min_contrast { set; get; }
        public float contrast_brightness { set; get; }
        public float contrast_darkness { set; get; }
        public int blur_size { set; get; }
        public int min_contour_size { set; get; }
        public int erode_size { set; get; }
        public int dilate_size { set; get; }
        [CategoryAttribute("AoiRect")]
        public int left { set; get; }
        [CategoryAttribute("AoiRect")]
        public int right { set; get; }
        [CategoryAttribute("AoiRect")]
        public int top { set; get; }
        [CategoryAttribute("AoiRect")]
        public int bottom { set; get; }
    };


    public class ListConfig
    {
        public int ID { set; get; }
        public string Name { set; get; }

        public object Value { set; get; }
    }


    /// <summary>
    /// WindowAOI.xaml 的交互逻辑
    /// </summary>
    public partial class WindowAOI : Window
    {
        
        public WindowAOI()
        {
            InitializeComponent();
        }

        private Dictionary<string, AoiParam> mapAoiParam;
        private ObservableCollection<ListConfig> ListConfigs = new ObservableCollection<ListConfig>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            mapAoiParam= new Dictionary<string, AoiParam>();
            mapAoiParam.Add("ssss", new AoiParam());
            ListConfigs = new ObservableCollection<ListConfig>();
            int id = 1;
            foreach (var item in mapAoiParam)
            {

                ListConfig listConfig = new ListConfig();
                listConfig.ID = id++;
                listConfig.Name = item.Key;
                listConfig.Value = item.Value;
                ListConfigs.Add(listConfig);
            }
            ListView1.ItemsSource = ListConfigs;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                PropertyGrid1.SelectedObject = ListConfigs[listView.SelectedIndex].Value;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!TextBox1.Text.IsNullOrEmpty())
            {
                ListConfigs.Add(new ListConfig() { ID = ListConfigs .Count+1,Name = TextBox1.Text,Value = new AoiParam()});
                TextBox1.Text =string.Empty;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show($"是否删除模板{ListView1.SelectedIndex+1},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    ListConfigs.RemoveAt(ListView1.SelectedIndex);
                }
            }
        }
    }
}
