using ColorVision.Device.SMU;
using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Device.Image
{
    /// <summary>
    /// ImageDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class ImageDisplayControl : UserControl
    {
        public DeviceImage DeviceImg { get; set; }

        public ImageView View { get => DeviceImg.View; }

        public ImageDisplayControl(DeviceImage deviceImg)
        {
            DeviceImg = deviceImg;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DeviceImg.Service;


            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>("独立窗口", -2));
                KeyValues.Add(new KeyValuePair<string, int>("隐藏", -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            };
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    View.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);
                }
            };


            ViewGridManager.GetInstance().AddView(View);
            if (ViewGridManager.GetInstance().ViewMax > 4 || ViewGridManager.GetInstance().ViewMax == 3)
            {
                ViewGridManager.GetInstance().SetViewNum(-1);
            }


        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            DeviceImg.Service.Open("F:\\img\\0524MTF-H.tif");
        }
    }
}
