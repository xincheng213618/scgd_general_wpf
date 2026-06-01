using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
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
        private readonly string? _InitialCameraCode;
        private readonly string? _InitialCameraId;
        private readonly CameraModel? _InitialCameraModel;

        public CreateWindow(PhyCameraManager phyCameraManager)
            : this(phyCameraManager, null, null, null)
        {
        }

        public CreateWindow(PhyCameraManager phyCameraManager, string cameraCode, string cameraId, CameraModel cameraModel)
            : this(phyCameraManager, cameraCode, cameraId, (CameraModel?)cameraModel)
        {
        }

        private CreateWindow(PhyCameraManager phyCameraManager, string? cameraCode, string? cameraId, CameraModel? cameraModel)
        {
            PhyCameraManager = phyCameraManager;
            _InitialCameraCode = cameraCode;
            _InitialCameraId = cameraId;
            _InitialCameraModel = cameraModel;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.CreateConfig = CreateDefaultConfig();
            ApplyInitialCamera();

            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            var cameraCandidates = Db.Queryable<SysResourceModel>().Where(a => a.Type == 101 && SqlFunc.IsNullOrEmpty(a.Value)).ToList();
            if (!string.IsNullOrWhiteSpace(_InitialCameraCode) && !cameraCandidates.Any(a => string.Equals(a.Code, _InitialCameraCode, StringComparison.OrdinalIgnoreCase)))
            {
                cameraCandidates.Insert(0, new SysResourceModel
                {
                    Code = _InitialCameraCode,
                    Name = _InitialCameraId,
                    Type = 101,
                    TenantId = 0
                });
            }

            if (cameraCandidates.Count > 0)
            {
                CameraCode.ItemsSource = cameraCandidates;
                CameraCode.DisplayMemberPath = "Code";
                CameraCode.SelectedValuePath = "Name";
                if (string.IsNullOrWhiteSpace(CreateConfig.Code))
                {
                    CreateConfig.Code = cameraCandidates[0].Code ?? string.Empty;
                }
                CameraCode.SelectionChanged += (s, e) =>
                {
                    if (CameraCode.SelectedIndex >= 0)
                    {
                        var model = PhyLicenseDao.Instance.GetByMAC(cameraCandidates[CameraCode.SelectedIndex].Code ?? string.Empty)?.Model;
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
                int selectedIndex = cameraCandidates.FindIndex(a => string.Equals(a.Code, CreateConfig.Code, StringComparison.OrdinalIgnoreCase));
                CameraCode.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;


            }
            else
            {
                MessageBox.Show(Properties.Resources.NoCameraToAdd);
            }

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues<TakeImageMode>().Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraImageBpp.ItemsSource = from e1 in Enum.GetValues<ImageBpp>().Cast<ImageBpp>()
                                               select new KeyValuePair<ImageBpp, string>(e1, e1.ToDescription());


            ComboxCameraModel.ItemsSource = from e1 in Enum.GetValues<CameraModel>().Cast<CameraModel>()
                                            select new KeyValuePair<CameraModel, string>(e1, e1.ToDescription());

            ComboxCameraMode.ItemsSource = from e1 in Enum.GetValues<CameraMode>().Cast<CameraMode>()
                                           select new KeyValuePair<CameraMode, string>(e1, e1.ToDescription());

            var ImageChannelTypeList = new[]{
                  new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_X, Properties.Resources.ChannelR),
                  new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Y, Properties.Resources.ChannelG),
                  new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Z, Properties.Resources.ChannelB)
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
                    ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues<ImageChannel>().Cast<ImageChannel>()
                                                      where e1 != ImageChannel.Three
                                                      select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                }
                else if (CreateConfig.CameraMode == CameraMode.BV_MODE)
                {
                    ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues<ImageChannel>().Cast<ImageChannel>()
                                                      where e1 != ImageChannel.One
                                                      select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                }
                else
                {
                    ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues<ImageChannel>().Cast<ImageChannel>()
                                                      select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                }

            };
            GeneratedConfigPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(CreateConfig.CameraCfg));
            GeneratedConfigPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(CreateConfig.MotorConfig));
            GeneratedConfigPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(CreateConfig.FileServerCfg));

            DataContext = this;
        }

        private static ConfigPhyCamera CreateDefaultConfig()
        {
            return new ConfigPhyCamera
            {
                TakeImageMode = TakeImageMode.Measure_Normal,
                ImageBpp = ImageBpp.bpp16,
                Channel = ImageChannel.One,
                CameraMode = CameraMode.BV_MODE,
                CFW = new CFWPORT() { BaudRate = 9600, CFWNum = 1, ChannelCfgs = new List<Configs.ChannelCfg>() },
            };
        }

        private void ApplyInitialCamera()
        {
            if (!string.IsNullOrWhiteSpace(_InitialCameraCode))
            {
                CreateConfig.Code = _InitialCameraCode;
            }

            if (!string.IsNullOrWhiteSpace(_InitialCameraId))
            {
                CreateConfig.CameraID = _InitialCameraId;
            }

            if (_InitialCameraModel.HasValue)
            {
                CreateConfig.CameraModel = _InitialCameraModel.Value;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CreateConfig.Code))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.CannotCreateCameraWithoutCode, Properties.Resources.CreateDevice, MessageBoxButton.OK, MessageBoxImage.Error);
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

            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            var sysResourceModel = Db.Queryable<SysResourceModel>().Where(x => x.Code == CreateConfig.Code) .First();
            // 不存在则新建
            if (sysResourceModel == null)
            {
                sysResourceModel = new SysResourceModel
                {
                    Name = CreateConfig.CameraID,
                    Code = CreateConfig.Code,
                    Type = 101,
                    TenantId = 0,
                };
            }

            // 赋值并保存
            sysResourceModel.Value = JsonConvert.SerializeObject(CreateConfig);

            // 推荐用 InsertOrUpdate（SqlSugar5+），否则判断主键再决定 insert/update
            int ret;
            if (sysResourceModel.Id > 0)
            {
                ret = Db.Updateable(sysResourceModel).ExecuteCommand();
            }
            else
            {
                ret = Db.Insertable(sysResourceModel).ExecuteCommand();
            }

            PhyCameraManager.CreatePhysicalCameraFloder(CreateConfig.Code);
            PhyCameraManager.LoadPhyCamera();
            DialogResult = true;
            Close();
        }
    }
}
