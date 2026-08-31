using ColorVision.UI.Properties;
using System.Windows.Input;
using ColorVision.UI.HotKey;
using System.Windows;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuSave : MenuItemFileBase, IHotKey
    {

        public override string GuidId => nameof(MenuSave);
        public override string Header => Resources.MenuSave;
        public override int Order => 30;
        public override string InputGestureText => "Ctrl+S";
        public override ICommand Command => ApplicationCommands.Save;
        public HotKeys HotKeys => new(FileHotkeyText.Save, new Hotkey(Key.S, ModifierKeys.Control), Execute)
        {
            Description = FileHotkeyText.SaveDescription,
            Category = FileHotkeyText.Category
        };

        public override void Execute()
        {
            IInputElement? target = Keyboard.FocusedElement ?? Application.Current?.MainWindow;
            if (target != null && ApplicationCommands.Save.CanExecute(null, target))
                ApplicationCommands.Save.Execute(null, target);
        }
    }

}
