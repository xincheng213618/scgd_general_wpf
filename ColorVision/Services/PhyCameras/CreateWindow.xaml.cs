﻿using ColorVision.Common.Extension;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.PhyCameras.Configs;
using ColorVision.Services.Type;
using ColorVision.Settings;
using cvColorVision;
using CVCommCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;

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

            CameraID.ItemsSource = SysResourceDao.Instance.GetAllEmptyCameraId();
            CameraID.DisplayMemberPath = "Name";
            CameraID.SelectedValuePath = "Code";
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
            SysResourceModel? sysResourceModel = SysResourceDao.Instance.GetByCode(CreateConfig.Code);
            if (sysResourceModel == null)
                sysResourceModel = new SysResourceModel(CreateConfig.CameraID, CreateConfig.Code, (int)PhysicalResourceType.PhyCamera, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);

            sysResourceModel.Value = JsonConvert.SerializeObject(CreateConfig);
            int ret =  SysResourceDao.Instance.Save(sysResourceModel);
            if (ret < 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),"不允许创建没有Code的相机", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);    
            }
            PhyCameraManager.LoadPhyCamera();
            Close();
        }
    }
}