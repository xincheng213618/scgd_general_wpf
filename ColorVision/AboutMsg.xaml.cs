﻿using ColorVision.Properties;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.CUDA;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVision
{
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
            Topmost = true;
            CloseButton.Focus();
            #if (DEBUG == true)
            TextBlockVision.Text = $"ColorVision{(DebugBuild(Assembly.GetExecutingAssembly())? " (Debug) " : "(Release)")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) - {Assembly.GetExecutingAssembly().GetName().Version} -.NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
#else
            TextBlockVision.Text = $"ColorVision{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug)" : "")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位 -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} -.NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd})";
#endif
            Icon = null;

            TextBlockVision.Text += Environment.NewLine + SystemHelper.LocalCpuInfo + "   " + SystemHelper.GetTotalPhysicalMemory();

            if (ConfigCuda.Instance.IsCudaSupported && ConfigCuda.Instance.DeviceCount >0)
            {
                TextCUDAVision.Visibility = Visibility.Visible;
                TextCUDAVision.Text = $"{ConfigCuda.Instance.DeviceNames[0]} - {ConfigCuda.Instance.ComputeCapabilities[0]} - { (double)(ConfigCuda.Instance.TotalMemories[0])/ (1024.0 * 1024 * 1024):F2} GB";
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
            ImageLogo.RenderTransform = translateTransform;

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
            Process.Start("explorer", "https://www.color-vision.com.cn/");
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
