using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects.ProjectShiYuan
{

    public class ShiyuanProjectPlugin : IProjectBase
    {
        public override string? Header => "ProjectShiyuan";
        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectShiyuan";

        public override string Description { get; set; } = "视源项目";
        public override void Execute()
        {
            new ShiyuanProjectWindow() { WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

    public class ShiyuanProjectExport : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "ProjectShiyuan";

        public int Order => 100;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "视源项目";
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
            new ShiyuanProjectWindow() {WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
