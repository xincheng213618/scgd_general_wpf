using ColorVision.Copilot;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus.Base.File;
using AvalonDock;
using AvalonDock.Layout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class ApplicationHotkeyIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseQueriesTheReplacementLayoutAndHonorsCancellation(bool cancel)
    {
        WpfTestHost.Invoke(() =>
        {
            var oldDocument = new LayoutDocument { Title = "old" };
            var oldPane = new LayoutDocumentPane(oldDocument);
            var manager = new DockingManager { Layout = new LayoutRoot { RootPanel = new LayoutPanel(oldPane) } };
            var host = new Window { Content = manager };
            host.CommandBindings.Add(MainWindow.CreateCloseDocumentBinding(manager));
            try
            {
                var currentDocument = new LayoutDocument { Title = "current" };
                var currentPane = new LayoutDocumentPane(currentDocument);
                manager.Layout = new LayoutRoot { RootPanel = new LayoutPanel(currentPane) };
                currentDocument.IsActive = true;
                int confirmations = 0;
                currentDocument.Closing += (_, e) => { confirmations++; e.Cancel = cancel; };

                Assert.True(MenuClose.CloseDocumentCommand.CanExecute(null, host));
                MenuClose.CloseDocumentCommand.Execute(null, host);

                Assert.Equal(1, confirmations);
                Assert.Equal(cancel, currentPane.Children.Contains(currentDocument));
                Assert.Contains(oldDocument, oldPane.Children);
            }
            finally { host.Content = null; host.Close(); }
        });
    }

    [Fact]
    public void NonClosableCurrentDocumentDisablesTheShellCloseCommand()
    {
        WpfTestHost.Invoke(() =>
        {
            var document = new LayoutDocument { CanClose = false };
            var manager = new DockingManager { Layout = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPane(document)) } };
            var host = new Window { Content = manager };
            host.CommandBindings.Add(MainWindow.CreateCloseDocumentBinding(manager));
            try
            {
                document.IsActive = true;
                Assert.False(MenuClose.CloseDocumentCommand.CanExecute(null, host));
            }
            finally { host.Content = null; host.Close(); }
        });
    }

    [Fact]
    public void BuiltInActionSetHasOnlyDistinctConventionalApplicationDefaults()
    {
        IHotKey[] providers =
        [
            new ColorVision.UI.MenuFileOpen(),
            new ColorVision.Solution.MenuOpenFolder(), new ColorVision.Solution.MenuOpenSolution(),
            new ColorVision.UI.Menus.Base.File.MenuSave(), new ColorVision.UI.Menus.Base.File.MenuSaveAs(),
            new ColorVision.UI.Menus.Base.File.MenuClose(), new ColorVision.UI.Desktop.Settings.MenuOptions(),
            new MenuCommandSearch(), new MenuContextualFind(), new ColorVision.UI.LogImp.MenuLogWindow(),
            new ColorVision.Update.MenuCheckAndUpdateV1(), new ExportMenuViewStatusBar(),
            new AboutMsgExport(), new ColorVision.Solution.Workspace.MenuResetLayout()
        ];
        var actions = providers.Select(provider => provider.HotKeys).ToArray();
        var bindings = actions.SelectMany(action => action.GetDefaultBindings()).ToArray();
        Assert.Equal(14, actions.Length);
        Assert.Equal(3, actions.Count(action => action.GetDefaultBindings().Count == 0));
        Assert.Equal(12, bindings.Length);
        Assert.Equal(bindings.Length, bindings.Distinct().Count());
        Assert.All(actions, action => Assert.Equal(HotKeyKinds.Windows, action.Kinds));
        Assert.DoesNotContain(bindings, binding => binding.Key is Key.F5 or Key.Delete or Key.Escape);
        Assert.DoesNotContain(bindings, binding => binding.Modifiers == ModifierKeys.Control && binding.Key is Key.L or Key.C or Key.V or Key.Z or Key.A);
    }

    [Fact]
    public void CommandSearchUsesCommandPaletteGestureNotContentFind()
    {
        var action = new MenuCommandSearch().HotKeys;
        Assert.Equal(new Hotkey(Key.P, ModifierKeys.Control | ModifierKeys.Shift), Assert.Single(action.GetDefaultBindings()));
        Assert.False(string.IsNullOrWhiteSpace(action.Description));
        Assert.Equal(HotKeyKinds.Windows, action.Kinds);
    }

    [Fact]
    public void ContextualFindHasItsOwnConfigurableDefaultAndExplainsLocalBehavior()
    {
        var action = new MenuContextualFind().HotKeys;
        Assert.Equal(new Hotkey(Key.F, ModifierKeys.Control), Assert.Single(action.GetDefaultBindings()));
        Assert.False(string.IsNullOrWhiteSpace(action.Description));
        Assert.Equal(HotKeyKinds.Windows, action.Kinds);
    }

    [Fact]
    public void CopilotHelpDoesNotAdvertiseOpenOrNewTabAsUnrelatedActions()
    {
        Assert.DoesNotContain(CopilotKeyboardShortcutHelp.Entries, item => item.Keys is "Ctrl+O" or "Ctrl+T");
        Assert.Contains(CopilotKeyboardShortcutHelp.Entries, item => item.Keys == "Ctrl+Shift+C" && item.Action.Contains("复制"));
        Assert.Contains(CopilotKeyboardShortcutHelp.Entries, item => item.Keys == "Ctrl+Alt+T" && item.Action.Contains("任务"));
    }

    [Fact]
    public void ComposerOwnsSaveCommandAndDoesNotFallThroughToAnotherDocument()
    {
        WpfTestHost.Invoke(() =>
        {
            // No ViewModel, real configuration, provider, HWND, clipboard or document is loaded.
            var panel = new CopilotChatPanel();
            var prompt = Assert.IsAssignableFrom<TextBox>(panel.FindName("PromptTextBox"));
            Assert.Single(prompt.CommandBindings.Cast<CommandBinding>(), binding => binding.Command == ApplicationCommands.Save);
            int saves = 0;
            var host = new Window { Content = panel };
            host.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save,
                (_, _) => saves++, (_, e) => e.CanExecute = true));
            try
            {
                Assert.False(ApplicationCommands.Save.CanExecute(null, prompt));
                Assert.Equal(0, saves);
                Assert.True(ApplicationCommands.Save.CanExecute(null, host));
            }
            finally { host.Content = null; host.Close(); }
        });
    }
}
