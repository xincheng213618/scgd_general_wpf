using ColorVision.Common.MVVM;
using ColorVision.Copilot;
using ColorVision.UI.LogImp;
using ColorVision.UI.LogImp.Controls;
using ColorVision.UI.Serach;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class ContextualFindRouterTests
{
    [Fact]
    public void MenuEntryUsesTheOwnersRememberedContentFocusNotAnotherDocument()
    {
        WpfTestHost.Invoke(() =>
        {
            var grid = new Grid();
            var content = new TextBox();
            var menu = new Menu();
            var item = new MenuItem();
            menu.Items.Add(item);
            grid.Children.Add(content);
            grid.Children.Add(menu);
            var owner = new Window { Content = grid };
            try
            {
                FocusManager.SetFocusedElement(owner, content);
                Assert.Same(content, MainWindow.ResolveSearchCommandTarget(owner, item));
                Assert.Same(content, MainWindow.ResolveSearchCommandTarget(owner, content));
                Assert.Null(MainWindow.ResolveSearchCommandTarget(owner, new TextBox()));
                grid.Children.Remove(content);
                Assert.Null(MainWindow.ResolveSearchCommandTarget(owner, item));
            }
            finally { owner.Content = null; owner.Close(); }
        });
    }

    [Fact]
    public void StandardFindWinsOverAttachedFallbackAndRunsOnce()
    {
        WpfTestHost.Invoke(() =>
        {
            var focused = new Button();
            var scope = new Grid();
            scope.Children.Add(focused);
            int standard = 0, fallback = 0;
            focused.CommandBindings.Add(new CommandBinding(ApplicationCommands.Find,
                (_, e) => { standard++; e.Handled = true; }, (_, e) => { e.CanExecute = true; e.Handled = true; }));
            ContextualFindRouter.SetLocalFindCommand(scope, new RelayCommand(_ => fallback++));
            Assert.True(ContextualFindRouter.TryFind(focused, scope));
            Assert.Equal(1, standard);
            Assert.Equal(0, fallback);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitLocalFindOwnsItsScopeEvenWhenTemporarilyDisabled(bool enabled)
    {
        WpfTestHost.Invoke(() =>
        {
            var focused = new Button();
            var scope = new Grid();
            scope.Children.Add(focused);
            int calls = 0;
            ContextualFindRouter.SetLocalFindCommand(scope, new RelayCommand(_ => calls++, _ => enabled));
            Assert.True(ContextualFindRouter.TryFind(focused, scope));
            Assert.Equal(enabled ? 1 : 0, calls);
        });
    }

    [Fact]
    public void DisabledStandardFindDoesNotOpenApplicationSearch()
    {
        WpfTestHost.Invoke(() =>
        {
            var scope = new Grid();
            var focused = new Button();
            scope.Children.Add(focused);
            focused.CommandBindings.Add(new CommandBinding(ApplicationCommands.Find,
                (_, _) => throw new InvalidOperationException("Disabled Find must not run."),
                (_, e) => { e.CanExecute = false; e.Handled = true; }));
            Assert.True(ContextualFindRouter.TryFind(focused, scope));
        });
    }

    [Fact]
    public void PlainPageFallsBackWithoutSearchingAnotherPane()
    {
        WpfTestHost.Invoke(() =>
        {
            var scope = new Grid();
            var focused = new Button();
            var otherPane = new Grid();
            scope.Children.Add(focused);
            scope.Children.Add(otherPane);
            ContextualFindRouter.SetLocalFindCommand(otherPane, new RelayCommand(_ => throw new InvalidOperationException("Other pane must not receive Find.")));
            Assert.False(ContextualFindRouter.TryFind(focused, scope));
            Assert.False(ContextualFindRouter.TryFind(null, scope));
        });
    }

    [Fact]
    public void DetachedOrForeignFocusNeverExecutesItsCommand()
    {
        WpfTestHost.Invoke(() =>
        {
            var scope = new Grid();
            var detached = new Button();
            ContextualFindRouter.SetLocalFindCommand(detached, new RelayCommand(_ => throw new InvalidOperationException("Detached command must not execute.")));
            Assert.False(ContextualFindRouter.IsWithin(detached, scope));
            Assert.False(ContextualFindRouter.TryFind(detached, scope));
        });
    }

    [Fact]
    public void PlainTextAndPasswordEntryRemainLocal()
    {
        WpfTestHost.Invoke(() =>
        {
            var scope = new Grid();
            var text = new TextBox();
            var password = new PasswordBox();
            scope.Children.Add(text);
            scope.Children.Add(password);
            Assert.True(ContextualFindRouter.TryFind(text, scope));
            Assert.True(ContextualFindRouter.TryFind(password, scope));
        });
    }

    [Fact]
    public void ContentElementsAreRecognizedWithoutVisualTreeExceptions()
    {
        WpfTestHost.Invoke(() =>
        {
            var text = new TextBlock();
            var link = new Hyperlink(new Run("Find"));
            text.Inlines.Add(link);
            int calls = 0;
            ContextualFindRouter.SetLocalFindCommand(text, new RelayCommand(_ => calls++));
            Assert.True(ContextualFindRouter.IsWithin(link, text));
            Assert.True(ContextualFindRouter.TryFind(link, text));
            Assert.Equal(1, calls);
        });
    }

    [Fact]
    public void AvalonEditUsesItsInstalledSearchPanel()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new TextEditor();
            var panel = SearchPanel.Install(editor);
            var owner = new Window
            {
                Width = 600, Height = 400, Left = -10000, Top = -10000,
                ShowInTaskbar = false, ShowActivated = false, Opacity = 0, WindowStyle = WindowStyle.None
            };
            try
            {
                // AvalonEdit does not parent TextArea until its control template is
                // materialized in an initialized host. Layout on a naked TextEditor
                // leaves its default Template null and cannot exercise command routing.
                Assert.False(ContextualFindRouter.IsWithin(editor.TextArea, editor));
                owner.Content = new AdornerDecorator { Child = editor };
                owner.Show();
                owner.UpdateLayout();
                owner.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.NotNull(editor.Template);
                Assert.True(ContextualFindRouter.IsWithin(editor.TextArea, owner));
                Assert.NotNull(AdornerLayer.GetAdornerLayer(editor.TextArea));
                Assert.True(ApplicationCommands.Find.CanExecute(null, editor.TextArea));
                Assert.True(panel.IsClosed);
                Assert.True(ContextualFindRouter.TryFind(editor.TextArea, owner));
                Assert.False(panel.IsClosed);
                Assert.False(owner.IsActive);
            }
            finally
            {
                panel.Uninstall();
                owner.Content = null;
                owner.Close();
            }
        });
    }

    [Fact]
    public void LogControllerExposesFindAndRemovesItsBindingOnDetach()
    {
        WpfTestHost.Invoke(() =>
        {
            var scope = new Grid();
            var panel = new Grid { Visibility = Visibility.Collapsed };
            var input = new TextBox();
            var viewer = new LogViewerControl();
            scope.Children.Add(panel);
            scope.Children.Add(viewer);
            panel.Children.Add(input);
            var controller = new LogTextViewController(scope, scope, panel, input, viewer);
            try
            {
                Assert.True(ContextualFindRouter.TryFind(viewer, scope));
                Assert.Equal(Visibility.Visible, panel.Visibility);
            }
            finally { controller.Detach(); }
            Assert.DoesNotContain(scope.CommandBindings.Cast<CommandBinding>(), binding => binding.Command == ApplicationCommands.Find);
        });
    }

    [Fact]
    public void ChatAdapterPreservesLocalOwnershipWithoutLoadingAConversation()
    {
        WpfTestHost.Invoke(() =>
        {
            var chat = new CopilotChatPanel();
            var prompt = Assert.IsType<TextBox>(chat.FindName("PromptTextBox"));
            MainWindow.AttachConversationFindAdapter(chat);
            ICommand command = Assert.IsAssignableFrom<ICommand>(ContextualFindRouter.GetLocalFindCommand(chat));
            MainWindow.AttachConversationFindAdapter(chat);
            Assert.Same(command, ContextualFindRouter.GetLocalFindCommand(chat));
            Assert.False(command.CanExecute(null));
            Assert.True(ContextualFindRouter.TryFind(prompt, chat));
        });
    }
}
