#pragma warning disable CA1863,CS8604
using ColorVision.Database;
using ColorVision.Engine.Services.Devices;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Terminal
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTerminal : Window
    {
        public TerminalService TerminalService { get; set; }
        public CreateTerminal(TerminalService terminalService)
        {
            TerminalService = terminalService;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CreateCode.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");
            CreateName.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");

            DataContext = TerminalService;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SysResourceModel saveDevConfigInfo(DeviceServiceConfig deviceConfig, SysResourceModel sysResource)
            {
                sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
                int pkId = SysResourceDao.Instance.SaveAndReturnId(sysResource);
                sysResource.Id = pkId;
                return sysResource;
            }


            var deviceS = ServiceManager.GetInstance().DeviceServices.FirstOrDefault(x => x.Code == CreateCode.Text);
            if (deviceS != null)
            {
                MessageBox1.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.DeviceIdentifierAlreadyExists,"ColorVision");
                return;
            }

            if (!DeviceServiceFactoryRegistry.TryGetFactory(TerminalService.ServiceType, out IDeviceServiceFactory? deviceServiceFactory) || deviceServiceFactory == null)
            {
                MessageBox1.Show(WindowHelpers.GetActiveWindow(), string.Format(Properties.Resources.UnsupportedDeviceTypeCreation, TerminalService.ServiceType), "ColorVision");
                return;
            }


            SysResourceModel sysResource = new SysResourceModel();
            sysResource.Name = CreateName.Text;
            sysResource.Code = CreateCode.Text;
            sysResource.Type = TerminalService.SysResourceModel.Type;
            sysResource.Pid = TerminalService.SysResourceModel.Id;


            DeviceServiceCreateContext createContext = new(
                CreateCode.Text,
                CreateName.Text,
                TerminalService.Config.SendTopic,
                TerminalService.Config.SubscribeTopic);
            DeviceServiceConfig deviceConfig = deviceServiceFactory.CreateConfig(createContext);
            SysResourceModel sysDevModel = saveDevConfigInfo(deviceConfig, sysResource);
            DeviceService deviceService = deviceServiceFactory.CreateService(sysDevModel);

            TerminalService.AddChild(deviceService);
            ServiceManager.GetInstance().DeviceServices.Add(deviceService);
            if (!SysResourceDao.IsLocalId(sysDevModel.Id))
                RC.MqttRCService.GetInstance().RestartServices(TerminalService.ServiceType.ToString(), TerminalService.SysResourceModel.Code, sysDevModel.Code);
            ServiceManager.GetInstance().GenDeviceDisplayControl();
            Close();

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
