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
        private static Style? GetDefautlStyle()
        {
            if (Application.Current.TryFindResource(typeof(BaseWindow)) is Style style)
            {
                return style;
            }
            else
            {
                ResourceDictionary dictionary1 = Application.LoadComponent(new Uri("/ColorVision.Themes;component/Themes/Window/BaseWindow.xaml", UriKind.Relative)) as ResourceDictionary;
                Application.Current.Resources.MergedDictionaries.Add(dictionary1);
                return Application.Current.FindResource(typeof(BaseWindow)) as Style ?? null;
            }
        }



        static BaseWindow()
        {
            StyleProperty.OverrideMetadata(typeof(BaseWindow), new FrameworkPropertyMetadata(GetDefautlStyle()));
        }
        public WindowChrome WindowChrome { get; set; }
        public BaseWindow()
        {
            CommandInitialized();

            Closing += (sender, e) => Owner?.Activate();
            WindowChrome = WindowChrome.GetWindowChrome(this);

            Loaded += (s, e) =>
            {
                if (IsBlurEnabled)
                {
                    //Win11这里要开，要不拖不动
                    IsDragMoveEnabled = true;
                    wac = new(this, false, (c) =>
                    {
                        c.A = 255;
                        Background = new SolidColorBrush(c);
                    });

                    if (IsWin10)
                    {
                        IsDragMoveEnabled = false;
                        WindowStyle = WindowStyle.None;
                    }

                    wac.Color = GetTransparentColor();
                    wac.IsEnabled = true;
                    ThemeChangedHandler themeChangedHandler = (s) =>
                    {

                        wac.Color = GetTransparentColor();
                        wac.IsEnabled = true;
                    };
                    ThemeManager.Current.CurrentUIThemeChanged += themeChangedHandler;
                    Closing += (s, e) =>
                    {
                        ThemeManager.Current.CurrentUIThemeChanged -= themeChangedHandler;
                    };
                }
            };
        }

        public static Color GetTransparentColor() => ThemeManager.Current.CurrentUITheme switch
        {
            Theme.Dark => Color.FromArgb(180, 0, 0, 0),
            Theme.Pink => Color.FromArgb(200, 249, 188, 211),// 粉色背景
            Theme.Cyan => Color.FromArgb(200, 202, 224, 228), // 青色背景
            Theme.UseSystem or Theme.Light or _ => Color.FromArgb(200, 255, 255, 255),
        };


        public static SolidColorBrush GetThemeBackGround() => ThemeManager.Current.CurrentUITheme switch
        {
            Theme.Dark => Brushes.Black,
            Theme.Pink => new SolidColorBrush(Color.FromRgb(0xF9, 0xBC, 0xD3)),// 粉色背景
            Theme.Cyan => new SolidColorBrush(Color.FromRgb(0xCA, 0xE0, 0xE4)), // 青色背景
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

        WindowAccentCompositor wac;

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
            HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook(WndProc));
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
