using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using CVCommCore;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices
{
    public class BaseConfig: ViewModelBase, IServiceConfig
    {
        [Category("Base")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        [Category("Base")]
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code;

        /// <summary>
        /// 心跳时间
        /// </summary>
        [Category("Base")]
        public int HeartbeatTime { get => _HeartbeatTime; set { _HeartbeatTime = value; OnPropertyChanged(); } }
        private int _HeartbeatTime = 5000;


        [Category("Base")]
        public string SubscribeTopic { get => _SubscribeTopic; set { _SubscribeTopic = value; OnPropertyChanged(); } }
        private string _SubscribeTopic;

        [Category("Base")]
        public string SendTopic { get => _SendTopic; set { _SendTopic = value; OnPropertyChanged(); } }
        private string _SendTopic;

        //Token
        [Category("Base")]
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
        [PropertyEditorType(typeof(TextSNPropertiesEditor)),Category("Base")]
        public virtual string SN { get => _SN; set { _SN = value; OnPropertyChanged(); } }
        private string _SN;
    }

    public class TextSNPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var combo = new HandyControl.Controls.ComboBox { Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            combo.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;
            combo.DisplayMemberPath = "Code";
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }

}
