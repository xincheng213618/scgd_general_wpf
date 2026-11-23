using ColorVision.UI.Menus;

namespace ColorVision.NativeLogging;

public sealed class MenuNativeLog : GlobalMenuBase
{
    public override string OwnerGuid => MenuItemConstants.View;

    // LogPanel is registered at index 2, giving it view-menu order 62.
    public override int Order => 63;

    public override string Header => LocalizedMenuAccessKey.Format(NativeLogText.Title, 'N');

    public override void Execute() => NativeLogWindowService.Show();
}
