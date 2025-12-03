using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace ImageProjector
{
    /// <summary>
    /// Visual monitor layout selection control
    /// </summary>
    public class MonitorLayoutControl : Canvas
    {
        private readonly List<MonitorItem> _monitorItems = new();
        private MonitorItem? _selectedItem;

        public event EventHandler<Screen>? ScreenSelected;

        public static readonly DependencyProperty SelectedScreenProperty =
            DependencyProperty.Register(nameof(SelectedScreen), typeof(Screen), typeof(MonitorLayoutControl),
                new PropertyMetadata(null, OnSelectedScreenChanged));

        public Screen? SelectedScreen
        {
            get => (Screen?)GetValue(SelectedScreenProperty);
            set => SetValue(SelectedScreenProperty, value);
        }

        private static void OnSelectedScreenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MonitorLayoutControl control && e.NewValue is Screen screen)
            {
                control.SelectScreen(screen);
            }
        }

        public MonitorLayoutControl()
        {
            this.Background = Brushes.Transparent;
            this.MinHeight = 150;
            this.Loaded += (s, e) => RefreshMonitors();
        }

        public void RefreshMonitors()
        {
            this.Children.Clear();
            _monitorItems.Clear();

            var screens = Screen.AllScreens.ToList();
            if (!screens.Any()) return;

            // Calculate bounds of all screens
            int minX = screens.Min(s => s.Bounds.Left);
            int minY = screens.Min(s => s.Bounds.Top);
            int maxX = screens.Max(s => s.Bounds.Right);
            int maxY = screens.Max(s => s.Bounds.Bottom);

            int totalWidth = maxX - minX;
            int totalHeight = maxY - minY;

            // Calculate scale to fit all screens in control
            double availableWidth = Math.Max(this.ActualWidth - 40, 200);
            double availableHeight = Math.Max(this.ActualHeight - 40, 100);

            double scaleX = availableWidth / totalWidth;
            double scaleY = availableHeight / totalHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.85; // Leave some margin

            // Calculate centering offset
            double offsetX = (this.ActualWidth - totalWidth * scale) / 2 - minX * scale;
            double offsetY = (this.ActualHeight - totalHeight * scale) / 2 - minY * scale;

            // Create visual element for each monitor
            for (int i = 0; i < screens.Count; i++)
            {
                var screen = screens[i];
                var bounds = screen.Bounds;

                // Calculate scaled position and size
                double x = bounds.Left * scale + offsetX;
                double y = bounds.Top * scale + offsetY;
                double width = bounds.Width * scale;
                double height = bounds.Height * scale;

                var item = new MonitorItem(screen, i + 1, x, y, width, height);
                item.MouseDown += MonitorItem_MouseDown;
                
                _monitorItems.Add(item);
                this.Children.Add(item);
            }

            // Default to first non-primary monitor, or primary
            var defaultScreen = screens.FirstOrDefault(s => !s.Primary) ?? screens.FirstOrDefault();
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
            var item = _monitorItems.FirstOrDefault(m => m.Screen.DeviceName == screen.DeviceName);
            if (item != null)
            {
                SelectItem(item);
            }
        }

        private void SelectItem(MonitorItem item)
        {
            // Deselect previous
            if (_selectedItem != null)
            {
                _selectedItem.IsSelected = false;
            }

            // Select new
            _selectedItem = item;
            _selectedItem.IsSelected = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            RefreshMonitors();
        }
    }

    /// <summary>
    /// Visual item for a single monitor
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

            // Set position and size
            Canvas.SetLeft(this, x);
            Canvas.SetTop(this, y);
            this.Width = width;
            this.Height = height;

            // Outer border
            this.BorderThickness = new Thickness(2);
            this.CornerRadius = new CornerRadius(4);
            this.Cursor = System.Windows.Input.Cursors.Hand;

            // Inner border (for primary monitor marker)
            _innerBorder = new Border
            {
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(1)
            };

            // Monitor number
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

            // Add tooltip
            var resolution = $"{screen.Bounds.Width} x {screen.Bounds.Height}";
            var primaryText = screen.Primary ? " (Primary)" : "";
            this.ToolTip = $"Monitor {number}{primaryText}\n{screen.DeviceName}\nResolution: {resolution}";

            UpdateVisualState();

            // Mouse hover effect
            this.MouseEnter += (s, e) =>
            {
                if (!IsSelected)
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
                // Selected state - blue
                this.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 103, 192));
                _innerBorder.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                _numberText.Foreground = Brushes.White;
            }
            else
            {
                // Unselected state - gray
                this.BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                _innerBorder.Background = new SolidColorBrush(Color.FromRgb(225, 225, 225));
                _numberText.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            }
        }
    }
}
