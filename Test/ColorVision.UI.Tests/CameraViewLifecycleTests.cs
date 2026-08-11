using ColorVision.Engine.Services.Devices.Camera.Views;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class CameraViewLifecycleTests
{
    [Fact]
    public void DetachResultListViewClearsSharedDataAndUiHandlersIdempotently()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                ObservableCollection<string> items = ["first", "second"];
                ListView listView = new()
                {
                    ItemsSource = items,
                    ContextMenu = new ContextMenu(),
                };
                BindingOperations.SetBinding(listView, ListView.HeightProperty, new Binding { Source = 100d });
                listView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy));

                int selectionNotifications = 0;
                SelectionChangedEventHandler selectionHandler = (_, _) => selectionNotifications++;
                KeyEventHandler previewKeyDownHandler = (_, _) => { };
                listView.SelectionChanged += selectionHandler;
                listView.PreviewKeyDown += previewKeyDownHandler;

                ViewCamera.DetachResultListView(listView, selectionHandler, previewKeyDownHandler);
                ViewCamera.DetachResultListView(listView, selectionHandler, previewKeyDownHandler);

                Assert.Null(listView.ItemsSource);
                selectionNotifications = 0;
                listView.ItemsSource = items;
                listView.SelectedIndex = 0;

                Assert.Equal(0, selectionNotifications);
                Assert.Null(BindingOperations.GetBindingExpression(listView, ListView.HeightProperty));
                Assert.Null(listView.ContextMenu);
                Assert.Empty(listView.CommandBindings);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        Assert.True(thread.TrySetApartmentState(ApartmentState.STA));
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA lifecycle test did not finish.");

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
