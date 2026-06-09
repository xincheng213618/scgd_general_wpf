#pragma warning disable CA1711
using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Draw
{
    public class DefalutTextAttribute : ViewModelBase,IConfig
    {
        private static readonly object DefaultLock = new();
        private static DefalutTextAttribute _defalut;

        public static DefalutTextAttribute Defalut
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        var configBacked = ConfigService.Instance.GetRequiredService<DefalutTextAttribute>();
                        lock (DefaultLock)
                        {
                            _defalut = configBacked;
                            return _defalut;
                        }
                    }
                    catch
                    {
                    }
                }

                lock (DefaultLock)
                {
                    _defalut ??= new DefalutTextAttribute();
                    return _defalut;
                }
            }
        }

    [DisplayName("物理长度")]
        public double ActualLength { get => _ActualLength; set { _ActualLength = value <= 0 ? 1 : value; OnPropertyChanged(); } }
        private double _ActualLength = 1;

    [DisplayName("物理单位")]
        public string PhysicalUnit { get => _PhysicalUnit; set { _PhysicalUnit = string.IsNullOrWhiteSpace(value) ? "Px" : value; OnPropertyChanged(); } }
        private string _PhysicalUnit = "Px";

    [DisplayName("启用物理单位")]
        public bool IsUsePhysicalUnit { get => _IsUsePhysicalUnit; set { _IsUsePhysicalUnit = value; OnPropertyChanged(); } }
        private bool _IsUsePhysicalUnit;

    }



}
