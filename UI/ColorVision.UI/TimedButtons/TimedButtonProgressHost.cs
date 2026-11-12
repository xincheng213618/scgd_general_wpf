#pragma warning disable CA1513,CA1716,CA1863
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.UI
{
    internal sealed class TimedButtonProgressHost : IDisposable
    {
        private readonly Button _button;
        private readonly Brush? _progressForeground;
        private readonly Grid _host = new Grid();
        private readonly Border _overlay;
        private readonly Grid _layoutRoot;
        private readonly Border _progressTrack;
        private readonly Border _progressFill;
        private readonly TextBlock _baseTextBlock;
        private readonly TextBlock _filledTextBlock;
        private readonly RectangleGeometry _filledTextClip = new RectangleGeometry();
        private bool _isHosted;
        private bool _overlayInserted;
        private double _progressValue;

        public bool IsHosted => _isHosted;

        public TimedButtonProgressHost(Button button, Brush? progressForeground)
        {
            _button = button;
            _progressForeground = progressForeground;
            _isHosted = TryCreateHost();

            _overlay = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Opacity = 1,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            _overlay.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            _overlay.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            _overlay.IsHitTestVisible = false;
            _overlay.SizeChanged += Overlay_SizeChanged;

            _layoutRoot = new Grid
            {
                IsHitTestVisible = false,
                ClipToBounds = true
            };

            _progressTrack = new Border
            {
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(24, 0, 0, 0))
            };
            _progressTrack.IsHitTestVisible = false;

            _progressFill = new Border
            {
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Width = 0,
                Background = _progressForeground ?? Brushes.Red
            };
            _progressFill.IsHitTestVisible = false;

            _baseTextBlock = CreateCenteredTextBlock(_progressForeground ?? Brushes.Red);
            _filledTextBlock = CreateCenteredTextBlock(Brushes.White);
            _filledTextBlock.Clip = _filledTextClip;

            _layoutRoot.Children.Add(_progressTrack);
            _layoutRoot.Children.Add(_progressFill);
            _layoutRoot.Children.Add(_baseTextBlock);
            _layoutRoot.Children.Add(_filledTextBlock);
            _overlay.Child = _layoutRoot;
        }

        private TextBlock CreateCenteredTextBlock(Brush foreground)
        {
            TextBlock textBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textBlock.Foreground = foreground;
            textBlock.FontSize = _button.FontSize;
            textBlock.FontWeight = _button.FontWeight;
            textBlock.FontFamily = _button.FontFamily;
            textBlock.IsHitTestVisible = false;
            return textBlock;
        }

        public void Show()
        {
            if (!_isHosted)
            {
                return;
            }

            if (!_overlayInserted)
            {
                _host.Children.Add(_overlay);
                _overlayInserted = true;
            }
        }

        public void Remove()
        {
            if (_overlayInserted)
            {
                _host.Children.Remove(_overlay);
                _overlayInserted = false;
            }
        }

        public void UpdateProgress(double progressValue, string? text)
        {
            _progressValue = Math.Max(0, Math.Min(99, progressValue));

            string resolvedText = text ?? string.Empty;
            _baseTextBlock.Text = resolvedText;
            _filledTextBlock.Text = resolvedText;
            UpdateProgressVisual();
        }

        private void Overlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateProgressVisual();
        }

        private void UpdateProgressVisual()
        {
            double availableWidth = Math.Max(0, _layoutRoot.ActualWidth);
            double availableHeight = Math.Max(0, _layoutRoot.ActualHeight);
            double filledWidth = availableWidth * (_progressValue / 100d);

            _progressFill.Width = filledWidth;
            _filledTextClip.Rect = new Rect(0, 0, filledWidth, availableHeight);
        }

        private bool TryCreateHost()
        {
            if (_button.Parent == _host)
            {
                return true;
            }

            CopyLayoutProperties(_button, _host);
            // Visibility belongs to the screen, including changes during an operation.
            _host.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(UIElement.Visibility)) { Source = _button, Mode = BindingMode.OneWay });
            PrepareButtonForHost(_button);

            switch (_button.Parent)
            {
                case Panel panel:
                    int childIndex = panel.Children.IndexOf(_button);
                    if (childIndex < 0)
                    {
                        return false;
                    }

                    panel.Children.RemoveAt(childIndex);
                    _host.Children.Add(_button);
                    panel.Children.Insert(childIndex, _host);
                    return true;

                case Decorator decorator when ReferenceEquals(decorator.Child, _button):
                    decorator.Child = null;
                    _host.Children.Add(_button);
                    decorator.Child = _host;
                    return true;

                case ContentControl contentControl when ReferenceEquals(contentControl.Content, _button):
                    contentControl.Content = null;
                    _host.Children.Add(_button);
                    contentControl.Content = _host;
                    return true;

                default:
                    return false;
            }
        }

        private static void PrepareButtonForHost(Button button)
        {
            button.Margin = new Thickness(0);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Stretch;
            button.ClearValue(FrameworkElement.WidthProperty);
            button.ClearValue(FrameworkElement.HeightProperty);
        }

        private static void CopyLayoutProperties(FrameworkElement source, FrameworkElement target)
        {
            target.Margin = source.Margin;
            target.HorizontalAlignment = source.HorizontalAlignment;
            target.VerticalAlignment = source.VerticalAlignment;
            target.Width = source.Width;
            target.Height = source.Height;
            target.MinWidth = source.MinWidth;
            target.MinHeight = source.MinHeight;
            target.MaxWidth = source.MaxWidth;
            target.MaxHeight = source.MaxHeight;

            Grid.SetRow(target, Grid.GetRow(source));
            Grid.SetColumn(target, Grid.GetColumn(source));
            Grid.SetRowSpan(target, Grid.GetRowSpan(source));
            Grid.SetColumnSpan(target, Grid.GetColumnSpan(source));
            DockPanel.SetDock(target, DockPanel.GetDock(source));
            Panel.SetZIndex(target, Panel.GetZIndex(source));
        }

        public void Dispose()
        {
            Remove();
            RestoreButtonParent();
            _overlay.SizeChanged -= Overlay_SizeChanged;
            GC.SuppressFinalize(this);
        }

        private void RestoreButtonParent()
        {
            if (!_isHosted || !ReferenceEquals(_button.Parent, _host))
            {
                return;
            }

            switch (_host.Parent)
            {
                case Panel panel:
                    int childIndex = panel.Children.IndexOf(_host);
                    if (childIndex >= 0)
                    {
                        CopyLayoutProperties(_host, _button);
                        _host.Children.Remove(_button);
                        panel.Children.RemoveAt(childIndex);
                        panel.Children.Insert(childIndex, _button);
                        _isHosted = false;
                    }
                    break;

                case Decorator decorator when ReferenceEquals(decorator.Child, _host):
                    CopyLayoutProperties(_host, _button);
                    _host.Children.Remove(_button);
                    decorator.Child = null;
                    decorator.Child = _button;
                    _isHosted = false;
                    break;

                case ContentControl contentControl when ReferenceEquals(contentControl.Content, _host):
                    CopyLayoutProperties(_host, _button);
                    _host.Children.Remove(_button);
                    contentControl.Content = null;
                    contentControl.Content = _button;
                    _isHosted = false;
                    break;
            }
        }
    }
}
