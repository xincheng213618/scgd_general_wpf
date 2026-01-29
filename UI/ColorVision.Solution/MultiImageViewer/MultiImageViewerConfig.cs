using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Solution.MultiImageViewer
{
    /// <summary>
    /// 多图预览控件配置
    /// </summary>
    [DisplayName("MultiImageViewerConfig")]
    public class MultiImageViewerConfig : ViewModelBase, IConfig
    {
        public static MultiImageViewerConfig Instance => ConfigService.Instance.GetRequiredService<MultiImageViewerConfig>();

        /// <summary>
        /// 获取用于编辑属性的命令
        /// </summary>
        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }

        /// <summary>
        /// 清除缓存命令
        /// </summary>
        [JsonIgnore]
        public RelayCommand ClearCacheCommand { get; set; }

        public MultiImageViewerConfig()
        {
            EditCommand = new RelayCommand(a =>
            {
                new PropertyEditorWindow(this)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                }.ShowDialog();
            });

            ClearCacheCommand = new RelayCommand(a =>
            {
                ThumbnailCacheManager.Instance.ClearCache();
                OnPropertyChanged(nameof(CacheSizeDisplay));
                OnPropertyChanged(nameof(CacheCountDisplay));
            });
        }

        /// <summary>
        /// 是否显示文件列表
        /// </summary>
        [DisplayName("显示文件列表")]
        [Category("视图")]
        public bool IsShowListView
        {
            get => _IsShowListView;
            set { _IsShowListView = value; OnPropertyChanged(); }
        }
        private bool _IsShowListView = true;

        /// <summary>
        /// 文件列表高度
        /// </summary>
        [DisplayName("列表高度")]
        [Category("视图")]
        public double ListHeight
        {
            get => _ListHeight;
            set { _ListHeight = value; OnPropertyChanged(); }
        }
        private double _ListHeight = 200;

        /// <summary>
        /// 缩略图大小
        /// </summary>
        [DisplayName("缩略图大小")]
        [Category("缩略图")]
        public int ThumbnailSize
        {
            get => _ThumbnailSize;
            set { _ThumbnailSize = Math.Max(50, Math.Min(300, value)); OnPropertyChanged(); }
        }
        private int _ThumbnailSize = 120;

        /// <summary>
        /// 最大显示文件数量
        /// </summary>
        [DisplayName("最大显示数量")]
        [Category("视图")]
        public int MaxDisplayCount
        {
            get => _MaxDisplayCount;
            set { _MaxDisplayCount = Math.Max(10, value); OnPropertyChanged(); }
        }
        private int _MaxDisplayCount = 1000;

        /// <summary>
        /// 是否启用缩略图缓存
        /// </summary>
        [DisplayName("启用缩略图缓存")]
        [Category("缩略图")]
        public bool EnableThumbnailCache
        {
            get => _EnableThumbnailCache;
            set { _EnableThumbnailCache = value; OnPropertyChanged(); }
        }
        private bool _EnableThumbnailCache = true;

        /// <summary>
        /// 是否显示缩略图
        /// </summary>
        [DisplayName("显示缩略图")]
        [Category("缩略图")]
        public bool ShowThumbnail
        {
            get => _ShowThumbnail;
            set { _ShowThumbnail = value; OnPropertyChanged(); }
        }
        private bool _ShowThumbnail = true;

        /// <summary>
        /// 图像读取延迟（毫秒）
        /// </summary>
        [DisplayName("图像读取延迟(ms)")]
        [Category("视图")]
        public int ImageReadDelay
        {
            get => _ImageReadDelay;
            set { _ImageReadDelay = Math.Max(100, value); OnPropertyChanged(); }
        }
        private int _ImageReadDelay = 500;

        /// <summary>
        /// 缓存大小显示（只读）
        /// </summary>
        [JsonIgnore]
        [DisplayName("缓存大小")]
        [Category("缓存")]
        public string CacheSizeDisplay
        {
            get
            {
                var bytes = ThumbnailCacheManager.Instance.GetCacheSize();
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            }
        }

        /// <summary>
        /// 缓存条目数量显示（只读）
        /// </summary>
        [JsonIgnore]
        [DisplayName("缓存数量")]
        [Category("缓存")]
        public string CacheCountDisplay => $"{ThumbnailCacheManager.Instance.GetCacheCount()} 个";

        /// <summary>
        /// 支持的图像扩展名
        /// </summary>
        [JsonIgnore]
        public static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };
    }
}
