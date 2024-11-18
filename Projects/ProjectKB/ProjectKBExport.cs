using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectKB
{
    public class ProjectKBExport : IMenuItem,IProject
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
            new ProjectKBWindow() {WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
