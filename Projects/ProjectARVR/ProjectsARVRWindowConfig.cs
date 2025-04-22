using ColorVision.UI;
using System.Collections.ObjectModel;
using System.Windows;

namespace ProjectARVR
{
    public class ProjectsARVRWindowConfig : IConfig
    {
        public static ProjectsARVRWindowConfig Instance => ConfigService.Instance.GetRequiredService<ProjectsARVRWindowConfig>();
        public ObservableCollection<KBItemMaster> ViewResluts { get; set; } = new ObservableCollection<KBItemMaster>();

        public bool IsRestoreWindow { get; set; } = true;
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowState { get; set; }


        public void SetWindow(Window window)
        {
            if (IsRestoreWindow && Height != 0 && Width != 0)
            {
                window.Top = Top;
                window.Left = Left;
                window.Height = Height;
                window.Width = Width;
                window.WindowState = (WindowState)WindowState;

                if (Width > SystemParameters.WorkArea.Width)
                {
                    window.Width = SystemParameters.WorkArea.Width;
                }
                if (Height > SystemParameters.WorkArea.Height)
                {
                    window.Height = SystemParameters.WorkArea.Height;
                }
            }
        }
        public void SetConfig(Window window)
        {
            Top = window.Top;
            Left = window.Left;
            Height = window.Height;
            Width = window.Width;
            WindowState = (int)window.WindowState;
        }
    }
}