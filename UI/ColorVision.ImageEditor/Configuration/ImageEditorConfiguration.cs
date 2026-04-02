using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// 图像编辑器配置项
    /// </summary>
    public class ImageEditorSettings : ConfigurationItemBase
    {
        public override string Key => "ImageEditor.Settings";

        private bool _showGrid = true;
        [DisplayName("显示网格")]
        [Description("是否显示背景网格")]
        public bool ShowGrid
        {
            get => _showGrid;
            set => SetProperty(ref _showGrid, value);
        }

        private bool _showRuler = true;
        [DisplayName("显示标尺")]
        [Description("是否显示标尺")]
        public bool ShowRuler
        {
            get => _showRuler;
            set => SetProperty(ref _showRuler, value);
        }

        private bool _enableSnapToGrid = false;
        [DisplayName("启用网格对齐")]
        [Description("是否启用网格对齐功能")]
        public bool EnableSnapToGrid
        {
            get => _enableSnapToGrid;
            set => SetProperty(ref _enableSnapToGrid, value);
        }

        private double _gridSize = 20;
        [DisplayName("网格大小")]
        [Description("网格单元格大小")]
        public double GridSize
        {
            get => _gridSize;
            set => SetProperty(ref _gridSize, value);
        }

        private Color _gridColor = Colors.LightGray;
        [DisplayName("网格颜色")]
        [Description("网格线颜色")]
        public Color GridColor
        {
            get => _gridColor;
            set => SetProperty(ref _gridColor, value);
        }

        private double _zoomStep = 0.1;
        [DisplayName("缩放步长")]
        [Description("每次缩放的变化量")]
        public double ZoomStep
        {
            get => _zoomStep;
            set => SetProperty(ref _zoomStep, value);
        }

        private double _minZoom = 0.1;
        [DisplayName("最小缩放")]
        public double MinZoom
        {
            get => _minZoom;
            set => SetProperty(ref _minZoom, value);
        }

        private double _maxZoom = 10.0;
        [DisplayName("最大缩放")]
        public double MaxZoom
        {
            get => _maxZoom;
            set => SetProperty(ref _maxZoom, value);
        }

        private bool _enableAnimations = true;
        [DisplayName("启用动画")]
        [Description("是否启用界面动画")]
        public bool EnableAnimations
        {
            get => _enableAnimations;
            set => SetProperty(ref _enableAnimations, value);
        }

        private bool _showStatusBar = true;
        [DisplayName("显示状态栏")]
        public bool ShowStatusBar
        {
            get => _showStatusBar;
            set => SetProperty(ref _showStatusBar, value);
        }
    }

    /// <summary>
    /// 绘图工具配置
    /// </summary>
    public class DrawingToolSettings : ConfigurationItemBase
    {
        public override string Key => "ImageEditor.DrawingTools";

        private Color _defaultStrokeColor = Colors.Red;
        [DisplayName("默认线条颜色")]
        public Color DefaultStrokeColor
        {
            get => _defaultStrokeColor;
            set => SetProperty(ref _defaultStrokeColor, value);
        }

        private double _defaultStrokeThickness = 2;
        [DisplayName("默认线条粗细")]
        public double DefaultStrokeThickness
        {
            get => _defaultStrokeThickness;
            set => SetProperty(ref _defaultStrokeThickness, value);
        }

        private Color _defaultFillColor = Colors.Transparent;
        [DisplayName("默认填充颜色")]
        public Color DefaultFillColor
        {
            get => _defaultFillColor;
            set => SetProperty(ref _defaultFillColor, value);
        }

        private bool _enableAntiAliasing = true;
        [DisplayName("启用抗锯齿")]
        public bool EnableAntiAliasing
        {
            get => _enableAntiAliasing;
            set => SetProperty(ref _enableAntiAliasing, value);
        }

        private bool _showToolTips = true;
        [DisplayName("显示工具提示")]
        public bool ShowToolTips
        {
            get => _showToolTips;
            set => SetProperty(ref _showToolTips, value);
        }
    }

    /// <summary>
    /// 图像编辑器配置 - 整合所有配置项
    /// </summary>
    public class ImageEditorConfiguration : EditorConfiguration
    {
        public ImageEditorSettings Settings { get; private set; }
        public DrawingToolSettings DrawingTools { get; private set; }

        public ImageEditorConfiguration() : base("ImageEditor")
        {
            InitializeDefaults();
        }

        public ImageEditorConfiguration(string name) : base(name)
        {
            InitializeDefaults();
        }

        protected sealed override void InitializeDefaults()
        {
            Settings = new ImageEditorSettings();
            DrawingTools = new DrawingToolSettings();

            SetItem(Settings);
            SetItem(DrawingTools);
        }

        /// <summary>
        /// 创建配置变更命令
        /// </summary>
        public ICommand CreatePropertyChangeCommand<T>(
            string propertyName,
            Func<T> getter,
            Action<T> setter,
            T newValue)
        {
            return new PropertyChangeCommand<ImageEditorConfiguration, T>(
                this, propertyName, getter, setter, newValue);
        }
    }
}
