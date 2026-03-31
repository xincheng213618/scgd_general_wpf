using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace Spectrum
{
    public class SpectrumStatusBarProvider : IStatusBarProvider
    {
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            var manager = SpectrometerManager.Instance;

            return new List<StatusBarMeta>
            {
                // 1. 连接状态 (绿/红圆点 + 文字)
                new StatusBarMeta
                {
                    Id = "Spectrum_Connection",
                    Name = Properties.Resources.连接状态,
                    Description = Properties.Resources.连接状态,
                    Type = StatusBarType.IconText,
                    Alignment = StatusBarAlignment.Left,
                    Order = 1,
                    TargetName = "Spectrum",
                    IconContent = CreateConnectionIcon(manager),
                    TextBindingName = nameof(SpectrometerManager.ConnectionTypeDisplay),
                    Source = manager,
                },
                // 2. 硬件型号
                new StatusBarMeta
                {
                    Id = "Spectrum_HardwareModel",
                    Name = Properties.Resources.硬件型号,
                    Description = Properties.Resources.硬件型号,
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Left,
                    Order = 3,
                    TargetName = "Spectrum",
                    BindingName = nameof(SpectrometerManager.HardwareModel),
                    Source = manager,
                },
                // 3. SN序列号 (点击复制)
                new StatusBarMeta
                {
                    Id = "Spectrum_SN",
                    Name = "SN",
                    Description = "SN序列号，点击复制",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Left,
                    Order = 4,
                    TargetName = "Spectrum",
                    BindingName = nameof(SpectrometerManager.SerialNumber),
                    Source = manager,
                    Command = new RelayCommand(a =>
                    {
                        var sn = SpectrometerManager.Instance.SerialNumber;
                        if (!string.IsNullOrEmpty(sn) && sn != "---")
                            Clipboard.SetText(sn);
                    }),
                },
                // 4. 标定组
                new StatusBarMeta
                {
                    Id = "Spectrum_Calibration",
                    Name = "标定",
                    Description = "当前标定组",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Left,
                    Order = 5,
                    TargetName = "Spectrum",
                    BindingName = nameof(SpectrometerManager.ActiveCalibrationGroupName),
                    Source = manager,
                },
                // 5. 测量模式
                new StatusBarMeta
                {
                    Id = "Spectrum_Mode",
                    Name = "测量模式",
                    Description = "当前测量模式 (通过工具菜单切换)",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Left,
                    Order = 6,
                    TargetName = "Spectrum",
                    BindingName = nameof(SpectrometerManager.MeasurementMode),
                    Source = manager,
                },
                // 6. Shutter 状态
                new StatusBarMeta
                {
                    Id = "Spectrum_Shutter",
                    Name = "Shutter",
                    Description = "Shutter 连接状态",
                    Type = StatusBarType.IconText,
                    Alignment = StatusBarAlignment.Left,
                    Order = 7,
                    TargetName = "Spectrum",
                    IconContent = CreateDeviceIcon(manager.ShutterController),
                    Source = manager.ShutterController,
                },
                // 7. CFW 滤光轮状态
                new StatusBarMeta
                {
                    Id = "Spectrum_CFW",
                    Name = "CFW",
                    Description = "CFW 滤光轮连接状态",
                    Type = StatusBarType.IconText,
                    Alignment = StatusBarAlignment.Left,
                    Order = 8,
                    TargetName = "Spectrum",
                    IconContent = CreateDeviceIcon(manager.FilterWheelController),
                    Source = manager.FilterWheelController,
                },
            };
        }

        /// <summary>
        /// 创建连接状态指示圆点（绑定到 IsConnected）
        /// </summary>
        private static UIElement CreateConnectionIcon(SpectrometerManager manager)
        {
            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var style = new Style(typeof(System.Windows.Shapes.Ellipse));
            style.Setters.Add(new Setter(System.Windows.Shapes.Ellipse.FillProperty,
                new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35))));

            var trigger = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding(nameof(SpectrometerManager.IsConnected)) { Source = manager },
                Value = true,
            };
            trigger.Setters.Add(new Setter(System.Windows.Shapes.Ellipse.FillProperty,
                new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60))));
            style.Triggers.Add(trigger);

            ellipse.Style = style;
            return ellipse;
        }

        /// <summary>
        /// 创建设备连接状态指示圆点（绑定到 IsConnected 属性）
        /// </summary>
        private static UIElement CreateDeviceIcon(INotifyPropertyChanged controller)
        {
            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var style = new Style(typeof(System.Windows.Shapes.Ellipse));
            style.Setters.Add(new Setter(System.Windows.Shapes.Ellipse.FillProperty,
                new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35))));

            var trigger = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding("IsConnected") { Source = controller },
                Value = true,
            };
            trigger.Setters.Add(new Setter(System.Windows.Shapes.Ellipse.FillProperty,
                new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60))));
            style.Triggers.Add(trigger);

            ellipse.Style = style;
            return ellipse;
        }
    }
}
