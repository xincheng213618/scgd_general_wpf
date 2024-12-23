using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectKB
{
    public class KBInstance
    {
        public static ProjectKBWindow WindowInstance { get; set; }
    }

    public class ProjectKBPlugin: IProjectBase
    {
        public  override string? Header => "键盘测试";
        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectKB";

        public override void Execute()
        {
            if (KBInstance.WindowInstance == null)
            {
                KBInstance.WindowInstance = new ProjectKBWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                KBInstance.WindowInstance.Closed += (s, e) => KBInstance.WindowInstance = null;
                KBInstance.WindowInstance.Show();
            }
            else
            {
                KBInstance.WindowInstance.Activate();
            }
        }
    }

    public class ProjectKBExport : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => nameof(ProjectKBExport);

        public int Order => 100;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "键盘测试";
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        public void Execute()
        {
            if (KBInstance.WindowInstance == null)
            {
                KBInstance.WindowInstance = new ProjectKBWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                KBInstance.WindowInstance.Closed += (s, e) => KBInstance.WindowInstance = null;
                KBInstance.WindowInstance.Show();
            }
            else
            {
                KBInstance.WindowInstance.Activate();
            }
        }
    }
}
