using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace ColorVision.Themes.Controls
{
    public class WindowNotifications
    {
        /// <summary>
        /// Sent to a window when its nonclient area needs to be changed to indicate an active or inactive state.
        /// </summary>
        public const int WMNCACTIVATE = 0x0086;
        public const uint MFBYCOMMAND = 0x00000000;
        public const uint MFGRAYED = 0x00000001;
        public const uint MFENABLED = 0x00000000;
        public const uint SCCLOSE = 0xF060;
        public const int WMSHOWWINDOW = 0x00000018;
        public const int WMCLOSE = 0x10;
    }


    public partial class BaseWindow : Window
    {
        private static readonly Uri BaseWindowResourceUri = new("/ColorVision.Themes;component/Themes/Window/BaseWindow.xaml", UriKind.Relative);

        private HwndSource? _hwndSource;
        private ThemeChangedHandler? _themeChangedHandler;
        private WindowAccentCompositor? _windowAccentCompositor;

        private static Style? GetDefaultStyle()
        {
            if (Application.Current.TryFindResource(typeof(BaseWindow)) is Style style)
            {
                return style;
            }

            if (Application.LoadComponent(BaseWindowResourceUri) is not ResourceDictionary dictionary)
            {
                return null;
            }

            Application.Current.Resources.MergedDictionaries.Add(dictionary);
            return Application.Current.FindResource(typeof(BaseWindow)) as Style;
        }



        static BaseWindow()
        {
            StyleProperty.OverrideMetadata(typeof(BaseWindow), new FrameworkPropertyMetadata(GetDefaultStyle()));
        }

        public BaseWindow()
        {
            CommandInitialized();

            Closing += (sender, e) => Owner?.Activate();
            Loaded += BaseWindow_Loaded;
        }

        private void BaseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= BaseWindow_Loaded;
            InitializeBlur();
        }

        private void InitializeBlur()
        {
            if (!IsBlurEnabled || _windowAccentCompositor != null)
                return;

            // Win11 需要启用拖拽命中区，否则自定义边框窗口无法直接拖动。
            IsDragMoveEnabled = true;
            _windowAccentCompositor = new(this, false, color =>
            {
                color.A = 255;
                Background = new SolidColorBrush(color);
            });

            if (IsWin10)
            {
                IsDragMoveEnabled = false;
                WindowStyle = WindowStyle.None;
            }

            ApplyBlurTheme();

            _themeChangedHandler ??= _ => ApplyBlurTheme();
            ThemeManager.Current.CurrentUIThemeChanged += _themeChangedHandler;
        }

        private void ApplyBlurTheme()
        {
            if (_windowAccentCompositor == null)
                return;

            _windowAccentCompositor.Color = GetTransparentColor();
            _windowAccentCompositor.IsEnabled = true;
        }

        public static Color GetTransparentColor() => ThemeManager.Current.CurrentUITheme switch
        {
            Theme.Dark => Color.FromArgb(180, 0, 0, 0),
            Theme.UseSystem or Theme.Light or _ => Color.FromArgb(200, 255, 255, 255),
        };


        public static SolidColorBrush GetThemeBackGround() => ThemeManager.Current.CurrentUITheme switch
        {
            Theme.Dark => Brushes.Black,
            Theme.UseSystem or Theme.Light or _ => Brushes.White,
        };

        public static readonly bool IsWin11 = Environment.OSVersion.Version >= new Version(10, 0, 21996);
        public static readonly bool IsWin10 = !(Environment.OSVersion.Version >= new Version(10, 0, 21996)) && Environment.OSVersion.Version >= new Version(10, 0);


        public static float Dpi { get => DpiX; }

        public static float DpiX
        {
            get
            {
                using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
                return graphics.DpiX;
            }
        }

        public static float DpiY
        {
            get
            {
                using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
                return graphics.DpiY;
            }
        }


        // Using a DependencyProperty as the backing store for IsWindowBlurEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsBlurEnabledProperty =
            DependencyProperty.Register(nameof(IsBlurEnabled), typeof(bool), typeof(BaseWindow), new PropertyMetadata(false));

        public bool IsBlurEnabled
        {
            get => (bool)GetValue(IsBlurEnabledProperty);
            set => SetValue(IsBlurEnabledProperty, value);
        }




        public static readonly DependencyProperty IsDragMoveEnabledProperty = DependencyProperty.Register(
            "IsDragMoveEnabled", typeof(bool), typeof(BaseWindow), new PropertyMetadata(false));

        public bool IsDragMoveEnabled
        {
            get => (bool)GetValue(IsDragMoveEnabledProperty);
            set => SetValue(IsDragMoveEnabledProperty, value);
        }


        public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(
            nameof(ShowTitle), typeof(bool), typeof(BaseWindow), new PropertyMetadata(true));

        public bool ShowTitle
        {
            get => (bool)GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, value);
        }

        public static readonly DependencyProperty ShowIconProperty = DependencyProperty.Register(
            nameof(ShowIcon), typeof(bool), typeof(BaseWindow), new PropertyMetadata(true));

        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }



        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (IsDragMoveEnabled && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }


        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (IsBlurEnabled && e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }


        #region 快捷键
        public static RoutedCommand WindowTopMost { get; set; } = new RoutedUICommand("WindowTopMost", "Full", typeof(BaseWindow), new InputGestureCollection());
        #endregion

        public virtual void CommandInitialized()
        {
            CommandBindings.Add(new CommandBinding(WindowTopMost, ExecutedCommand, CanExecuteCommand));

            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, CloseWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, MaximizeWindow, CanResizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, MinimizeWindow, CanMinimizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, RestoreWindow, CanResizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.ShowSystemMenuCommand, ShowSystemMenu));
        }

        // https://www.cnblogs.com/dino623/p/problems_of_WindowChrome.html
        //解决WindowsChrome在设置SizeToContent的时候
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (SizeToContent == SizeToContent.WidthAndHeight && WindowChrome.GetWindowChrome(this) != null)
            {
                InvalidateMeasure();
            }
            nint handle = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource?.AddHook(WndProc);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_themeChangedHandler != null)
            {
                ThemeManager.Current.CurrentUIThemeChanged -= _themeChangedHandler;
                _themeChangedHandler = null;
            }

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            _windowAccentCompositor = null;
            base.OnClosed(e);
        }

        nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            //if (msg == WM_SHOWWINDOW)
            //{
            //    IntPtr hMenu = GetSystemMenu(hwnd, false);
            //    if (hMenu != IntPtr.Zero)
            //    {
            //        EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            //    }
            //}
            if (msg == WindowNotifications.WMCLOSE)
            {
                if (!BaseClosed)
                {
                    BaseClose();
                    handled = !BaseClosed;
                }

            }
            return IntPtr.Zero;
        }




        public virtual void CanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void CanResizeWindow(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;
        }

        private void CanMinimizeWindow(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ResizeMode != ResizeMode.NoResize;
        }


        private void CloseWindow(object sender, ExecutedRoutedEventArgs e)
        {
            BaseClose();
        }

        public bool BaseClosed { get; set; }
        /// <summary>
        /// 封装一层，允许在关闭窗口之前增加一些操作
        /// </summary>
        public virtual void BaseClose()
        {
            BaseClosed = true;
            Close();
        }



        private void MaximizeWindow(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);

        }

        private void MinimizeWindow(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void RestoreWindow(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
        }


        private void ShowSystemMenu(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.OriginalSource is not FrameworkElement element)
                return;

            var point = WindowState == WindowState.Maximized ? new Point(0, element.ActualHeight)
                : new Point(Left + BorderThickness.Left, element.ActualHeight + Top + BorderThickness.Top);
            point = element.TransformToAncestor(this).Transform(point);
            SystemCommands.ShowSystemMenu(this, point);
        }

        public virtual void ExecutedCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == WindowTopMost)
            {
                Topmost = !Topmost;
            }
        }
    }
}
