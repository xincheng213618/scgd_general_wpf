using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public class ButtonProgressBar:IDisposable
    {
        public ProgressBar ProgressBar { get; set; }
        public Button Button { get; set; }

        public ButtonProgressBar(ProgressBar progressBar, Button button)
        {
            ProgressBar = progressBar;
            Button = button;

            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(10); // 定时器间隔
            Timer.Tick += Run_Tick;
        }

        public void Start()
        {
            Button.Visibility = Visibility.Hidden;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = 1;

            ProgressBar.Value = 0;
            _startTime = DateTime.Now;
            Timer.Start();
        }

        public void Stop()
        {
            Button.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 1;
            Timer.Stop();
            Elapsed = (DateTime.Now - _startTime).TotalMilliseconds;
        }
        public double Elapsed { get; set; }

        DateTime _startTime;
        DispatcherTimer Timer;
        public double TargetTime { get; set; }

        void Run_Tick(object? sender, EventArgs e)
        {
            var elapsed = (DateTime.Now - _startTime).TotalMilliseconds;
            if (elapsed >= TargetTime)
            {
                ProgressBar.Value = 99;
            }
            else
            {
                ProgressBar.Value = (elapsed / TargetTime) * 100;
            }
        }

        public void Dispose()
        {
            Timer.Tick -= Run_Tick;
            Timer.Stop();
            Timer = null;
            GC.SuppressFinalize(this);
        }
    }
}

