using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices
{
    public class BaseConfig: ViewModelBase, IServiceConfig
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        [Browsable(false)]
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code;

        /// <summary>
        /// 心跳时间
        /// </summary>
        public int HeartbeatTime { get => _HeartbeatTime; set { _HeartbeatTime = value; OnPropertyChanged(); } }
        private int _HeartbeatTime = 5000;


        [Browsable(false)]
        public string SubscribeTopic { get => _SubscribeTopic; set { _SubscribeTopic = value; OnPropertyChanged(); } }
        private string _SubscribeTopic;

        [Browsable(false)]
        public string SendTopic { get => _SendTopic; set { _SendTopic = value; OnPropertyChanged(); } }
        private string _SendTopic;

        //Token
        [ Browsable(false)]
        public string ServiceToken { get => _ServiceToken; set { _ServiceToken = value; OnPropertyChanged(); } }
        private string _ServiceToken;



    }
    public delegate void DeviceStatusChangedHandler(DeviceStatusType deviceStatus);

    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class DeviceServiceConfig : BaseConfig
    {
        /// <summary>
        /// 设备序号
        /// </summary>
        [Browsable(false)]
        public string Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private string _Id;

        /// <summary>
        /// 许可
        /// </summary>
        [PropertyEditorType(typeof(TextSNPropertiesEditor))]
        public virtual string SN { get => _SN; set { _SN = value; OnPropertyChanged(); } }
        private string _SN;
    }

    // 新增：状态转中文文本转换器
    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (string.IsNullOrEmpty(status)) return "未知";

            switch (status.ToLower())
            {
                case "online": return "在线";
                case "offline":
                case "offine": return "离线";
                default: return status;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (string.IsNullOrEmpty(status)) return Brushes.Gray;

            switch (status.ToLower())
            {
                case "online": return Brushes.Green;
                case "offline": return Brushes.Red;
                default: return Brushes.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TextSNPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            Button button = new Button
            {
                Content = "编辑",
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 70,
            };
            RelayCommand relayCommand = new RelayCommand((o) =>
            {
                PhyCameraManagerWindow phyCameraManager = new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                phyCameraManager.ShowDialog();
            });
            button.Command = relayCommand;
            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var combo = new HandyControl.Controls.ComboBox { Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            combo.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;

            // 1. 设置 TextSearch.TextPath
            System.Windows.Controls.TextSearch.SetTextPath(combo, "Code");

            // 2. 创建 ItemTemplate
            DataTemplate itemTemplate = new DataTemplate();

            // 创建根布局 StackPanel (Horizontal)
            FrameworkElementFactory stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // --- 第一部分：Code (颜色绑定 LicenseExpiryColor 表示过期状态) ---
            FrameworkElementFactory codeBlock = new FrameworkElementFactory(typeof(TextBlock));
            codeBlock.SetBinding(TextBlock.TextProperty, new Binding("Code"));
            // 绑定到 PhyCamera.LicenseExpiryColor
            codeBlock.SetBinding(TextBlock.ForegroundProperty, new Binding("LicenseExpiryColor"));
            stackPanelFactory.AppendChild(codeBlock);

            // --- 第二部分：空格分隔符 ---
            FrameworkElementFactory spaceBlock = new FrameworkElementFactory(typeof(TextBlock));
            spaceBlock.SetValue(TextBlock.TextProperty, " ");
            stackPanelFactory.AppendChild(spaceBlock);

            // --- 第三部分：在线状态 (中文文本，颜色绑定 Online/Offline) ---
            FrameworkElementFactory statusBlock = new FrameworkElementFactory(typeof(TextBlock));

            // 文本绑定：使用 StatusToTextConverter 转中文
            Binding textBinding = new Binding("SysResourceModel.Remark");
            textBinding.Converter = new StatusToTextConverter();
            statusBlock.SetBinding(TextBlock.TextProperty, textBinding);

            // 颜色绑定：使用 StatusToColorConverter 转颜色
            Binding colorBinding = new Binding("SysResourceModel.Remark");
            colorBinding.Converter = new StatusToColorConverter();
            statusBlock.SetBinding(TextBlock.ForegroundProperty, colorBinding);

            stackPanelFactory.AppendChild(statusBlock);

            // 设置 VisualTree
            itemTemplate.VisualTree = stackPanelFactory;
            combo.ItemTemplate = itemTemplate;

            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }

}
