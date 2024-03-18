using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Terminal;
using ColorVision.Settings;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Windows;

namespace ColorVision.Services.Devices.Camera
{
    public class TerminalCamera : TerminalService
    {
        public TerminalCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            OpenCreateWindowCommand = new RelayCommand((s) => OpenCreateWindow());
        }


        
        public ConfigCamera CreateConfig { get; set; }

        public override void OpenCreateWindow()
        {
            int fromPort = (Math.Abs(new Random().Next()) % 99 + 6800);

            CreateConfig = new ConfigCamera
            {
                CameraType = CameraType.LV_Q,
                TakeImageMode = TakeImageMode.Measure_Normal,
                ImageBpp = ImageBpp.bpp8,
                Channel = ImageChannel.One,
                FileServerCfg = new FileServerCfg()
                {
                    Endpoint = "127.0.0.1",
                    PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                    DataBasePath = "D:\\CVTest",
                },
                VideoConfig = new Video.CameraVideoConfig()
                {
                    Host = "127.0.0.1",
                    Port = (Math.Abs(new Random().Next()) % 99 + 9000),
                }
            };
            CreateConfig.SendTopic = Config.SendTopic;
            CreateConfig.SubscribeTopic = Config.SubscribeTopic;

            CreateWindow createWindow = new CreateWindow(this);
            createWindow.Owner = Window.GetWindow(Application.Current.MainWindow);
            createWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            CreateDeviceOver += (s, e) =>
            {
                createWindow.Close();
            }; ;
            createWindow.ShowDialog();
        }


        public override void Create()
        {
            if ( !ServicesHelper.IsInvalidPath(CreatCode, "资源标识") ||!ServicesHelper.IsInvalidPath(CreatName, "资源名称") )
                return;

            if (ServicesCodes.Contains(CreatCode))
            {
                MessageBox.Show("设备标识已存在,不允许重复添加");
                return;
            }
            SysResourceModel sysResource = new SysResourceModel(CreatName, CreatCode, SysResourceModel.Type, SysResourceModel.Id, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            CreateConfig.Id = CreatCode;
            CreateConfig.Name = CreatName;
            sysResource.Value = JsonConvert.SerializeObject(CreateConfig);
            ServiceManager.GetInstance().VSysResourceDao.Save(sysResource);
            int pkId = sysResource.PKId;
            if (pkId > 0 && ServiceManager.GetInstance().VSysDeviceDao.GetById(pkId) is SysDeviceModel model)
            {
                if (MQTTServiceTerminalBase is MQTTTerminalCamera cameraService)
                {

                    var deviceService = new DeviceCamera(model, cameraService);
                    AddChild(deviceService);
                    ServiceManager.GetInstance().DeviceServices.Add(deviceService);

                }
                CreateDeviceOver?.Invoke(this,new EventArgs());
            }
            else
            {
                MessageBox.Show("创建失败，请检查网络或Mysql配置情况");
            }
        }
    }
}
