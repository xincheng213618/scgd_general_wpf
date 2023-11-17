using ColorVision.Net;
using log4net;
using MQTTMessageLib.FileServer;
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

        private NetFileUtil netFileUtil;

        public FileServerDisplayControl(DeviceFileServer deviceImg)
        {
            DeviceImg = deviceImg;
            InitializeComponent();

            netFileUtil = new NetFileUtil(string.Empty);
            netFileUtil.handler += NetFileUtil_handler;

            DeviceImg.Service.OnImageData += Service_OnImageData;
        }

        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    View.OpenImage(arg.FileData);
                });
            }
            handler?.Close();
        }

        private void Service_OnImageData(object sender, FileServerDataEvent arg)
        {
            switch (arg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(arg.Data));
                    Application.Current.Dispatcher.Invoke(() => {
                        FilesView.ItemsSource = data.Files;
                        FilesView.SelectedIndex = 0;
                    });
                    break;
                case MQTTFileServerEventEnum.Event_File_Upload:
                    DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    FileUpload(pm_up);
                    break;
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    FileDownload(pm_dl);
                    break;
                default:
                    break;
            }
        }

        private void FileUpload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName)) netFileUtil.TaskStartUploadFile(param.IsLocal, param.ServerEndpoint, param.FileName);
        }

        private void FileDownload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName)) netFileUtil.TaskStartDownloadFile(param.IsLocal, param.ServerEndpoint, param.FileName, false);
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
        }

        private void doOpen(string fileName)
        {
            DeviceImg.Service.Open(fileName);

            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
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
               // DeviceImg.Service.UploadFile(Path.GetFileName(openFileDialog.FileName));
                DeviceImg.Service.UploadFile(openFileDialog.FileName);
                handler = PendingBox.Show(Application.Current.MainWindow, "", "上传", true);
                handler.Cancelling += delegate
                {
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
    }
}
