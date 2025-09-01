using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.FileIO;
using ColorVision.UI;
using log4net;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;



namespace ColorVision.Engine.Services.Devices.FileServer
{
    /// <summary>
    /// ImageDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class FileServerDisplayControl : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(FileServerDisplayControl));
        public DeviceFileServer DeviceFileServer { get; set; }

        public MQTTFileServer MQTTFileServer { get => DeviceFileServer.DService; }
        public string DisPlayName => DeviceFileServer.Config.Name;

        public ImageView View { get => DeviceFileServer.View; }

        private NetFileUtil netFileUtil;

        public FileServerDisplayControl(DeviceFileServer deviceFileServer)
        {
            DeviceFileServer = deviceFileServer;
            InitializeComponent();

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;

            DeviceFileServer.DService.OnImageData += Service_OnImageData;

            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    View.OpenImage(arg.FileData.ToWriteableBitmap());
                });
            }
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
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    FileDownload(pm_dl);
                    break;
                default:
                    break;
            }
        }


        private void FileDownload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName)) netFileUtil.TaskStartDownloadFile(param.IsLocal, param.ServerEndpoint, param.FileName, (CVType)FileExtType.Src);
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DeviceFileServer;
            Task.Run(() => { MQTTFileServer.GetAllFiles(); });
        }


        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            MQTTFileServer.Open(FilesView.Text);
        }

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            MQTTFileServer.GetAllFiles();
        }

        private void Button_Click_Upload(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "TIF|*.tif||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MQTTFileServer.UploadFile(openFileDialog.FileName);
            }
        }

        private static byte[] ReadFile(string path)
        {
            FileStream fileStream = new(path, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new(fileStream);
            //获取文件长度
            long length = fileStream.Length;
            byte[] bytes = new byte[length];
            //读取文件中的内容并保存到字节数组中
            binaryReader.Read(bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
