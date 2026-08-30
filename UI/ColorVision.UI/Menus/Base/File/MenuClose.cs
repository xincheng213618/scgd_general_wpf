using System.Windows.Input;

using System.Windows;
using ColorVision.UI.HotKey;
using ColorVision.Common.MVVM;

namespace ColorVision.UI.Menus.Base.File
{
    public class MenuClose : MenuItemFileBase, IHotKey
    {
        public static RoutedUICommand CloseDocumentCommand { get; } = new(
            "Close document", nameof(CloseDocumentCommand), typeof(MenuClose));

        private readonly RelayCommand _command;

        public MenuClose()
        {
            _command = new RelayCommand(_ => Execute(), _ =>
            {
                var route = GetCloseTarget();
                return route is { } target && target.Command.CanExecute(null, target.Target);
            });
        }

        public override string GuidId => nameof(MenuClose);
        public override string Header => ColorVision.UI.Properties.Resources.MenuClose;
        public override int Order => 20;
        public override ICommand Command => _command;
        public override string InputGestureText => "Ctrl+W / Ctrl+F4";
        public HotKeys HotKeys => new(FileHotkeyText.CloseDocument, new Hotkey(Key.W, ModifierKeys.Control), Execute)
        {
            AdditionalHotkeys = [new Hotkey(Key.F4, ModifierKeys.Control)],
            DefaultAdditionalHotkeys = [new Hotkey(Key.F4, ModifierKeys.Control)],
            Description = FileHotkeyText.CloseDocumentDescription,
            Category = FileHotkeyText.Category
        };

        public override void Execute()
        {
            if (GetCloseTarget() is { } route && route.Command.CanExecute(null, route.Target))
                route.Command.Execute(null, route.Target);
        }

        private static (RoutedCommand Command, IInputElement Target)? GetCloseTarget()
        {
            Window? window = Application.Current?.GetActiveWindow();
            if (window == null) return null;

            // A document host owns Close even when its current tab cannot close.
            // Falling back after CanExecute=false would clear the image instead.
            if (window.CommandBindings.OfType<CommandBinding>().Any(binding => binding.Command == CloseDocumentCommand))
                return (CloseDocumentCommand, window);

            // Menus use their own focus scope; preserve the editor's remembered target.
            IInputElement? focus = FocusManager.GetFocusedElement(window);
            if (focus is not DependencyObject element || Window.GetWindow(element) != window)
                focus = Keyboard.FocusedElement;
            if (focus is not DependencyObject focusedElement || Window.GetWindow(focusedElement) != window)
                focus = window;
            return (ApplicationCommands.Close, focus);
        }
    }
}
