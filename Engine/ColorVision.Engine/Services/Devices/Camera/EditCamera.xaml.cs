using ColorVision.Common.MVVM;
using ColorVision.Engine.Cache;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Devices.Camera
{
    /// <summary>
    /// EditCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditCamera : Window
    {
        private JObject initialDeviceConfig = null!;
        private readonly bool initialUseLocalCamera;
        private readonly DisplayCameraConfig displayDraft;

        public DeviceCamera DeviceCamera { get; set; }

        public MQTTCamera Service { get => DeviceCamera.DService; }

        public ConfigCamera EditConfig { get; set; }

        public EditCamera(DeviceCamera mQTTDeviceCamera)
        {
            DeviceCamera = mQTTDeviceCamera;
            initialUseLocalCamera = DeviceCamera.DisplayConfig.UseLocalCamera;
            displayDraft = new DisplayCameraConfig { UseLocalCamera = initialUseLocalCamera };
            InitializeComponent();
            this.ApplyCaption();
        }

        public List<PhyCamera> PhyCameras { get; set; } = new List<PhyCamera>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            var phyCameraManager = PhyCameraManager.GetInstance();
            var deviceCamera = DeviceCamera;
            PhyCameras = phyCameraManager.PhyCameras
                .Where(p => p.DeviceCamera == null || p.DeviceCamera.Name == deviceCamera.Name)
                .ToList();

            CameraPhyID.ItemsSource = PhyCameras;

            CameraPhyID.SelectedItem = phyCameraManager.GetPhyCamera(deviceCamera.Config.CameraCode);
            TextSearch.SetTextPath(CameraPhyID, nameof(PhyCamera.Code));
            CameraPhyID.ItemTemplate = TextSNPropertiesEditor.CreateCameraSnItemTemplate();
            CameraPhyID.ItemContainerStyle = TextSNPropertiesEditor.CreateCameraSnItemContainerStyle(CameraPhyID.ItemTemplate);

            EditConfig = DeviceCamera.Config.Clone();
            initialDeviceConfig = JObject.FromObject(EditConfig);
            DataContext = DeviceCamera;
            EditContent.DataContext = EditConfig;

            LocalCameraField.Content = PropertyEditorHelper.GenProperties(displayDraft, nameof(DisplayCameraConfig.UseLocalCamera));
            FilePathField.Content = PropertyEditorHelper.GenProperties(EditConfig.FileServerCfg, nameof(FileServerCfg.DataBasePath));
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            try
            {
                DeviceCamera.DemandConfigurationAccess();
                if (!JToken.DeepEquals(initialDeviceConfig, JObject.FromObject(EditConfig)))
                {
                    DeviceCamera.EnsurePhyCameraBindingCanChange(EditConfig.CameraCode);
                    DeviceCamera.CommitConfiguration(() => EditConfig.CopyTo(DeviceCamera.Config));
                }
                // The live preference setter notifies the backend and persists the display configuration.
                if (displayDraft.UseLocalCamera != initialUseLocalCamera)
                    DeviceCamera.DisplayConfig.UseLocalCamera = displayDraft.UseLocalCamera;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MQTTServiceBase.log.Error("保存相机配置失败。", ex);
                StatusText.Text = ex.Message;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        private void CameraPhyID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditConfig == null || sender is not ComboBox { SelectedItem: PhyCamera phyCamera }) return;

            EditConfig.CameraCode = phyCamera.Code;
            phyCamera.Config.ApplyTo(EditConfig, includeCameraId: true, includeCameraType: false);
        }
    }
}
