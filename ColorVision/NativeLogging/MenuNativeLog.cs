using ColorVision.UI.Menus;

namespace ColorVision.NativeLogging;

public sealed class MenuNativeLog : GlobalMenuBase
{
    public override string OwnerGuid => MenuItemConstants.Help;

    public override int Order => 10006;

    public override string Header => NativeLogText.Title;

    public override void Execute() => NativeLogWindowService.Show();
}
