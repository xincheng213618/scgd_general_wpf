using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;

namespace ImageProjector
{
    /// <summary>
    /// Image display stretch mode
    /// </summary>
    public enum ImageStretchMode
    {
        [Description("适应")]
        Uniform,        // Maintain aspect ratio, fit within bounds
        [Description("拉伸")]
        Fill,           // Stretch to fill, may distort
        [Description("居中")]
        None,           // No stretching, center the image
        [Description("填充")]
        UniformToFill   // Fill while maintaining aspect ratio, may crop
    }

    /// <summary>
    /// Represents a single image item in the projector list
    /// </summary>
    public class ImageProjectorItem : ViewModelBase
    {
        public string FilePath { get => _FilePath; set { _FilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileName)); } }
        private string _FilePath = string.Empty;

        public string FileName => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetFileName(FilePath);

        public ImageProjectorItem() { }

        public ImageProjectorItem(string filePath)
        {
            FilePath = filePath;
        }
    }

    /// <summary>
    /// Configuration class for ImageProjector, stores the last used state
    /// </summary>
    public class ImageProjectorConfig : ViewModelBase, IConfig
    {
        public static ImageProjectorConfig Instance => ConfigService.Instance.GetRequiredService<ImageProjectorConfig>();

        [DisplayName("图片列表")]
        public ObservableCollection<ImageProjectorItem> ImageItems { get => _ImageItems; set { _ImageItems = value; OnPropertyChanged(); } }
        private ObservableCollection<ImageProjectorItem> _ImageItems = new ObservableCollection<ImageProjectorItem>();

        [DisplayName("上次选中索引")]
        public int LastSelectedIndex { get => _LastSelectedIndex; set { _LastSelectedIndex = value; OnPropertyChanged(); } }
        private int _LastSelectedIndex = -1;

        [DisplayName("上次选中的显示器")]
        public string LastSelectedMonitor { get => _LastSelectedMonitor; set { _LastSelectedMonitor = value; OnPropertyChanged(); } }
        private string _LastSelectedMonitor = string.Empty;

        [DisplayName("图片显示模式")]
        public ImageStretchMode StretchMode { get => _StretchMode; set { _StretchMode = value; OnPropertyChanged(); } }
        private ImageStretchMode _StretchMode = ImageStretchMode.Uniform;

        /// <summary>
        /// Converts ImageStretchMode to WPF Stretch enum
        /// </summary>
        public static Stretch ToStretch(ImageStretchMode mode)
        {
            return mode switch
            {
                ImageStretchMode.Fill => Stretch.Fill,
                ImageStretchMode.None => Stretch.None,
                ImageStretchMode.UniformToFill => Stretch.UniformToFill,
                _ => Stretch.Uniform,
            };
        }
    }
}
