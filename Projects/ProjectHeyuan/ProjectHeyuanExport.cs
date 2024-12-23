using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects.ProjectHeyuan
{
    public class ProjectHeyuanPlugin : IProjectBase
    {
        public override string? Header => "河源精电";
        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectHeyuan";

        public override void Execute()
        {
            new ProjectHeyuanWindow() { WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    public class ProjectHeyuanExport : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "HeYuan";

        public int Order => 100;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "河源精电";
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        public void Execute()
        {
            new ProjectHeyuanWindow() {WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
