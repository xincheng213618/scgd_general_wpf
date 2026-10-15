using ColorVision.UI.PropertyEditor.Editor.List;
using FlowEngineLib.Node.Algorithm;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ListEditorIsolationTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ObjectItemEditsReachFlowHistoryOnlyAfterBothDialogsAccept(bool acceptItem, bool acceptList)
    {
        WithResources(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var node = new AlgorithmLoopNode();
            node.Create();
            node.Params = [new AlgorithmNodeProperty { AlgorithmType = "FOV", TempName = "Original" }];
            editor.Nodes.Add(node);
            editor.ClearHistory();
            List<AlgorithmNodeProperty> originalList = node.Params;
            AlgorithmNodeProperty originalItem = node.Params[0];
            int setterNotifications = 0;
            node.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AlgorithmLoopNode.Params)) setterNotifications++;
            };
            DockPanel panel = new CollectionJsonEditor().GenProperties(
                typeof(AlgorithmLoopNode).GetProperty(nameof(AlgorithmLoopNode.Params))!, node);
            Button editCollection = Assert.Single(panel.Children.OfType<Button>());

            DuringModal<ListEditorWindow>(() => Click(editCollection), listWindow =>
            {
                var rows = (ListBox)listWindow.FindName("ItemsListBox");
                rows.SelectedIndex = 0;
                DuringModal<ListItemEditorWindow>(() => Click((Button)listWindow.FindName("EditButton")), itemWindow =>
                {
                    SetBoundText(itemWindow, nameof(AlgorithmNodeProperty.TempName), "Changed");
                    Assert.Equal("Original", originalItem.TempName);
                    Assert.Equal("Original", RowValue<AlgorithmNodeProperty>(rows).TempName);
                    Assert.Equal(0, setterNotifications);
                    CloseItem(itemWindow, acceptItem);
                });

                Assert.Equal(acceptItem ? "Changed" : "Original", RowValue<AlgorithmNodeProperty>(rows).TempName);
                Assert.Same(originalList, node.Params);
                Assert.Equal("Original", originalItem.TempName);
                Assert.Equal(0, setterNotifications);
                Assert.False(editor.IsModified);
                Click((Button)listWindow.FindName(acceptList ? "OkButton" : "CancelButton"));
            });

            bool changed = acceptItem && acceptList;
            Assert.Equal(changed ? "Changed" : "Original", node.Params[0].TempName);
            Assert.Equal(acceptList ? 1 : 0, setterNotifications);
            Assert.Equal("Original", originalItem.TempName);
            Assert.Equal(changed, editor.IsModified);
            if (changed)
            {
                Assert.Single(editor.UndoHistory);
                editor.Undo();
                Assert.Equal("Original", node.Params[0].TempName);
                Assert.False(editor.IsModified);
                editor.Redo();
                Assert.Equal("Changed", node.Params[0].TempName);
                Assert.True(editor.IsModified);
            }
            else
            {
                Assert.Empty(editor.UndoHistory);
                Assert.Empty(editor.RedoHistory);
                if (!acceptList) Assert.Same(originalList, node.Params);
            }
        });
    }

    [Fact]
    public void CancelingNestedListItemDiscardsAcceptedInnerListChanges()
    {
        WithResources(() =>
        {
            var source = new List<List<string>> { new() { "Original" } };
            var outer = new ListEditorWindow(source, typeof(List<string>)) { Opacity = 0, ShowInTaskbar = false };
            DuringModal<ListEditorWindow>(() => outer.ShowDialog(), listWindow =>
            {
                var rows = (ListBox)listWindow.FindName("ItemsListBox");
                rows.SelectedIndex = 0;
                DuringModal<ListItemEditorWindow>(() => Click((Button)listWindow.FindName("EditButton")), itemWindow =>
                {
                    var itemPanel = (StackPanel)itemWindow.FindName("EditorPanel");
                    Button editInnerList = Assert.Single(itemPanel.Children.OfType<Button>());
                    DuringModal<ListEditorWindow>(() => Click(editInnerList), innerListWindow =>
                    {
                        var innerRows = (ListBox)innerListWindow.FindName("ItemsListBox");
                        var row = Assert.IsType<ListEditorWindow.ListItemViewModel>(Assert.Single(innerRows.Items.Cast<object>()));
                        TextBox textBox = Assert.Single(row.InlineEditor!.Children.OfType<TextBox>());
                        textBox.SetCurrentValue(TextBox.TextProperty, "Changed");
                        textBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                        Click((Button)innerListWindow.FindName("OkButton"));
                    });

                    Assert.Equal("Changed", Assert.Single(Assert.IsType<List<string>>(itemWindow.EditedValue)));
                    Assert.Equal("Original", Assert.Single(RowValue<List<string>>(rows)));
                    Assert.Equal("Original", Assert.Single(source[0]));
                    CloseItem(itemWindow, accept: false);
                });

                Assert.Equal("Original", Assert.Single(RowValue<List<string>>(rows)));
                Click((Button)listWindow.FindName("OkButton"));
            });
            Assert.Equal("Original", Assert.Single(Assert.Single(source)));
        });
    }

    [Fact]
    public void AcceptingObjectItemPreservesHiddenValuesWithoutCopyingSourceObservers()
    {
        WithResources(() =>
        {
            var originalItem = new ObservableListItem { Name = "Original", PersistentId = "saved-id", HiddenValues = ["saved-value"] };
            int sourceNotifications = 0;
            originalItem.PropertyChanged += (_, _) => sourceNotifications++;
            var source = new List<ObservableListItem> { originalItem };
            var outer = new ListEditorWindow(source, typeof(ObservableListItem)) { Opacity = 0, ShowInTaskbar = false };
            DuringModal<ListEditorWindow>(() => outer.ShowDialog(), listWindow =>
            {
                var rows = (ListBox)listWindow.FindName("ItemsListBox");
                rows.SelectedIndex = 0;
                DuringModal<ListItemEditorWindow>(() => Click((Button)listWindow.FindName("EditButton")), itemWindow =>
                {
                    SetBoundText(itemWindow, nameof(ObservableListItem.Name), "Changed");
                    var editable = Assert.IsType<ObservableListItem>(itemWindow.EditedValue);
                    Assert.Equal("saved-id", editable.PersistentId);
                    Assert.Equal("saved-value", Assert.Single(editable.HiddenValues));
                    Assert.NotSame(originalItem.HiddenValues, editable.HiddenValues);
                    Assert.NotSame(RowValue<ObservableListItem>(rows).HiddenValues, editable.HiddenValues);
                    Assert.Equal(0, sourceNotifications);
                    CloseItem(itemWindow, accept: true);
                });
                Click((Button)listWindow.FindName("OkButton"));
            });

            ObservableListItem committed = Assert.Single(source);
            Assert.Equal("Changed", committed.Name);
            Assert.Equal("saved-id", committed.PersistentId);
            Assert.Equal("saved-value", Assert.Single(committed.HiddenValues));
            Assert.Equal("Original", originalItem.Name);
            Assert.Equal(0, sourceNotifications);
        });
    }

    private sealed class ObservableListItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        [Browsable(false)]
        public string PersistentId { get; set; } = string.Empty;

        [Browsable(false)]
        public List<string> HiddenValues { get; set; } = [];

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static T RowValue<T>(ListBox rows)
        => Assert.IsType<T>(Assert.IsType<ListEditorWindow.ListItemViewModel>(Assert.Single(rows.Items.Cast<object>())).Value);

    private static void SetBoundText(Window window, string propertyName, string value)
    {
        window.UpdateLayout();
        TextBox textBox = Descendants<TextBox>(window).Single(textBox =>
            textBox.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == propertyName);
        textBox.SetCurrentValue(TextBox.TextProperty, value);
        textBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
    }

    private static void CloseItem(ListItemEditorWindow window, bool accept)
    {
        string label = accept ? Properties.Resources.OK : Properties.Resources.Cancel;
        Click(Descendants<Button>(window).Single(button => Equals(button.Content, label)));
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (T descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void DuringModal<T>(Action open, Action<T> interaction) where T : Window
    {
        Exception? failure = null;
        bool interacted = false;
        DispatcherOperation operation = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            T? window = null;
            try
            {
                window = Application.Current.Windows.OfType<T>().Last(candidate => candidate.IsVisible);
                window.Opacity = 0;
                window.ShowInTaskbar = false;
                interacted = true;
                interaction(window);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                if (window?.IsVisible == true) window.Close();
            }
        }));
        try
        {
            open();
        }
        finally
        {
            operation.Abort();
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.True(interacted);
    }

    private static void WithResources(Action test)
    {
        WpfTestHost.Invoke(() =>
        {
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            try
            {
                test();
            }
            finally
            {
                Application.Current.Resources.MergedDictionaries.Remove(theme);
            }
        });
    }
}
