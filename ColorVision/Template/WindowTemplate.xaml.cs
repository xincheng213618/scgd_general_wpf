#pragma warning disable CA1707
using ColorVision.Extension;
using ColorVision.Util;
using cvColorVision;
using OpenCvSharp.Detail;
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

    public enum WindowTemplateType
    {
        AoiParam,
        Calibration
    }





    /// <summary>
    /// WindowTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowTemplate : Window
    {
        WindowTemplateType WindowTemplateType { get; set; }

        public WindowTemplate(WindowTemplateType windowTemplateType)
        {
            WindowTemplateType = windowTemplateType;
            InitializeComponent();
        }
        public UserControl  UserControl { get; set; }
        public WindowTemplate(WindowTemplateType windowTemplateType,UserControl userControl)
        {
            WindowTemplateType = windowTemplateType;
            InitializeComponent();
            if (UserControl is Calibration calibration && ListConfigs[0].Value is CalibrationParam calibrationParam)
            {
                calibration.DataContext = calibrationParam;
                calibration.CalibrationParam = calibrationParam;
            }

            GridProperty.Children.Clear();
            UserControl = userControl;
            GridProperty.Children.Add(UserControl);
        }

        public new void Show()
        {
            base.Show();
            ListView1.SelectedIndex = 0;
        }


        public ObservableCollection<ListConfig> ListConfigs { get; set; } = new ObservableCollection<ListConfig>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            ListConfigs = new ObservableCollection<ListConfig>();
            ListView1.ItemsSource = ListConfigs;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                
                switch (WindowTemplateType )
                {
                    case WindowTemplateType.AoiParam:
                        PropertyGrid1.SelectedObject = ListConfigs[listView.SelectedIndex].Value;
                        break;
                    case WindowTemplateType.Calibration:
                        if (UserControl is Calibration calibration && ListConfigs[listView.SelectedIndex].Value is CalibrationParam calibrationParam)
                        {
                            calibration.DataContext = calibrationParam;
                            calibration.CalibrationParam = calibrationParam;
                        }
                        break;
                }

            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!TextBox1.Text.IsNullOrEmpty())
            {
                switch (WindowTemplateType)
                {
                    case WindowTemplateType.AoiParam:
                        var obj = new AoiParam();
                        TemplateControl.GetInstance().AoiParams.Add( new KeyValuePair<string, AoiParam>( TextBox1.Text, obj));
                        ListConfigs.Add(new ListConfig() { ID = ListConfigs.Count + 1, Name = TextBox1.Text, Value = obj });

                        break;
                    case WindowTemplateType.Calibration:
                        var obj1 = new CalibrationParam();
                        TemplateControl.GetInstance().CalibrationParams.Add( new KeyValuePair<string, CalibrationParam>(TextBox1.Text, obj1));
                        ListConfigs.Add(new ListConfig() { ID = ListConfigs.Count + 1, Name = TextBox1.Text, Value = obj1 });
                        break;
                }
                TextBox1.Text =string.Empty;
            }
            else
            {
                MessageBox.Show("请输入模板名称", Application.Current.MainWindow.Title, MessageBoxButton.OK);
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

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            TemplateControl.GetInstance().Save(WindowTemplateType);
            this.Close();
        }
    }
}
