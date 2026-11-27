using ColorVision.Common.MVVM;
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
        private readonly CameraAcquisitionSettingsDraft acquisitionDraft;
        private readonly DeviceConfigurationTarget openingTarget;

        public DeviceCamera DeviceCamera { get; set; }

        public MQTTCamera Service { get => DeviceCamera.DService; }

        public ConfigCamera EditConfig { get; set; }

        public EditCamera(DeviceCamera mQTTDeviceCamera)
        {
            DeviceCamera = mQTTDeviceCamera;
            openingTarget = mQTTDeviceCamera.CaptureConfigurationTarget();
            acquisitionDraft = new CameraAcquisitionSettingsDraft(DeviceCamera.DisplayConfig);
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

            foreach (string name in CameraAcquisitionSettingsDraft.EditableProperties)
            {
                var field = PropertyEditorHelper.GenProperties(acquisitionDraft.Settings, name, Properties.Resources.ResourceManager);
                field.Margin = new Thickness(0, 3, 0, 5);
                field.MinHeight = 28;
                AcquisitionFields.Children.Add(field);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            try
            {
                DeviceCamera.DemandConfigurationAccess();
                if (!DeviceCamera.TryAdmitConfigurationEdit(openingTarget, this)) return;
                if (!JToken.DeepEquals(initialDeviceConfig, JObject.FromObject(EditConfig)))
                {
                    DeviceCamera.EnsurePhyCameraBindingCanChange(EditConfig.CameraCode);
                    DeviceCamera.CommitConfiguration(() =>
                    {
                        EditConfig.FileServerCfg = DeviceCamera.Config.FileServerCfg;
                        EditConfig.CopyTo(DeviceCamera.Config);
                    }, openingTarget);
                    initialDeviceConfig = JObject.FromObject(EditConfig);
                }
                DeviceCamera.SaveAcquisitionSettings(acquisitionDraft);
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
