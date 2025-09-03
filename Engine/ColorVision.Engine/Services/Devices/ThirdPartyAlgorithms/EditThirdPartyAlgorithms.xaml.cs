using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    /// <summary>
    /// EditAlgorithm.xaml 的交互逻辑
    /// </summary>
    public partial class EditThirdPartyAlgorithms : Window
    {
        public DeviceThirdPartyAlgorithms Device { get; set; }
        public ConfigThirdPartyAlgorithms EditConfig { get; set; }

        public EditThirdPartyAlgorithms(DeviceThirdPartyAlgorithms deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            InitializeComponent();
            this.ApplyCaption();
        }
             private void Window_Initialized(object sender, System.EventArgs e)
        {
            DataContext = Device;
            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;

            CameraPhyID.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;
            CameraPhyID.DisplayMemberPath = "Code";

            CobBingCode.ItemsSource = SysResourceTpaDLLDao.Instance.GetAll();
            CobBingCode.DisplayMemberPath = "Code";

        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Close();
        }


    }
}
