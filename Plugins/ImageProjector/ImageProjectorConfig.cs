using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace ImageProjector
{
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
    }
}
