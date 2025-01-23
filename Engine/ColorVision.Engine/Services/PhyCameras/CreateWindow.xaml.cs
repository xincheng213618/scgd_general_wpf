using ColorVision.Common.Utilities;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Engine.Services.RC;
using ColorVision.Themes;
using cvColorVision;
using CVCommCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// CreateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CreateWindow : Window
    {
        public ConfigPhyCamera CreateConfig { get; set; }

        public PhyCameraManager PhyCameraManager { get; set; }
        public CreateWindow(PhyCameraManager phyCameraManager)
        {
            PhyCameraManager = phyCameraManager;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = this;


            this.CreateConfig = new ConfigPhyCamera
            {
                TakeImageMode = TakeImageMode.Measure_Normal,
                ImageBpp = ImageBpp.bpp8,
                Channel = ImageChannel.One,
            };
            var list = SysResourceDao.Instance.GetAllEmptyCameraId();

            if (list != null)
            {
                CameraCode.ItemsSource = list;
                CameraCode.DisplayMemberPath = "Code";
                CameraCode.SelectedValuePath = "Name";
                CreateConfig.Code = list[0].Code ?? string.Empty;
                CameraCode.SelectionChanged += (s, e) =>
                {
                    if (CameraCode.SelectedIndex >= 0)
                    {
                        var model = CameraLicenseDao.Instance.GetByMAC(list[CameraCode.SelectedIndex].Code ?? string.Empty)?.Model;
                        if (model != null)
                        {
                            DeviceName.Text = model;
                            if (model.Contains("BV", StringComparison.OrdinalIgnoreCase))
                            {
                                CreateConfig.CameraMode = CameraMode.BV_MODE;
                                CreateConfig.Channel = ImageChannel.Three;
                            }
                            if (model.Contains("LV", StringComparison.OrdinalIgnoreCase))
                            {
                                CreateConfig.CameraMode = CameraMode.LV_MODE;
                                CreateConfig.Channel = ImageChannel.One;
                            }
                            if (model.Contains("CV", StringComparison.OrdinalIgnoreCase))
                            {
                                CreateConfig.CameraMode = CameraMode.CV_MODE;
                                CreateConfig.Channel = ImageChannel.Three;
                            }
                        }

                    }
                };
                CameraCode.SelectedIndex = 0;


            }
            else
            {
                MessageBox.Show("找不到可以添加的相机");
            }

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraImageBpp.ItemsSource = from e1 in Enum.GetValues(typeof(ImageBpp)).Cast<ImageBpp>()
                                               select new KeyValuePair<ImageBpp, string>(e1, e1.ToDescription());


            ComboxCameraModel.ItemsSource = from e1 in Enum.GetValues(typeof(CameraModel)).Cast<CameraModel>()
                                            select new KeyValuePair<CameraModel, string>(e1, e1.ToDescription());

            ComboxCameraMode.ItemsSource = from e1 in Enum.GetValues(typeof(CameraMode)).Cast<CameraMode>()
                                           select new KeyValuePair<CameraMode, string>(e1, e1.ToDescription());

            while (CreateConfig.CFW.ChannelCfgs.Count < 9)
            {
                CreateConfig.CFW.ChannelCfgs.Add(new Services.PhyCameras.Configs.ChannelCfg());
            }

            ComboxCameraMode.SelectionChanged += (s, e) =>
            {

                if (CreateConfig.CameraMode == CameraMode.LV_MODE)
                {
                    ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                      where e1 != ImageChannel.Three
                                                      select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                }
                else if (CreateConfig.CameraMode == CameraMode.BV_MODE)
                {
                    ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                      where e1 != ImageChannel.One
                                                      select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                }
                else
                {
                    ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                      select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                }

            };


            List<int> BaudRates = new() { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            List<string> Serials = new() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10" };

            TextBaudRate.ItemsSource = BaudRates;


            TextSerial.ItemsSource = Serials;


            ComboxMotorType.ItemsSource = from e1 in Enum.GetValues(typeof(FOCUS_COMMUN)).Cast<FOCUS_COMMUN>()
                                          select new KeyValuePair<FOCUS_COMMUN, string>(e1, e1.ToString());
            int index = 0;
            ComboxMotorType.SelectionChanged += (s, e) =>
            {
                if (index++ < 1)
                    return;
                switch (CreateConfig.MotorConfig.eFOCUSCOMMUN)
                {
                    case FOCUS_COMMUN.VID_SERIAL:
                        CreateConfig.MotorConfig.BaudRate = 115200;
                        break;
                    case FOCUS_COMMUN.CANON_SERIAL:
                        CreateConfig.MotorConfig.BaudRate = 38400;
                        break;
                    case FOCUS_COMMUN.NED_SERIAL:
                        CreateConfig.MotorConfig.BaudRate = 115200;
                        break;
                    case FOCUS_COMMUN.LONGFOOT_SERIAL:
                        CreateConfig.MotorConfig.BaudRate = 115200;
                        break;
                    default:
                        break;
                }
            };

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (CreateConfig.CFW.CFWNum > 1)
            {
                CreateConfig.CFW.ChannelCfgs[3].Chtype = CreateConfig.CFW.ChannelCfgs[0].Chtype;
                CreateConfig.CFW.ChannelCfgs[4].Chtype = CreateConfig.CFW.ChannelCfgs[1].Chtype;
                CreateConfig.CFW.ChannelCfgs[5].Chtype = CreateConfig.CFW.ChannelCfgs[2].Chtype;
            }
            if (CreateConfig.CFW.CFWNum > 2)
            {
                CreateConfig.CFW.ChannelCfgs[6].Chtype = CreateConfig.CFW.ChannelCfgs[0].Chtype;
                CreateConfig.CFW.ChannelCfgs[7].Chtype = CreateConfig.CFW.ChannelCfgs[1].Chtype;
                CreateConfig.CFW.ChannelCfgs[8].Chtype = CreateConfig.CFW.ChannelCfgs[2].Chtype;
            }
            if (CreateConfig.CFW.CFWNum == 1)
                CreateConfig.CFW.ChannelCfgs = CreateConfig.CFW.ChannelCfgs.GetRange(0, 3);
            if (CreateConfig.CFW.CFWNum == 2)
                CreateConfig.CFW.ChannelCfgs = CreateConfig.CFW.ChannelCfgs.GetRange(0, 6);
            if (CreateConfig.CFW.CFWNum == 3)
                CreateConfig.CFW.ChannelCfgs = CreateConfig.CFW.ChannelCfgs.GetRange(0, 9);


            SysResourceModel? sysResourceModel = SysResourceDao.Instance.GetByCode(CreateConfig.Code);
            if (sysResourceModel == null)
                sysResourceModel = new SysResourceModel(CreateConfig.CameraID, CreateConfig.Code, (int)PhysicalResourceType.PhyCamera, UserConfig.Instance.TenantId);

            sysResourceModel.Value = JsonConvert.SerializeObject(CreateConfig);
            int ret = SysResourceDao.Instance.Save(sysResourceModel);
            if (ret < 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "不允许创建没有Code的相机", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            RCFileUpload.GetInstance().CreatePhysicalCameraFloder(CreateConfig.Code);
            PhyCameraManager.LoadPhyCamera();
            Close();
        }

        private void FileBasePath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为相机路径选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                CreateConfig.FileServerCfg.FileBasePath = dialog.SelectedPath;
            }
        }
    }
}
