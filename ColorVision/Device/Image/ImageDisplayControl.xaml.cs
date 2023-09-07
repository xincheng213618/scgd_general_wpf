using ColorVision.Device.SMU;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


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

            DeviceImg.Service.OnImageData += Service_OnImageData;
        }

        private void Service_OnImageData(object sender, ImageDataEventArgs arg)
        {
            switch (arg.EventName)
            {
                case "GetAllFiles":
                    List<string> data = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(arg.Data));
                    Application.Current.Dispatcher.Invoke(() => {
                        FilesView.ItemsSource = data; 
                    });
                    break;
                case "":
                    break;
                default:
                    break;
            }
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

        IPendingHandler handler { get; set; }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            doOpen(FilesView.Text);
        }

        private void doOpen(string fileName)
        {
            DeviceImg.Service.Open(fileName);
            DealerSocket client = new DealerSocket(DeviceImg.Config.Endpoint);
            Task t = new(() => { Task_Start(client); });
            t.Start();

            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                client.Close();
                client.Dispose();
                handler?.Close();
            };
        }

        private void Task_Start(DealerSocket client)
        {
            try
            {
                List<byte[]> data = client.ReceiveMultipartBytes();
                if (data.Count == 1)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        View.OpenImage(data[0]);
                    });
                }
                client.Close();
                client.Dispose();
            }catch (Exception ex)
            {
                client.Close();
                client.Dispose();
            }

            handler?.Close();
        }

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            DeviceImg.Service.GetAllFiles();
        }
    }
}
