using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects.ProjectShiYuan
{
    public class ShiyuanProjectExport : IMenuItem,IProject
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "ProjectShiyuan";

        public int Order => 100;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "ProjectShiyuan";
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        public void Execute()
        {
            new ShiyuanProjectWindow() {WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
