using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes;
using cvColorVision;
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
        public DeviceCamera DeviceCamera { get; set; }

        public MQTTCamera Service { get => DeviceCamera.DService; }

        public ConfigCamera EditConfig { get; set; }

        public EditCamera(DeviceCamera mQTTDeviceCamera)
        {
            DeviceCamera = mQTTDeviceCamera;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
        public List<PhyCamera> PhyCameras { get; set; } = new List<PhyCamera>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            var phyCameraManager = PhyCameraManager.GetInstance();
            var deviceCamera = DeviceCamera;
            PhyCameras = phyCameraManager.PhyCameras
                .Where(p => p.DeviceCamera == null || p.DeviceCamera.Name == deviceCamera.Name)
                .ToList();

            CameraPhyID.ItemsSource = PhyCameras;

            CameraPhyID.SelectedItem = phyCameraManager.GetPhyCamera(deviceCamera.Config.CameraCode);
            CameraPhyID.DisplayMemberPath = "Code";

            var type = DeviceCamera.Config.CameraType;


            ComboxeEvaFunc.ItemsSource = from e1 in Enum.GetValues(typeof(EvaFunc)).Cast<EvaFunc>()
                                         select new KeyValuePair<EvaFunc, string>(e1, e1.ToString());
            EditConfig = DeviceCamera.Config.Clone();
            DataContext = DeviceCamera;
            EditContent.DataContext = EditConfig;
            //EditStackPanel.Children.Add(GenerateContent(EditConfig));
        }

        private static StackPanel GenerateContent(object config)
        {
            var stackPanel = new StackPanel();
            var type = config.GetType();
            var properties = type.GetProperties();

            foreach (var property in properties)
            {
                if (!(property.CanWrite && property.CanRead)) continue; // Skip properties that cannot be set

                var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                var textBlock = new TextBlock { Text = property.Name, Width = 120 };
                dockPanel.Children.Add(textBlock);

                if (property.PropertyType == typeof(string) ||
                    property.PropertyType == typeof(int) ||
                    property.PropertyType == typeof(double) ||
                    property.PropertyType == typeof(float))
                {
                    var textBox = new TextBox
                    {
                        Text = property.GetValue(config)?.ToString(),
                        Style = (Style)Application.Current.Resources["TextBox.Small"]
                    };
                    dockPanel.Children.Add(textBox);
                }
                else if (property.PropertyType == typeof(bool))
                {
                    var checkBox = new CheckBox
                    {
                        Content = "启用",
                        IsChecked = (bool?)property.GetValue(config)
                    };
                    dockPanel.Children.Add(checkBox);
                }
                else if (property.PropertyType.IsEnum)
                {
                    var comboBox = new ComboBox
                    {
                        SelectedValue = property.GetValue(config),
                        SelectedValuePath = "Key",
                        DisplayMemberPath = "Value",
                        Margin = new Thickness(0, 0, 10, 0)
                    };

                    comboBox.ItemsSource = Enum.GetValues(property.PropertyType)
                                               .Cast<Enum>()
                                               .Select(e => new KeyValuePair<Enum, string>(e, e.ToString()));
                    dockPanel.Children.Add(comboBox);
                }

                stackPanel.Children.Add(dockPanel);
            }

            return stackPanel;
        }
    

    private void Button_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera.PhyCamera?.ReleaseDeviceCamera();
            EditConfig.CopyTo(DeviceCamera.Config);
            if (DeviceCamera.PhyCamera !=null)
                DeviceCamera.PhyCamera.ConfigChanged += DeviceCamera.PhyCameraConfigChanged;


            Close();
        }

        private void CameraPhyID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && EditConfig !=null)
            {
                UpdateConfig();
            }
        }
        public void UpdateConfig()
        {
            if (CameraPhyID.SelectedIndex > -1)
            {
                var phyCamera = PhyCameras[CameraPhyID.SelectedIndex];
                EditConfig.Channel = phyCamera.Config.Channel;
                EditConfig.CFW.CopyFrom(phyCamera.Config.CFW);
                EditConfig.MotorConfig.CopyFrom(phyCamera.Config.MotorConfig);

                EditConfig.CameraMode = phyCamera.Config.CameraMode;
                EditConfig.CameraModel = phyCamera.Config.CameraModel;
                EditConfig.TakeImageMode = phyCamera.Config.TakeImageMode;
                EditConfig.ImageBpp = phyCamera.Config.ImageBpp;
            }
        }

        private void UpdateConfig_Click(object sender, RoutedEventArgs e)
        {
            UpdateConfig();
        }

        private void RoiDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Int32RectViewModel int32RectViewModel)
            {
                EditConfig.ROIParams.Remove(int32RectViewModel);
            }
        }
    }
}
