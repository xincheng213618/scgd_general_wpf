using ColorVision.Solution;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base.File;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class FileHotkeyDefaultsTests
{

    [Fact]
    public void CloseUsesTwoIndependentDefaultsAndOneContextAwareMenuCommand()
    {
        var menu = new MenuClose();
        HotKeys declaration = menu.HotKeys;
        Assert.Same(menu.Command, menu.Command);
        Assert.NotSame(ApplicationCommands.Close, menu.Command);
        Assert.Empty(MenuClose.CloseDocumentCommand.InputGestures);
        Assert.Equal([new Hotkey(Key.W, ModifierKeys.Control), new Hotkey(Key.F4, ModifierKeys.Control)], declaration.GetBindings());
        Assert.Equal(declaration.GetBindings(), declaration.GetDefaultBindings());

        declaration.AdditionalHotkeys[0].Key = Key.F8;
        Assert.Equal(Key.F4, declaration.DefaultAdditionalHotkeys[0].Key);
        Assert.Equal(Key.F4, menu.HotKeys.AdditionalHotkeys[0].Key);
    }

    [Theory]
    [InlineData(typeof(MenuSave), true)]
    [InlineData(typeof(MenuSave), false)]
    [InlineData(typeof(MenuSaveAs), true)]
    [InlineData(typeof(MenuSaveAs), false)]
    [InlineData(typeof(MenuClose), true)]
    [InlineData(typeof(MenuClose), false)]
    public void ShortcutCallbackUsesTheMenuCommandAndRespectsCanExecute(Type providerType, bool canExecute)
    {
        WpfTestHost.Invoke(() => WithMainWindow(window =>
        {
            var source = Assert.IsAssignableFrom<IMenuItem>(Activator.CreateInstance(providerType));
            var command = providerType == typeof(MenuClose)
                ? MenuClose.CloseDocumentCommand
                : Assert.IsAssignableFrom<RoutedCommand>(source.Command);
            int executions = 0;
            window.CommandBindings.Add(new CommandBinding(command,
                (_, e) => { executions++; e.Handled = true; },
                (_, e) => { e.CanExecute = canExecute; e.Handled = true; }));

            if (source is MenuClose) Assert.Equal(canExecute, source.Command!.CanExecute(null));
            Assert.IsAssignableFrom<IHotKey>(source).HotKeys.HotKeyHandler!();

            Assert.Equal(canExecute ? 1 : 0, executions);
        }));
    }

    [Fact]
    public void DocumentHostCloseUsesOnlyTheTabCommandForMenuAndShortcut()
    {
        WpfTestHost.Invoke(() => WithMainWindow(window =>
        {
            var editor = new Border { Focusable = true };
            window.Content = editor;
            FocusManager.SetFocusedElement(window, editor);
            int tabClosures = 0;
            int imageClears = 0;
            window.CommandBindings.Add(new CommandBinding(MenuClose.CloseDocumentCommand,
                (_, e) => { tabClosures++; e.Handled = true; },
                (_, e) => { e.CanExecute = true; e.Handled = true; }));
            editor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close,
                (_, e) => { imageClears++; e.Handled = true; },
                (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var source = new MenuClose();

            Assert.True(source.Command.CanExecute(null));
            source.Command.Execute(null);
            source.HotKeys.HotKeyHandler!();

            Assert.Equal(2, tabClosures);
            Assert.Equal(0, imageClears);
        }));
    }

    [Fact]
    public void DisabledDocumentHostCloseNeverFallsBackToImageClear()
    {
        WpfTestHost.Invoke(() => WithMainWindow(window =>
        {
            var editor = new Border { Focusable = true };
            window.Content = editor;
            FocusManager.SetFocusedElement(window, editor);
            int executions = 0;
            window.CommandBindings.Add(new CommandBinding(MenuClose.CloseDocumentCommand,
                (_, _) => executions++,
                (_, e) => { e.CanExecute = false; e.Handled = true; }));
            editor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close,
                (_, _) => executions++,
                (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var source = new MenuClose();

            Assert.False(source.Command.CanExecute(null));
            source.Command.Execute(null);
            source.HotKeys.HotKeyHandler!();

            Assert.Equal(0, executions);
        }));
    }

    [Fact]
    public void StandaloneWindowCloseRetainsItsRememberedEditorCommand()
    {
        WpfTestHost.Invoke(() => WithMainWindow(window =>
        {
            var editor = new Border { Focusable = true };
            window.Content = editor;
            FocusManager.SetFocusedElement(window, editor);
            int executions = 0;
            editor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close,
                (_, e) => { executions++; e.Handled = true; },
                (_, e) => { e.CanExecute = true; e.Handled = true; }));
            var source = new MenuClose();

            Assert.True(source.Command.CanExecute(null));
            source.Command.Execute(null);
            source.HotKeys.HotKeyHandler!();

            Assert.Equal(2, executions);
        }));
    }

    [Fact]
    public void UnsupportedSaveAsDoesNotFallBackToSavingOrClosing()
    {
        WpfTestHost.Invoke(() => WithMainWindow(window =>
        {
            int otherExecutions = 0;
            foreach (var command in new[] { ApplicationCommands.Save, ApplicationCommands.Close })
                window.CommandBindings.Add(new CommandBinding(command, (_, _) => otherExecutions++, (_, e) => e.CanExecute = true));

            new MenuSaveAs().HotKeys.HotKeyHandler!();

            Assert.Equal(0, otherExecutions);
        }));
    }

    private static void WithMainWindow(Action<Window> action)
    {
        Application application = Application.Current;
        Window previous = application.MainWindow;
        var window = new Window();
        try
        {
            Keyboard.ClearFocus();
            application.MainWindow = window;
            action(window);
        }
        finally
        {
            application.MainWindow = previous;
            window.Close();
        }
    }
}
