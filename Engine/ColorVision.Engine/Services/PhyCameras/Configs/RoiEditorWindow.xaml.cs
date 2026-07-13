using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public partial class RoiEditorWindow : Window, INotifyPropertyChanged
    {
        private const int PreviewPadding = 18;
        private readonly PhyCameraCfg _config;
        private bool _isNormalizing;
        private double _previewScale = 1;
        private double _previewLeft;
        private double _previewTop;
        private int _sensorWidth;
        private int _sensorHeight;
        private int _pointX;
        private int _pointY;
        private int _roiWidth;
        private int _roiHeight;

        public event PropertyChangedEventHandler? PropertyChanged;

        public RoiEditorWindow(PhyCameraCfg config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _sensorWidth = Math.Max(1, config.SensorWidth);
            _sensorHeight = Math.Max(1, config.SensorHeight);
            _roiWidth = Math.Max(0, config.Width);
            _roiHeight = Math.Max(0, config.Height);
            _pointX = Math.Max(0, config.PointX);
            _pointY = Math.Max(0, config.PointY);
            NormalizeFields(false);

            InitializeComponent();
            DataContext = this;
            Loaded += (_, _) =>
            {
                ConfigurePreviewImageView();
                UpdatePreview();
            };
        }

        public int SensorWidth
        {
            get => _sensorWidth;
            set => SetIntegerValue(ref _sensorWidth, value, 1);
        }

        public int SensorHeight
        {
            get => _sensorHeight;
            set => SetIntegerValue(ref _sensorHeight, value, 1);
        }

        public int PointX
        {
            get => _pointX;
            set => SetIntegerValue(ref _pointX, value, 0);
        }

        public int PointY
        {
            get => _pointY;
            set => SetIntegerValue(ref _pointY, value, 0);
        }

        public int RoiWidth
        {
            get => _roiWidth;
            set => SetIntegerValue(ref _roiWidth, value, 0);
        }

        public int RoiHeight
        {
            get => _roiHeight;
            set => SetIntegerValue(ref _roiHeight, value, 0);
        }

        public string Summary => IsFullFrame
            ? string.Format(Properties.Resources.RoiEditor_FullFrameSummary, SensorWidth, SensorHeight)
            : $"X={PointX}, Y={PointY}, W={RoiWidth}, H={RoiHeight} / {SensorWidth} x {SensorHeight}";

        private bool IsFullFrame => _roiWidth == 0 && _roiHeight == 0;

        private int EffectivePointX => _roiWidth > 0 ? _pointX : 0;

        private int EffectivePointY => _roiHeight > 0 ? _pointY : 0;

        private int EffectiveRoiWidth => _roiWidth > 0 ? _roiWidth : _sensorWidth;

        private int EffectiveRoiHeight => _roiHeight > 0 ? _roiHeight : _sensorHeight;

        private void SetIntegerValue(ref int field, int value, int minimum, [CallerMemberName] string propertyName = "")
        {
            value = Math.Max(minimum, value);
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
            NormalizeAndUpdate();
        }

        private void NormalizeAndUpdate()
        {
            if (_isNormalizing)
            {
                return;
            }

            _isNormalizing = true;
            try
            {
                NormalizeFields(true);
                OnPropertyChanged(nameof(Summary));
                UpdatePreview();
            }
            finally
            {
                _isNormalizing = false;
            }
        }

        private void NormalizeFields(bool notifyChanges)
        {
            SetField(ref _sensorWidth, Math.Max(1, _sensorWidth), nameof(SensorWidth), notifyChanges);
            SetField(ref _sensorHeight, Math.Max(1, _sensorHeight), nameof(SensorHeight), notifyChanges);
            SetField(ref _roiWidth, Clamp(_roiWidth, 0, _sensorWidth), nameof(RoiWidth), notifyChanges);
            SetField(ref _roiHeight, Clamp(_roiHeight, 0, _sensorHeight), nameof(RoiHeight), notifyChanges);

            int maxX = _roiWidth > 0 ? Math.Max(0, _sensorWidth - _roiWidth) : 0;
            int maxY = _roiHeight > 0 ? Math.Max(0, _sensorHeight - _roiHeight) : 0;
            SetField(ref _pointX, Clamp(_pointX, 0, maxX), nameof(PointX), notifyChanges);
            SetField(ref _pointY, Clamp(_pointY, 0, maxY), nameof(PointY), notifyChanges);
        }

        private void SetField(ref int field, int value, string propertyName, bool notifyChanges)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            if (notifyChanges)
            {
                OnPropertyChanged(propertyName);
            }
        }

        private void ApplyCenteredRatio(double ratio)
        {
            ratio = Math.Max(0.01, Math.Min(1, ratio));
            _roiWidth = Math.Max(1, (int)Math.Round(_sensorWidth * ratio, MidpointRounding.AwayFromZero));
            _roiHeight = Math.Max(1, (int)Math.Round(_sensorHeight * ratio, MidpointRounding.AwayFromZero));
            CenterCurrentRoi();
            NotifyRoiChanged();
            NormalizeAndUpdate();
        }

        private void CenterCurrentRoi()
        {
            _pointX = _roiWidth > 0 ? Math.Max(0, (_sensorWidth - _roiWidth) / 2) : 0;
            _pointY = _roiHeight > 0 ? Math.Max(0, (_sensorHeight - _roiHeight) / 2) : 0;
        }

        private void NotifyRoiChanged()
        {
            OnPropertyChanged(nameof(PointX));
            OnPropertyChanged(nameof(PointY));
            OnPropertyChanged(nameof(RoiWidth));
            OnPropertyChanged(nameof(RoiHeight));
        }

        private void UpdatePreview()
        {
            if (PreviewCanvas == null || PreviewCanvas.ActualWidth <= 0 || PreviewCanvas.ActualHeight <= 0)
            {
                return;
            }

            double availableWidth = Math.Max(1, PreviewCanvas.ActualWidth - PreviewPadding * 2);
            double availableHeight = Math.Max(1, PreviewCanvas.ActualHeight - PreviewPadding * 2);
            _previewScale = Math.Min(availableWidth / _sensorWidth, availableHeight / _sensorHeight);
            if (double.IsNaN(_previewScale) || double.IsInfinity(_previewScale) || _previewScale <= 0)
            {
                _previewScale = 1;
            }

            double sensorViewWidth = _sensorWidth * _previewScale;
            double sensorViewHeight = _sensorHeight * _previewScale;
            _previewLeft = (PreviewCanvas.ActualWidth - sensorViewWidth) / 2;
            _previewTop = (PreviewCanvas.ActualHeight - sensorViewHeight) / 2;

            SetCanvasBounds(SensorRect, _previewLeft, _previewTop, sensorViewWidth, sensorViewHeight);

            double roiLeft = _previewLeft + EffectivePointX * _previewScale;
            double roiTop = _previewTop + EffectivePointY * _previewScale;
            double roiWidth = Math.Max(1, EffectiveRoiWidth * _previewScale);
            double roiHeight = Math.Max(1, EffectiveRoiHeight * _previewScale);

            SetCanvasBounds(RoiRect, roiLeft, roiTop, roiWidth, roiHeight);
            SetCanvasBounds(MoveThumb, roiLeft, roiTop, Math.Max(8, roiWidth), Math.Max(8, roiHeight));

            Canvas.SetLeft(ResizeThumb, roiLeft + roiWidth - ResizeThumb.Width / 2);
            Canvas.SetTop(ResizeThumb, roiTop + roiHeight - ResizeThumb.Height / 2);
        }

        private void ConfigurePreviewImageView()
        {
            if (PreviewImageView?.Config == null)
            {
                return;
            }

            PreviewImageView.Config.IsToolBarAlVisible = false;
            PreviewImageView.Config.IsToolBarDrawVisible = false;
            PreviewImageView.Config.IsToolBarTopVisible = false;
            PreviewImageView.Config.IsToolBarLeftVisible = false;
            PreviewImageView.Config.IsToolBarRightVisible = false;
        }

        private static void SetCanvasBounds(FrameworkElement element, double left, double top, double width, double height)
        {
            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);
            element.Width = width;
            element.Height = height;
        }

        private bool TryGetSensorPoint(Point canvasPoint, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (_previewScale <= 0)
            {
                return false;
            }

            double sensorX = (canvasPoint.X - _previewLeft) / _previewScale;
            double sensorY = (canvasPoint.Y - _previewTop) / _previewScale;
            if (sensorX < 0 || sensorY < 0 || sensorX > _sensorWidth || sensorY > _sensorHeight)
            {
                return false;
            }

            x = Clamp((int)Math.Round(sensorX, MidpointRounding.AwayFromZero), 0, _sensorWidth);
            y = Clamp((int)Math.Round(sensorY, MidpointRounding.AwayFromZero), 0, _sensorHeight);
            return true;
        }

        private static bool IsVisualChildOf(DependencyObject? child, DependencyObject parent)
        {
            while (child != null)
            {
                if (ReferenceEquals(child, parent))
                {
                    return true;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return false;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (maximum < minimum)
            {
                maximum = minimum;
            }

            return Math.Min(Math.Max(value, minimum), maximum);
        }

        private void HalfButton_Click(object sender, RoutedEventArgs e) => ApplyCenteredRatio(0.5);

        private void QuarterButton_Click(object sender, RoutedEventArgs e) => ApplyCenteredRatio(0.25);

        private void TwoThirdButton_Click(object sender, RoutedEventArgs e) => ApplyCenteredRatio(2d / 3d);

        private void CenterButton_Click(object sender, RoutedEventArgs e)
        {
            CenterCurrentRoi();
            OnPropertyChanged(nameof(PointX));
            OnPropertyChanged(nameof(PointY));
            NormalizeAndUpdate();
        }

        private void FullButton_Click(object sender, RoutedEventArgs e)
        {
            _pointX = 0;
            _pointY = 0;
            _roiWidth = 0;
            _roiHeight = 0;
            NotifyRoiChanged();
            NormalizeAndUpdate();
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePreview();

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dependencyObject &&
                (IsVisualChildOf(dependencyObject, MoveThumb) || IsVisualChildOf(dependencyObject, ResizeThumb)))
            {
                return;
            }

            if (!TryGetSensorPoint(e.GetPosition(PreviewCanvas), out int x, out int y))
            {
                return;
            }

            _pointX = _roiWidth > 0
                ? Clamp(x - EffectiveRoiWidth / 2, 0, Math.Max(0, _sensorWidth - EffectiveRoiWidth))
                : 0;
            _pointY = _roiHeight > 0
                ? Clamp(y - EffectiveRoiHeight / 2, 0, Math.Max(0, _sensorHeight - EffectiveRoiHeight))
                : 0;
            OnPropertyChanged(nameof(PointX));
            OnPropertyChanged(nameof(PointY));
            NormalizeAndUpdate();
        }

        private void MoveThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_previewScale <= 0)
            {
                return;
            }

            int deltaX = (int)Math.Round(e.HorizontalChange / _previewScale, MidpointRounding.AwayFromZero);
            int deltaY = (int)Math.Round(e.VerticalChange / _previewScale, MidpointRounding.AwayFromZero);
            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            _pointX = _roiWidth > 0
                ? Clamp(_pointX + deltaX, 0, Math.Max(0, _sensorWidth - _roiWidth))
                : 0;
            _pointY = _roiHeight > 0
                ? Clamp(_pointY + deltaY, 0, Math.Max(0, _sensorHeight - _roiHeight))
                : 0;
            OnPropertyChanged(nameof(PointX));
            OnPropertyChanged(nameof(PointY));
            NormalizeAndUpdate();
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_previewScale <= 0)
            {
                return;
            }

            int deltaWidth = (int)Math.Round(e.HorizontalChange / _previewScale, MidpointRounding.AwayFromZero);
            int deltaHeight = (int)Math.Round(e.VerticalChange / _previewScale, MidpointRounding.AwayFromZero);
            if (deltaWidth == 0 && deltaHeight == 0)
            {
                return;
            }

            if (_roiWidth <= 0)
            {
                _pointX = 0;
                _roiWidth = _sensorWidth;
                OnPropertyChanged(nameof(PointX));
            }

            if (_roiHeight <= 0)
            {
                _pointY = 0;
                _roiHeight = _sensorHeight;
                OnPropertyChanged(nameof(PointY));
            }

            _roiWidth = Clamp(_roiWidth + deltaWidth, 1, Math.Max(1, _sensorWidth - _pointX));
            _roiHeight = Clamp(_roiHeight + deltaHeight, 1, Math.Max(1, _sensorHeight - _pointY));
            OnPropertyChanged(nameof(RoiWidth));
            OnPropertyChanged(nameof(RoiHeight));
            NormalizeAndUpdate();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            NormalizeFields(true);
            _config.SensorWidth = _sensorWidth;
            _config.SensorHeight = _sensorHeight;
            _config.ROI = new Int32Rect(_pointX, _pointY, _roiWidth, _roiHeight);
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
