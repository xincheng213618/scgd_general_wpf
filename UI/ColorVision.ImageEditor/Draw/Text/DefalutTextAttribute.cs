#pragma warning disable CA1711
using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ColorVision.ImageEditor.Settings;

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
        [Display(Name = nameof(SettingsText.ActualLength), ResourceType = typeof(SettingsText))]
        public double ActualLength { get => _ActualLength; set { _ActualLength = double.IsFinite(value) && value > 0 ? value : 1; OnPropertyChanged(); } }
        private double _ActualLength = 1;

    [DisplayName("物理单位")]
        [Display(Name = nameof(SettingsText.PhysicalUnit), ResourceType = typeof(SettingsText))]
        public string PhysicalUnit { get => _PhysicalUnit; set { _PhysicalUnit = string.IsNullOrWhiteSpace(value) ? "Px" : value; OnPropertyChanged(); } }
        private string _PhysicalUnit = "Px";

    [DisplayName("启用物理单位")]
        [Display(Name = nameof(SettingsText.IsUsePhysicalUnit), ResourceType = typeof(SettingsText))]
        public bool IsUsePhysicalUnit { get => _IsUsePhysicalUnit; set { _IsUsePhysicalUnit = value; OnPropertyChanged(); } }
        private bool _IsUsePhysicalUnit;

    }



}
