using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Common.Utilities
{
    /// <summary>Transient exit hint and a mouse-accessible exit button at the top edge.</summary>
    internal sealed class WindowFullScreenOverlay : IDisposable
    {
        private readonly Window window;
        private readonly Func<bool> isCurrent;
        private readonly Popup popup;
        private readonly Border hint;
        private readonly DispatcherTimer hintTimer;
        private bool disposed;

        public WindowFullScreenOverlay(Window window, Func<bool> isCurrent, Action exit)
        {
            this.window = window;
            this.isCurrent = isCurrent;
            string hintText = Properties.Resources.FullScreenExitHint;
            string exitText = Properties.Resources.ExitFullScreen;
            hint = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 43, 48)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(18, 10, 18, 10),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock { Text = hintText, Foreground = Brushes.White, FontSize = 14 }
            };
            var button = new Button
            {
                Content = "×", Width = 44, Height = 44, FontSize = 30,
                Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(70, 73, 79)),
                HorizontalAlignment = HorizontalAlignment.Center, ToolTip = exitText,
                Focusable = false,
                Template = (ControlTemplate)XamlReader.Parse("""
                    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
                      <Border x:Name="Surface" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Background="{TemplateBinding Background}" CornerRadius="22">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="0,-3,0,0" />
                      </Border>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Background" Value="#FF656A73" /></Trigger>
                        <Trigger Property="IsPressed" Value="True"><Setter TargetName="Surface" Property="Background" Value="#FF30343B" /></Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                    """)
            };
            AutomationProperties.SetName(button, exitText);
            button.Click += (_, _) => exit();
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(hint);
            panel.Children.Add(button);
            popup = new Popup
            {
                PlacementTarget = window, Placement = PlacementMode.Custom,
                AllowsTransparency = true, StaysOpen = true, Child = panel,
                CustomPopupPlacementCallback = (size, target, offset) =>
                    [new CustomPopupPlacement(new Point((target.Width - size.Width) / 2, 8), PopupPrimaryAxis.Horizontal)]
            };
            hintTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.FromSeconds(3) };
            hintTimer.Tick += (_, _) => Hide();
            window.PreviewMouseMove += OnMouseMove;
            window.Deactivated += OnDeactivated;
            window.Activated += OnActivated;
            window.SizeChanged += OnSizeChanged;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!disposed && isCurrent() && window.IsActive)
                {
                    hint.Visibility = Visibility.Visible;
                    popup.IsOpen = true;
                    hintTimer.Start();
                }
            }));
        }

        public void Hide()
        {
            hintTimer.Stop();
            hint.Visibility = Visibility.Collapsed;
            popup.IsOpen = false;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!isCurrent() || !window.IsActive)
                return;
            double y = e.GetPosition(window).Y;
            if (y <= 4 && e.LeftButton == MouseButtonState.Released)
            {
                hintTimer.Stop();
                hint.Visibility = Visibility.Collapsed;
                popup.IsOpen = true;
            }
            else if (y > 90 && !popup.IsMouseOver && !hintTimer.IsEnabled)
                popup.IsOpen = false;
        }

        private void OnDeactivated(object? sender, EventArgs e) => Hide();
        private void OnActivated(object? sender, EventArgs e) { if (isCurrent()) Hide(); }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Hide();

        public void Dispose()
        {
            disposed = true;
            Hide();
            window.PreviewMouseMove -= OnMouseMove;
            window.Deactivated -= OnDeactivated;
            window.Activated -= OnActivated;
            window.SizeChanged -= OnSizeChanged;
            popup.Child = null;
            popup.PlacementTarget = null;
        }
    }
}
