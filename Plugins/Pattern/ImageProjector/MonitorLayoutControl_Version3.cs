using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace Pattern.ImageProjector
{
    /// <summary>
    /// 可视化显示器布局选择控件
    /// </summary>
    public class MonitorLayoutControl : Canvas
    {
        private readonly List<MonitorItem> _monitorItems = new();
        private MonitorItem?  _selectedItem;

        public event EventHandler<Screen>? ScreenSelected;

        public static readonly DependencyProperty SelectedScreenProperty =
            DependencyProperty.Register(nameof(SelectedScreen), typeof(Screen), typeof(MonitorLayoutControl),
                new PropertyMetadata(null, OnSelectedScreenChanged));

        public Screen?  SelectedScreen
        {
            get => (Screen?)GetValue(SelectedScreenProperty);
            set => SetValue(SelectedScreenProperty, value);
        }

        private static void OnSelectedScreenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MonitorLayoutControl control && e.NewValue is Screen screen)
            {
                control. SelectScreen(screen);
            }
        }

        public MonitorLayoutControl()
        {
            this.Background = Brushes. Transparent;
            this.MinHeight = 150;
            this. Loaded += (s, e) => RefreshMonitors();
        }

        public void RefreshMonitors()
        {
            this.Children.Clear();
            _monitorItems.Clear();

            var screens = Screen.AllScreens.ToList();
            if (! screens.Any()) return;

            // 计算所有屏幕的边界
            int minX = screens. Min(s => s.Bounds.Left);
            int minY = screens.Min(s => s. Bounds.Top);
            int maxX = screens.Max(s => s. Bounds.Right);
            int maxY = screens.Max(s => s.Bounds.Bottom);

            int totalWidth = maxX - minX;
            int totalHeight = maxY - minY;

            // 计算缩放比例，使所有屏幕适应控件大小
            double availableWidth = Math.Max(this.ActualWidth - 40, 200);
            double availableHeight = Math. Max(this.ActualHeight - 40, 100);

            double scaleX = availableWidth / totalWidth;
            double scaleY = availableHeight / totalHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.85; // 留一些边距

            // 计算居中偏移
            double offsetX = (this.ActualWidth - totalWidth * scale) / 2 - minX * scale;
            double offsetY = (this.ActualHeight - totalHeight * scale) / 2 - minY * scale;

            // 创建每个显示器的可视化元素
            for (int i = 0; i < screens.Count; i++)
            {
                var screen = screens[i];
                var bounds = screen.Bounds;

                // 计算缩放后的位置和大小
                double x = bounds.Left * scale + offsetX;
                double y = bounds.Top * scale + offsetY;
                double width = bounds.Width * scale;
                double height = bounds.Height * scale;

                var item = new MonitorItem(screen, i + 1, x, y, width, height);
                item.MouseDown += MonitorItem_MouseDown;
                
                _monitorItems.Add(item);
                this.Children.Add(item);
            }

            // 默认选择第一个非主显示器，或者主显示器
            var defaultScreen = screens.FirstOrDefault(s => ! s.Primary) ?? screens. FirstOrDefault();
            if (defaultScreen != null && SelectedScreen == null)
            {
                SelectedScreen = defaultScreen;
            }
            else if (SelectedScreen != null)
            {
                SelectScreen(SelectedScreen);
            }
        }

        private void MonitorItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is MonitorItem item)
            {
                SelectItem(item);
                SelectedScreen = item.Screen;
                ScreenSelected?.Invoke(this, item.Screen);
            }
        }

        private void SelectScreen(Screen screen)
        {
            var item = _monitorItems.FirstOrDefault(m => m.Screen. DeviceName == screen. DeviceName);
            if (item != null)
            {
                SelectItem(item);
            }
        }

        private void SelectItem(MonitorItem item)
        {
            // 取消之前的选择
            if (_selectedItem != null)
            {
                _selectedItem.IsSelected = false;
            }

            // 设置新的选择
            _selectedItem = item;
            _selectedItem. IsSelected = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base. OnRenderSizeChanged(sizeInfo);
            RefreshMonitors();
        }
    }

    /// <summary>
    /// 单个显示器的可视化项
    /// </summary>
    public class MonitorItem : Border
    {
        public Screen Screen { get; }
        public int MonitorNumber { get; }

        private readonly TextBlock _numberText;
        private readonly Border _innerBorder;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                UpdateVisualState();
            }
        }

        public MonitorItem(Screen screen, int number, double x, double y, double width, double height)
        {
            Screen = screen;
            MonitorNumber = number;

            // 设置位置和大小
            Canvas.SetLeft(this, x);
            Canvas.SetTop(this, y);
            this.Width = width;
            this.Height = height;

            // 外边框
            this.BorderThickness = new Thickness(2);
            this.CornerRadius = new CornerRadius(4);
            this.Cursor = System.Windows.Input.Cursors.Hand;

            // 内部边框（用于显示主显示器标记）
            _innerBorder = new Border
            {
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(1)
            };

            // 显示器编号
            _numberText = new TextBlock
            {
                Text = number.ToString(),
                FontSize = Math.Min(width, height) * 0.3,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _innerBorder.Child = _numberText;
            this.Child = _innerBorder;

            // 添加工具提示
            var resolution = $"{screen. Bounds.Width} x {screen. Bounds.Height}";
            var primaryText = screen.Primary ? " (主显示器)" : "";
            this.ToolTip = $"显示器 {number}{primaryText}\n{screen.DeviceName}\n分辨率: {resolution}";

            UpdateVisualState();

            // 鼠标悬停效果
            this. MouseEnter += (s, e) =>
            {
                if (! IsSelected)
                {
                    this.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    this.Opacity = 0.9;
                }
            };

            this.MouseLeave += (s, e) =>
            {
                if (!IsSelected)
                {
                    UpdateVisualState();
                }
                this.Opacity = 1.0;
            };
        }

        private void UpdateVisualState()
        {
            if (IsSelected)
            {
                // 选中状态 - 蓝色
                this.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 103, 192));
                _innerBorder.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                _numberText. Foreground = Brushes.White;
            }
            else
            {
                // 未选中状态 - 灰色
                this.BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                _innerBorder.Background = new SolidColorBrush(Color.FromRgb(225, 225, 225));
                _numberText.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            }
        }
    }
}