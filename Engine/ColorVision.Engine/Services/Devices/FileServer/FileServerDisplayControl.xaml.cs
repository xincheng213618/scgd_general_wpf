using ColorVision.ImageEditor;
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
        public DeviceFileServer Device { get; set; }

        public MQTTFileServer MQTTFileServer { get => Device.DService; }
        public string DisPlayName => Device.Config.Name;

        public ImageView View { get => Device.View; }


        public FileServerDisplayControl(DeviceFileServer deviceFileServer)
        {
            Device = deviceFileServer;
            InitializeComponent();

            Device.DService.OnImageData += Service_OnImageData;

            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void Service_OnImageData(object sender, FileServerDataEvent arg)
        {

        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            this.ContextMenu = Device.ContextMenu;
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

    }
}
