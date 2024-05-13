using ColorVision.Media;
using ColorVision.Net;
using ColorVision.Solution;
using ColorVision.Common.Extension;

using HandyControl.Tools;
using HandyControl.Tools.Extension;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ColorVision.Draw;

namespace ColorVision
{

    public partial class MainWindow
    {

        const uint WM_USER = 0x0400; // 用户自定义消息起始值

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GlobalGetAtomName(ushort nAtom, char[] retVal, int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern short GlobalDeleteAtom(short nAtom);


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => {
                if (msg == WM_USER + 1)
                {
                    try
                    {
                        char[] chars = new char[1024];
                        int size = GlobalGetAtomName((ushort)wParam, chars, chars.Length);
                        if (size > 0)
                        {
                            string result = new(chars, 0, size);
                            GlobalDeleteAtom((short)wParam);

                            if (File.Exists(result))
                            {
                                if (result.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
                                {
                                    SolutionManager.GetInstance().OpenSolution(result);
                                }
                                if (result.EndsWith("cvraw", StringComparison.OrdinalIgnoreCase))
                                {
                                    ImageView imageView = new();
                                    CVFileUtil.ReadCVRaw(result, out CVCIEFile fileInfo);
                                    Window window = new() { Title = "快速预览", Owner = Application.Current.MainWindow};
                                    window.Content = imageView;
                                    imageView.OpenImage(fileInfo);

                                    window.Show();
                                    window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() => {
                                        imageView.ToolBarTop.ClearImage();
                                    }));



                                }
                            }
                            else
                            {
                                MessageBox.Show(result, "ColorVision");
                            }
                        }
                    }
                    catch (Exception ex) 
                    { 
                        MessageBox.Show(ex.Message, "ColorVision");
                    }
                }
                return IntPtr.Zero;
            }));
        }


        private GridLength _columnDefinitionWidth;
        private void OnLeftMainContentShiftOut(object sender, RoutedEventArgs e)
        {
            ButtonShiftOut.Collapse();
            GridSplitter.IsEnabled = false;

            double targetValue = -ColumnDefinitionLeft.MaxWidth;
            _columnDefinitionWidth = ColumnDefinitionLeft.Width;

            DoubleAnimation animation = AnimationHelper.CreateAnimation(targetValue, milliseconds: 1);
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += OnAnimationCompleted;
            LeftMainContent.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);
            void OnAnimationCompleted(object? _, EventArgs args)
            {
                animation.Completed -= OnAnimationCompleted;
                LeftMainContent.RenderTransform.SetCurrentValue(TranslateTransform.XProperty, targetValue);

                Grid.SetColumn(MainContent, 0);
                Grid.SetColumnSpan(MainContent, 2);
                ColumnDefinitionLeft.MinWidth = 0;
                ColumnDefinitionLeft.Width = new GridLength(0);
                ButtonShiftIn.Show();
            }
        }

        private void OnLeftMainContentShiftIn(object sender, RoutedEventArgs e)
        {
            ButtonShiftIn.Collapse();
            
            GridSplitter.IsEnabled = true;

            double targetValue = ColumnDefinitionLeft.Width.Value;

            DoubleAnimation animation = AnimationHelper.CreateAnimation(targetValue, milliseconds: 1);
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += OnAnimationCompleted;
            LeftMainContent.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            void OnAnimationCompleted(object? _, EventArgs args)
            {
                animation.Completed -= OnAnimationCompleted;
                LeftMainContent.RenderTransform.SetCurrentValue(TranslateTransform.XProperty, targetValue);

                Grid.SetColumn(MainContent, 1);
                Grid.SetColumnSpan(MainContent, 1);

                ColumnDefinitionLeft.MinWidth = 240;
                ColumnDefinitionLeft.Width = _columnDefinitionWidth;
                ButtonShiftOut.Show();
            }
        }
    }
}
