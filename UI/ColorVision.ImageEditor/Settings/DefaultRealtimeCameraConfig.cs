using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;

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

        [DisplayName("启用视频计算")]
        [Description("是否允许 realtime 相机入口构建额外处理请求。关闭后只显示最新帧，不触发清晰度计算。")]
        public bool IsUseCacheFile { get => _isUseCacheFile; set { _isUseCacheFile = value; OnPropertyChanged(); } }
        private bool _isUseCacheFile;

        [DisplayName("计算清晰度")]
        [Description("是否在支持的 realtime 相机入口上计算清晰度。")]
        public bool IsCalArtculation { get => _isCalArtculation; set { _isCalArtculation = value; OnPropertyChanged(); } }
        private bool _isCalArtculation = true;

        [DisplayName("清晰度算法")]
        public FocusAlgorithm EvaFunc { get => _evaFunc; set { _evaFunc = value; OnPropertyChanged(); } }
        private FocusAlgorithm _evaFunc = FocusAlgorithm.VarianceOfLaplacian;

        [DisplayName("显示帧率上限")]
        [Description("所有 realtime 图像入口默认共用的显示 FPS 上限。")]
        public int MaxDisplayFps { get => _maxDisplayFps; set { _maxDisplayFps = value < 0 ? 0 : value; OnPropertyChanged(); } }
        private int _maxDisplayFps = 60;

        [JsonIgnore]
        [DisplayName("状态文字样式")]
        public TextProperties TextProperties { get => _textProperties; set { _textProperties = value; OnPropertyChanged(); } }
        private TextProperties _textProperties = new() { FontSize = 200 };

        [Browsable(false), JsonIgnore]
        public RectangleTextProperties RectangleTextProperties { get; set; } = new();
    }
}
