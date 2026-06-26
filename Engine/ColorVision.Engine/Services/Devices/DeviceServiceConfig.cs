#pragma warning disable CA1304,CA1311
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
    public class BaseConfig: ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        [Browsable(false)]
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code;

        [Browsable(false)]
        public string SubscribeTopic { get => _SubscribeTopic; set { _SubscribeTopic = value; OnPropertyChanged(); } }
        private string _SubscribeTopic;

        [Browsable(false)]
        public string SendTopic { get => _SendTopic; set { _SendTopic = value; OnPropertyChanged(); } }
        private string _SendTopic;

        [ Browsable(false)]
        public string ServiceToken { get => _ServiceToken; set { _ServiceToken = value; OnPropertyChanged(); } }
        private string _ServiceToken;
    }

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
            if (string.IsNullOrEmpty(status)) return "";

            switch (status.ToLower())
            {
                case "online": return ColorVision.Engine.Properties.Resources.Online;
                case "offline": return ColorVision.Engine.Properties.Resources.offline;
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
                Content = ColorVision.Engine.Properties.Resources.Edit,
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

            var itemTemplate = CreateCameraSnItemTemplate();
            combo.ItemTemplate = itemTemplate;
            combo.ItemContainerStyle = CreateCameraSnItemContainerStyle(itemTemplate);

            dockPanel.Children.Add(combo);
            return dockPanel;
        }

        private static DataTemplate CreateCameraSnItemTemplate()
        {
            DataTemplate itemTemplate = new DataTemplate();

            FrameworkElementFactory root = new FrameworkElementFactory(typeof(StackPanel));
            root.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            root.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 3));

            FrameworkElementFactory header = new FrameworkElementFactory(typeof(DockPanel));
            header.SetValue(DockPanel.LastChildFillProperty, true);

            FrameworkElementFactory statusBlock = CreateStatusTextBlock();
            statusBlock.SetValue(DockPanel.DockProperty, Dock.Right);
            header.AppendChild(statusBlock);

            FrameworkElementFactory deviceSnBlock = CreateTextBlock("DeviceModeDisplayText", 13, FontWeights.SemiBold, 1.0);
            deviceSnBlock.SetBinding(TextBlock.ForegroundProperty, new Binding("LicenseExpiryColor"));
            header.AppendChild(deviceSnBlock);
            root.AppendChild(header);

            FrameworkElementFactory codeBlock = CreateTextBlock("Code", 12, FontWeights.Normal, 0.72);
            codeBlock.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
            root.AppendChild(codeBlock);

            itemTemplate.VisualTree = root;
            return itemTemplate;
        }

        private static Style CreateCameraSnItemContainerStyle(DataTemplate itemTemplate)
        {
            Style style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.MinHeightProperty, 42.0));
            style.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, itemTemplate));
            return style;
        }

        private static FrameworkElementFactory CreateTextBlock(string path, double fontSize, FontWeight fontWeight, double opacity)
        {
            FrameworkElementFactory textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.SetBinding(TextBlock.TextProperty, new Binding(path));
            textBlock.SetBinding(TextBlock.ToolTipProperty, new Binding(path));
            textBlock.SetValue(TextBlock.FontSizeProperty, fontSize);
            textBlock.SetValue(TextBlock.FontWeightProperty, fontWeight);
            textBlock.SetValue(UIElement.OpacityProperty, opacity);
            textBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            return textBlock;
        }

        private static FrameworkElementFactory CreateStatusTextBlock()
        {
            FrameworkElementFactory statusBlock = CreateTextBlock("SysResourceModel.Remark", 12, FontWeights.SemiBold, 1.0);
            statusBlock.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));

            Binding textBinding = new Binding("SysResourceModel.Remark") { Converter = new StatusToTextConverter() };
            statusBlock.SetBinding(TextBlock.TextProperty, textBinding);

            Binding colorBinding = new Binding("SysResourceModel.Remark") { Converter = new StatusToColorConverter() };
            statusBlock.SetBinding(TextBlock.ForegroundProperty, colorBinding);

            return statusBlock;
        }
    }

}
