using ColorVision.Properties;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.CUDA;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ColorVision
{
    internal sealed class AboutTextShape : Shape
    {
        private double _height;
        private Geometry _textGeometry = Geometry.Empty;
        private double _width;

        protected sealed override Geometry DefiningGeometry => _textGeometry;

        protected override Size MeasureOverride(Size constraint)
        {
            RealizeGeometry();
            return new Size(Math.Min(constraint.Width, _width), Math.Min(constraint.Height, _height));
        }

        private void RealizeGeometry()
        {
            var formattedText = CreateFormattedText(Text);
            _height = formattedText.Height;
            _width = formattedText.Width;
            _textGeometry = formattedText.BuildGeometry(new Point());

            if (Text == " ")
                _width = CreateFormattedText("_").Width;
        }

        private FormattedText CreateFormattedText(string text)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                Brushes.Black,
                100);
        }

        public static readonly DependencyProperty FontFamilyProperty =
            TextElement.FontFamilyProperty.AddOwner(typeof(AboutTextShape));

        public static readonly DependencyProperty FontSizeProperty =
            TextElement.FontSizeProperty.AddOwner(typeof(AboutTextShape));

        public static readonly DependencyProperty FontStretchProperty =
            TextElement.FontStretchProperty.AddOwner(typeof(AboutTextShape));

        public static readonly DependencyProperty FontStyleProperty =
            TextElement.FontStyleProperty.AddOwner(typeof(AboutTextShape));

        public static readonly DependencyProperty FontWeightProperty =
            TextElement.FontWeightProperty.AddOwner(typeof(AboutTextShape));

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(AboutTextShape),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender));

        [Localizability(LocalizationCategory.Font)]
        public FontFamily FontFamily
        {
            get => (FontFamily)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        [TypeConverter(typeof(FontSizeConverter))]
        [Localizability(LocalizationCategory.None)]
        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontStretch FontStretch
        {
            get => (FontStretch)GetValue(FontStretchProperty);
            set => SetValue(FontStretchProperty, value);
        }

        public FontStyle FontStyle
        {
            get => (FontStyle)GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }

        public FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        [Localizability(LocalizationCategory.Text)]
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }

    public class AboutMsgExport : MenuItemBase,IMenuItem
    {
        public HotKeys HotKeys => new HotKeys(Resources.About, new Hotkey(Key.F1, ModifierKeys.Control), Execute);

        public override string OwnerGuid => "Help";
        public override string GuidId => "AboutMsg";

        public override int Order => 100000;
        public override string Header => Resources.MenuAbout;

        public override string InputGestureText => "Ctrl + F1";

        public override void Execute()
        {
            new AboutMsgWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }


    //public class MenuRbacManager : IRightMenuItemProvider
    //{
    //    public IEnumerable<MenuItemMetadata> GetMenuItems()
    //    {
    //        MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
    //        menuItemMetadata.Command = new RelayCommand(a => new AboutMsgWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
    //        var Image = new System.Windows.Controls.Image();
    //        Image.Source = new BitmapImage(new Uri("/ColorVision;component/Assets/Image/ColorVision.ico", UriKind.Relative));
    //        menuItemMetadata.Icon = Image;
    //        menuItemMetadata.Order = 999;
    //        return new MenuItemMetadata[] { menuItemMetadata };
    //    }
    //}


    /// <summary>
    /// AboutMsg.xaml 的交互逻辑
    /// </summary>
    public partial class AboutMsgWindow : BaseWindow
    {
        public AboutMsgWindow()
        {
            InitializeComponent();
            IsBlurEnabled = ThemeConfig.Instance.TransparentWindow && IsBlurEnabled;
            Background = IsBlurEnabled ? Background : Brushes.Gray;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            Topmost = true;
            CloseButton.Focus();
            #if (DEBUG == true)
            TextBlockVision.Text = $"ColorVision{(DebugBuild(Assembly.GetExecutingAssembly())? " Debug " : "Release")}{(Debugger.IsAttached ? $" ({Properties.Resources.Debugging}) " : "")} {(IntPtr.Size == 4 ? "32" : "64")}{Properties.Resources.Bit}) - {version} -.NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
#else
            TextBlockVision.Text = $"ColorVision{(DebugBuild(Assembly.GetExecutingAssembly()) ? " Debug" : "")}{(Debugger.IsAttached ? $" ({Properties.Resources.Debugging}) " : "")} {(IntPtr.Size == 4 ? "32" : "64")}{Properties.Resources.Bit} -  {version} -.NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd}";
#endif
            Icon = null;
            VersionTextShape.Text = version?.ToString() ?? string.Empty;
            TextBlockVision.Text += Environment.NewLine + SystemHelper.LocalCpuInfo.TrimEnd() + " " + SystemHelper.GetTotalPhysicalMemory();
            TextBoxHardwareId.Text = SystemHelper.GetHardwareId();

            if (ConfigCuda.Instance.IsCudaSupported && ConfigCuda.Instance.DeviceCount >0)
            {
                TextCUDAVision.Visibility = Visibility.Visible;
                TextCUDAVision.Text = $"{ConfigCuda.Instance.DeviceNames[0]} { (ConfigCuda.Instance.TotalMemories[0])/ (1024.0 * 1024 * 1024):F0} GB - {ConfigCuda.Instance.ComputeCapabilities[0]}" ;
            }

            //LogoText.Text = string.Join(" ", ArgumentParser.GetInstance().CommandLineArgs.Select(arg => $"\"{arg}\""));
            Grid1.Background = RainbowAnimation();
            Deactivated += (s, e) =>
            {
                try
                {
                    Close();
                }
                catch 
                {

                }
            };

            DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFramesX = new();
            doubleAnimationUsingKeyFramesX.Duration = TimeSpan.FromSeconds(0.8);
            doubleAnimationUsingKeyFramesX.RepeatBehavior = RepeatBehavior.Forever;
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.025))));
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.05))));
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.075))));
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(-1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.125))));
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(-1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.15))));
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(-1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.175))));
            doubleAnimationUsingKeyFramesX.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2))));
            AnimationClock myClockX = doubleAnimationUsingKeyFramesX.CreateClock();

            DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFramesY = new();
            doubleAnimationUsingKeyFramesY.Duration = TimeSpan.FromSeconds(0.2);
            doubleAnimationUsingKeyFramesY.RepeatBehavior = RepeatBehavior.Forever;
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.025))));
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.05))));
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(-1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.075))));
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(-1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(-1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.125))));
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.15))));
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.175))));
            doubleAnimationUsingKeyFramesY.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2))));
            AnimationClock myClockY = doubleAnimationUsingKeyFramesY.CreateClock();



            TranslateTransform translateTransform = new();
            translateTransform.ApplyAnimationClock(TranslateTransform.XProperty, myClockX);
            translateTransform.ApplyAnimationClock(TranslateTransform.YProperty, myClockY);
            //ImageLogo.RenderTransform = translateTransform;

        }

        private static double GetRandomNumber(double minimum, double maximum)
        {
            Random random = new();
            return random.NextDouble() * (maximum - minimum) + minimum;
        }

        private static SolidColorBrush RainbowAnimation()
        {
            Color[] colors = { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.DarkGreen, Colors.Blue, Colors.Violet };
            ColorAnimationUsingKeyFrames colorAnimation = new();
            colorAnimation.Duration = TimeSpan.FromSeconds(7*0.3);
            colorAnimation.FillBehavior = FillBehavior.Stop;
            colorAnimation.RepeatBehavior = RepeatBehavior.Forever;
            int z = 0;
            foreach (var item in colors)
            {
                colorAnimation.KeyFrames.Add(new LinearColorKeyFrame(item, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(z*0.3))));
                z++;
            }

            SolidColorBrush background = new();
            AnimationClock myClock = colorAnimation.CreateClock();
            background.ApplyAnimationClock(SolidColorBrush.ColorProperty, myClock);
            return background;
        }


        private static bool DebugBuild(Assembly assembly)
        {
            foreach (object attribute in assembly.GetCustomAttributes(false))
            {
                if (attribute is DebuggableAttribute _attribute)
                {
                    return _attribute.IsJITTrackingEnabled;
                }
            }
            return false;
        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void BaseWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void CloseButton_MouseLeave(object sender, MouseEventArgs e)
        {
            Cursor = Cursors.Arrow;
        }

        private void CloseButton_MouseEnter(object sender, MouseEventArgs e)
        {
            Cursor = Cursors.Hand;
        }
    }
}
