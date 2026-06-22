using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Settings
{
    public class DefaultRealtimeCameraConfig : ViewModelBase, IConfig
    {
        private static readonly object SyncLock = new();
        private static DefaultRealtimeCameraConfig? _current;

        public static DefaultRealtimeCameraConfig Current
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        var configBacked = ConfigService.Instance.GetRequiredService<DefaultRealtimeCameraConfig>();
                        lock (SyncLock)
                        {
                            _current = configBacked;
                            return _current;
                        }
                    }
                    catch
                    {
                    }
                }

                lock (SyncLock)
                {
                    _current ??= new DefaultRealtimeCameraConfig();
                    return _current;
                }
            }
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<DefaultRealtimeCameraConfig>();
            }
            catch
            {
            }
        }

        [DisplayName("计算清晰度")]
        [Description("是否在支持的 realtime 相机入口上计算清晰度。")]
        public bool IsCalArtculation { get => _isCalArtculation; set { _isCalArtculation = value; OnPropertyChanged(); } }
        private bool _isCalArtculation = true;

        [DisplayName("清晰度算法")]
        public FocusAlgorithm EvaFunc { get => _evaFunc; set { _evaFunc = value; OnPropertyChanged(); } }
        private FocusAlgorithm _evaFunc = FocusAlgorithm.VarianceOfLaplacian;

        [Browsable(false), JsonIgnore]
        public RectangleTextProperties RectangleTextProperties { get; set; } = new()
        {
            Brush = Brushes.Transparent,
            Pen = new Pen(Brushes.LimeGreen, 1),
            Foreground = Brushes.DarkOrange,
            FontSize = 200,
            Position = RectangleTextPosition.Top,
            IsShowText = true
        };
    }
}
