using ColorVision.UI;
using Spectrum.Properties;
using System.Collections.Generic;

namespace Spectrum
{
    /// <summary>
    /// Spectrum 插件的状态栏提供者，将原本硬编码在 XAML 中的状态栏项
    /// 通过 IStatusBarProvider 接口动态提供，支持 StatusBarControl 复用。
    /// </summary>
    public class SpectrumStatusBarProvider : IStatusBarProvider
    {
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            var manager = SpectrometerManager.Instance;

            return new List<StatusBarMeta>
            {
                new StatusBarMeta
                {
                    Name = Resources.连接状态,
                    Description = Resources.连接状态,
                    Order = 1,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SpectrometerManager.ConnectionStatusText),
                    Source = manager,
                    TargetName = "Spectrum",
                    Alignment = StatusBarAlignment.Left,
                },
                new StatusBarMeta
                {
                    Name = "连接类型",
                    Description = "连接类型",
                    Order = 2,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SpectrometerManager.ConnectionTypeDisplay),
                    Source = manager,
                    TargetName = "Spectrum",
                    Alignment = StatusBarAlignment.Left,
                },
                new StatusBarMeta
                {
                    Name = Resources.硬件型号,
                    Description = Resources.硬件型号,
                    Order = 3,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SpectrometerManager.HardwareModelText),
                    Source = manager,
                    TargetName = "Spectrum",
                    Alignment = StatusBarAlignment.Left,
                },
                new StatusBarMeta
                {
                    Name = "SN",
                    Description = "SN序列号",
                    Order = 4,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SpectrometerManager.SerialNumber),
                    Source = manager,
                    TargetName = "Spectrum",
                    Alignment = StatusBarAlignment.Left,
                },
                new StatusBarMeta
                {
                    Name = "标定",
                    Description = "当前标定组",
                    Order = 5,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SpectrometerManager.ActiveCalibrationGroupName),
                    Source = manager,
                    TargetName = "Spectrum",
                    Alignment = StatusBarAlignment.Left,
                },
                new StatusBarMeta
                {
                    Name = "测量模式",
                    Description = "当前测量模式",
                    Order = 6,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SpectrometerManager.MeasurementModeText),
                    Source = manager,
                    TargetName = "Spectrum",
                    Alignment = StatusBarAlignment.Right,
                },
            };
        }
    }
}
