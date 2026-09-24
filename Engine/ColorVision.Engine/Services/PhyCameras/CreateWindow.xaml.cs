using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Themes;
using cvColorVision;
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

            var cameraCandidates = SysResourceDao.Instance.GetAllType(101).Where(a => string.IsNullOrEmpty(a.Value)).ToList();
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
                if (!SysResourceDao.Instance.UseLocal) MessageBox.Show(Properties.Resources.NoCameraToAdd);
            }

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues<TakeImageMode>().Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ConfigEditor.Initialize(CreateConfig);

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

        private void OpenPropertyEditor_Click(object sender, RoutedEventArgs e) => ConfigEditor.OpenPropertyEditor(this);

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CreateConfig.Code))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.CannotCreateCameraWithoutCode, Properties.Resources.CreateDevice, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CreateConfig.CFW.NormalizeChannelCfgsForSave();

            var sysResourceModel = PhyCameraManager.FindPhysicalCameraResource(SysResourceDao.Instance.GetAll(), CreateConfig.Code);
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

            if (SysResourceDao.Instance.Save(sysResourceModel) < 0) return;

            PhyCameraManager.CreatePhysicalCameraFloder(CreateConfig.Code);
            PhyCameraManager.LoadPhyCamera();
            DialogResult = true;
            Close();
        }
    }
}
