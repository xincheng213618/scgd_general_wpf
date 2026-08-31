using System.Windows.Input;

using ColorVision.UI.Properties;
using System.Windows;
using ColorVision.UI.HotKey;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuSaveAs : MenuItemFileBase, IHotKey
    {
        public override string GuidId => nameof(MenuSaveAs);
        public override string Header => Resources.MenuSaveAs;
        public override int Order => 30;
        public override ICommand Command => ApplicationCommands.SaveAs;
        public override string InputGestureText => "Ctrl+Shift+S";
        public HotKeys HotKeys => new(FileHotkeyText.SaveAs, new Hotkey(Key.S, ModifierKeys.Control | ModifierKeys.Shift), Execute)
        {
            Description = FileHotkeyText.SaveAsDescription,
            Category = FileHotkeyText.Category
        };

        public override void Execute()
        {
            IInputElement? target = Keyboard.FocusedElement ?? Application.Current?.MainWindow;
            if (target != null && ApplicationCommands.SaveAs.CanExecute(null, target))
                ApplicationCommands.SaveAs.Execute(null, target);
        }
    }

}
