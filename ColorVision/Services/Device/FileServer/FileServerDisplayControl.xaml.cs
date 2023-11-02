using log4net;
using MQTTMessageLib.FileServer;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Device.FileServer
{
    /// <summary>
    /// ImageDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class FileServerDisplayControl : UserControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(FileServerDisplayControl));
        public DeviceFileServer DeviceImg { get; set; }

        public ImageView View { get => DeviceImg.View; }

        public FileServerDisplayControl(DeviceFileServer deviceImg)
        {
            DeviceImg = deviceImg;
            InitializeComponent();

            DeviceImg.Service.OnImageData += Service_OnImageData;
        }

        private void Service_OnImageData(object sender, FileServerDataEvent arg)
        {
            switch (arg.EventName)
            {
                case MQTTFileServerEventEnum.Event_GetAllFiles:
                    List<string> data = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(arg.Data));
                    Application.Current.Dispatcher.Invoke(() => {
                        FilesView.ItemsSource = data;
                        FilesView.SelectedIndex = 0;
                    });
                    break;
                case MQTTFileServerEventEnum.Event_UploadFile:
                    handler?.Close();
                    break;
                default:
                    break;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DeviceImg;

            ViewMaxChangedEvent(ViewGridManager.GetInstance().ViewMax);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += ViewMaxChangedEvent;

            void ViewMaxChangedEvent(int max)
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < max; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            }
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
            View.View.ViewIndex = -1;

            ViewGridManager.GetInstance().AddView(View);
            if (ViewGridManager.GetInstance().ViewMax > 4 || ViewGridManager.GetInstance().ViewMax == 3)
            {
                ViewGridManager.GetInstance().SetViewNum(-1);
            }

            View.View.ViewIndex = -1;






            Task t = new(() => { DeviceImg.Service.GetAllFiles(); });
            t.Start();
        }

        IPendingHandler handler { get; set; }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            doOpen(FilesView.Text);
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        View.OpenCVCIE(@"F:\img\cvcie\20230322142727_1_src.cvcie");
        //        View.OpenCVCIE(@"F:\img\cvcie\20230322142727_Y_src.cvcie");
        //        View.OpenCVCIE(@"F:\img\cvcie\0524MTF-H.cvcie");
        //        View.OpenCVCIE(@"F:\img\cvcie\ttt.cvcie");
        //    });
        }

        private void doOpen(string fileName)
        {
            DeviceImg.Service.Open(fileName);
            Task t = new(() => { Task_Start(); });
            t.Start();

            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
        }

        private void Task_Start()
        {
            DealerSocket client = null;
            try
            {
                client = new DealerSocket(DeviceImg.Config.Endpoint);
                List<byte[]> data = client.ReceiveMultipartBytes();
                if (data.Count == 1)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        View.OpenImage(data[0]);
                    });
                }
                client?.Close();
                client?.Dispose();
            }catch (Exception ex)
            {
                logger.Error(ex);
                client?.Close();
                client?.Dispose();
            }

            handler?.Close();
        }

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            DeviceImg.Service.GetAllFiles();
        }

        private void Button_Click_Upload(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "TIF|*.tif||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DeviceImg.Service.UploadFile(Path.GetFileName(openFileDialog.FileName));
                Task t = new(() => { Task_StartUpload(openFileDialog.FileName); });
                t.Start();

                handler = PendingBox.Show(Application.Current.MainWindow, "", "上传", true);
                handler.Cancelling += delegate
                {
                    t.Dispose();
                    handler?.Close();
                };
            }
        }

        private static byte[] readFile(string path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            //获取文件长度
            long length = fileStream.Length;
            byte[] bytes = new byte[length];
            //读取文件中的内容并保存到字节数组中
            binaryReader.Read(bytes, 0, bytes.Length);
            return bytes;
        }

        private void Task_StartUpload(string fileName)
        {
            DealerSocket client = new DealerSocket(DeviceImg.Config.Endpoint);
            var message = new List<byte[]>();
            message.Add(readFile(fileName));
            client.TrySendMultipartBytes(TimeSpan.FromMilliseconds(3000), message);
            client.Close();
            client.Dispose();
        }
    }
}
