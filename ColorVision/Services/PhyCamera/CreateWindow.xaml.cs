using ColorVision.Common.Extension;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Dao;
using ColorVision.Services.PhyCameras.Configs;
using ColorVision.Settings;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.PhyCameras
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
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CreateConfig = new ConfigPhyCamera
            {
                CameraType = CameraType.LV_Q,
                TakeImageMode = TakeImageMode.Measure_Normal,
                ImageBpp = ImageBpp.bpp8,
                Channel = ImageChannel.One,
            };

            CameraID.ItemsSource = SysResourceDao.Instance.GetAllCameraID();
            DataContext = this;

            var Config = CreateConfig;

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraImageBpp.ItemsSource = from e1 in Enum.GetValues(typeof(ImageBpp)).Cast<ImageBpp>()
                                               select new KeyValuePair<ImageBpp, string>(e1, e1.ToDescription());

            var type = Config.CameraType;

            if (type == CameraType.LV_Q || type == CameraType.LV_H || type == CameraType.LV_MIL_CL || type == CameraType.MIL_CL)
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  where e1 != ImageChannel.Three
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
            }
            else if (type == CameraType.CV_Q || type == CameraType.BV_Q || type == CameraType.BV_H)
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  where e1 != ImageChannel.One
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
            }
            else
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());


            };


            ComboxCameraType.SelectionChanged += (s, e) =>
            {
                if (ComboxCameraType.SelectedValue is CameraType type)
                {
                    if (type == CameraType.LV_Q || type == CameraType.LV_H || type == CameraType.LV_MIL_CL || type == CameraType.MIL_CL)
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          where e1 != ImageChannel.Three
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                        ComboxCameraChannel.SelectedValue = ImageChannel.One;
                    }
                    else if (type == CameraType.CV_Q || type == CameraType.BV_Q || type == CameraType.BV_H)
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          where e1 != ImageChannel.One
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                        ComboxCameraChannel.SelectedValue = ImageChannel.Three;
                    }

                    else
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                    };
                }

            };
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsInvalidPath(CreateConfig.CameraID, "相机ID") )
                return;

            SysResourceModel sysResource = new SysResourceModel(CreateConfig.CameraID, CreateConfig.CameraID,101 ,ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            sysResource.Value = JsonConvert.SerializeObject(CreateConfig);
            SysResourceDao.Instance.Save(sysResource);

            PhyCamera phyCamera = new PhyCamera(sysResource);
            PhyCameraManager.PhyCameras.Add(phyCamera);
            Close();
        }
    }
}
