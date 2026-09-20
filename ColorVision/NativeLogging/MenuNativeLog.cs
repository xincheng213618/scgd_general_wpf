using ColorVision.UI.Menus;

namespace ColorVision.NativeLogging;

public sealed class MenuNativeLog : GlobalMenuBase
{
    public override string OwnerGuid => MenuItemConstants.Help;

    public override int Order => 10006;

    public override string Header => LocalizedMenuAccessKey.Format(NativeLogText.Title, 'N');

    public override void Execute() => NativeLogWindowService.Show();
}
