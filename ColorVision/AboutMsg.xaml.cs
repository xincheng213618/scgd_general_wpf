
using ColorVision.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVision
{
    /// <summary>
    /// AboutMsg.xaml 的交互逻辑
    /// </summary>
    public partial class AboutMsgWindow : BaseWindow
    {
        public AboutMsgWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Topmost = true;
            CloseButton.Focus();
#if (DEBUG == true)
            TextBlockVision.Text = $"ColorVision{(DebugBuild(Assembly.GetExecutingAssembly())? " (Debug) " : "(Release)")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
#else
            TextBlockVision.Text = $"ColorVision{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug)" : "")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
#endif
            Icon = null;


            Grid1.Background = RainbowAnimation();
            this.Deactivated += (s, e) =>
            {
                try
                {
                    this.Close();
                }
                catch 
                {

                }
            };


            //for (int i = 0; i < 100; i++)
            //{
            //    Grid1.Background = Brushes.Red;
            //    await Task.Delay(350);
            //    Grid1.Background = Brushes.DarkRed;
            //    await Task.Delay(350);
            //    Grid1.Background = Brushes.Orange;
            //    await Task.Delay(350);
            //    Grid1.Background = Brushes.Yellow;
            //    await Task.Delay(350);
            //    Grid1.Background = Brushes.Green;
            //    await Task.Delay(350);
            //    Grid1.Background = Brushes.DarkGreen;
            //    await Task.Delay(350);
            //    Grid1.Background = Brushes.Blue;
            //    await Task.Delay(350);
            //    Grid1.Background = Brushes.DarkBlue;
            //    await Task.Delay(350);
            //    Grid1.Background = Brushes.Violet;
            //    await Task.Delay(350);
            //}

        }

        private SolidColorBrush RainbowAnimation()
        {
            Color[] colors = { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.DarkGreen, Colors.Blue, Colors.Violet };
            ColorAnimationUsingKeyFrames colorAnimation = new ColorAnimationUsingKeyFrames();
            colorAnimation.Duration = TimeSpan.FromSeconds(7*0.3);
            colorAnimation.FillBehavior = FillBehavior.Stop;
            colorAnimation.RepeatBehavior = RepeatBehavior.Forever;
            int z = 0;
            foreach (var item in colors)
            {
                colorAnimation.KeyFrames.Add(new LinearColorKeyFrame(item, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(z*0.3))));
                z++;
            }

            SolidColorBrush background = new SolidColorBrush();
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
            this.Close();
        }

        private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("explorer", "https://www.color-vision.com.cn/");
            this.Close();
        }

        private void BaseWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
