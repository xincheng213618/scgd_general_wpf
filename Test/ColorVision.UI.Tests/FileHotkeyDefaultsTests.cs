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
    [Theory]
    [InlineData(typeof(MenuFileOpen), Key.O, ModifierKeys.Control)]
    [InlineData(typeof(MenuOpenFolder), Key.O, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(typeof(MenuOpenSolution), Key.O, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(typeof(MenuSave), Key.S, ModifierKeys.Control)]
    [InlineData(typeof(MenuSaveAs), Key.S, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(typeof(MenuClose), Key.W, ModifierKeys.Control)]
    public void FileActionsDeclareConventionalDefaultsAndEditableMenuHints(Type providerType, Key key, ModifierKeys modifiers)
    {
        WpfTestHost.Invoke(() =>
        {
            var menuSource = Assert.IsAssignableFrom<IMenuItem>(Activator.CreateInstance(providerType));
            HotKeys declaration = Assert.IsAssignableFrom<IHotKey>(menuSource).HotKeys;
            Assert.Equal(new Hotkey(key, modifiers), declaration.Hotkey);
            Assert.Equal(new Hotkey(key, modifiers), declaration.DefaultHotkey);
            Assert.False(string.IsNullOrWhiteSpace(declaration.Description));
            Assert.False(string.IsNullOrWhiteSpace(declaration.Category));
            Assert.Equal(HotKeyKinds.Windows, declaration.Kinds);
            Assert.NotNull(declaration.HotKeyHandler);

            // Declarations are safe to inspect: no dialog, registration, file write, or application startup.
            declaration.Id = providerType.FullName!;
            var menu = new MenuItem { Header = menuSource.Header, Command = menuSource.Command };
            HotkeyMenuGestureBinding.Attach(menu, menuSource, new ObservableCollection<HotKeys> { declaration });
            Assert.Equal(string.Join(" / ", declaration.GetBindings().Select(HotkeyInput.Format)), menu.InputGestureText);

            declaration.SetBindings([new Hotkey(Key.F8, ModifierKeys.Control | ModifierKeys.Shift)]);
            Assert.Equal("Ctrl+Shift+F8", menu.InputGestureText);
            declaration.SetBindings([]);
            Assert.Equal(string.Empty, menu.InputGestureText);
            Assert.False(declaration.IsRegistered);
        });
    }

    [Fact]
    public void OpenFileWorkspaceAndFolderHaveDistinctMenuTargets()
    {
        Assert.NotSame(ApplicationCommands.Open, new MenuOpenSolution().Command);
        Assert.Same(SolutionWorkspaceCommands.OpenWorkspace, new MenuOpenSolution().Command);
        Assert.Same(SolutionWorkspaceCommands.OpenFolder, new MenuOpenFolder().Command);
        Assert.Empty(SolutionWorkspaceCommands.OpenWorkspace.InputGestures);
        Assert.Empty(SolutionWorkspaceCommands.OpenFolder.InputGestures);
        Assert.Equal(new Hotkey(Key.O, ModifierKeys.Control | ModifierKeys.Alt), Assert.Single(new MenuOpenSolution().HotKeys.GetBindings()));
    }

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

    [Theory]
    [InlineData("zh-CN", "打开文件", "当前渲染图")]
    [InlineData("en-US", "Open file", "current rendered image")]
    public void DescriptionsAreLocalizedAndDoNotClaimUniversalSaveAs(string language, string openFileName, string saveAsLimit)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            Assert.Equal(openFileName, new MenuFileOpen().HotKeys.Name);
            Assert.Contains(saveAsLimit, new MenuSaveAs().HotKeys.Description, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
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
