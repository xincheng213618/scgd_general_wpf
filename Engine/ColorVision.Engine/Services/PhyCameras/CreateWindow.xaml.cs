using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Themes;
using ColorVision.UI;
using cvColorVision;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
            this.CreateConfig = new ConfigPhyCamera
            {
                TakeImageMode = TakeImageMode.Measure_Normal,
                ImageBpp = ImageBpp.bpp16,
                Channel = ImageChannel.One,
                CameraMode = CameraMode.BV_MODE,
                CFW = new CFWPORT() { BaudRate = 9600, CFWNum = 1, ChannelCfgs = new List<Configs.ChannelCfg>() },

            };
            var list = MySqlControl.GetInstance().DB.Queryable<SysResourceModel>().Where(a => a.Type == 101 && SqlFunc.IsNullOrEmpty(a.Value)).ToList();

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

            var ImageChannelTypeList = new[]{
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_X, "Channel_R"),
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Y, "Channel_G"),
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Z, "Channel_B")
            };
            chType1.ItemsSource = ImageChannelTypeList;
            chType2.ItemsSource = ImageChannelTypeList;
            chType3.ItemsSource = ImageChannelTypeList;


            Dictionary<ImageChannelType, ComboBox> keyValuePairs = new()
            {
                { ImageChannelType.Gray_X, chType1 },
                { ImageChannelType.Gray_Y, chType2 },
                { ImageChannelType.Gray_Z, chType3 }
            };

            if (CreateConfig.CFW.ChannelCfgs.Count == 0)
            {
                CreateConfig.CFW.ChannelCfgs.Add(new() { Cfwport = 0, Chtype = ImageChannelType.Gray_Y });
                CreateConfig.CFW.ChannelCfgs.Add(new() { Cfwport = 1, Chtype = ImageChannelType.Gray_X });
                CreateConfig.CFW.ChannelCfgs.Add(new() { Cfwport = 2, Chtype = ImageChannelType.Gray_Z });
            }
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
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(CreateConfig.CameraCfg));
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(CreateConfig.MotorConfig));
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(CreateConfig.FileServerCfg));

            DataContext = this;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CreateConfig.Code))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "不允许创建没有Code的相机", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

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


            var sysResourceModel = MySqlControl.GetInstance().DB.Queryable<SysResourceModel>().Where(x => x.Code == CreateConfig.Code) .First();
            // 不存在则新建
            if (sysResourceModel == null)
            {
                sysResourceModel = new SysResourceModel
                {
                    Name = CreateConfig.CameraID,
                    Code = CreateConfig.Code,
                    Type = 101,
                    TenantId = UserConfig.Instance.TenantId,
                };
            }

            // 赋值并保存
            sysResourceModel.Value = JsonConvert.SerializeObject(CreateConfig);

            // 推荐用 InsertOrUpdate（SqlSugar5+），否则判断主键再决定 insert/update
            int ret;
            if (sysResourceModel.Id > 0)
            {
                ret = MySqlControl.GetInstance().DB.Updateable(sysResourceModel).ExecuteCommand();
            }
            else
            {
                ret = MySqlControl.GetInstance().DB.Insertable(sysResourceModel).ExecuteCommand();
            }

            PhyCameraManager.CreatePhysicalCameraFloder(CreateConfig.Code);
            PhyCameraManager.LoadPhyCamera();
            Close();
        }
    }
}
