using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Xunit;

namespace ProjectLUX.Tests;

public sealed class LUXWindowLifecycleTests
{
    [Fact]
    public void DetachResultListViewStopsTheClosedViewFromFollowingSharedSelection()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                ObservableCollection<string> items = ["first", "second"];
                SelectionState state = new();
                ListView closedView = CreateBoundList(items, state);
                closedView.ContextMenu = new ContextMenu();
                closedView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy));

                int closedNotifications = 0;
                SelectionChangedEventHandler closedHandler = (_, _) => closedNotifications++;
                closedView.SelectionChanged += closedHandler;
                closedView.SelectedIndex = 0;
                PumpDataBinding();
                closedNotifications = 0;

                LUXWindow.DetachResultListView(closedView, closedHandler);

                ListView currentView = CreateBoundList(items, state);
                int currentNotifications = 0;
                currentView.SelectionChanged += (_, _) => currentNotifications++;
                PumpDataBinding();
                currentNotifications = 0;

                currentView.SelectedIndex = 1;
                PumpDataBinding();

                Assert.Equal(0, closedNotifications);
                Assert.Equal(1, currentNotifications);
                Assert.Null(closedView.ItemsSource);
                Assert.Null(closedView.ContextMenu);
                Assert.Empty(closedView.CommandBindings);
                Assert.Null(BindingOperations.GetBindingExpression(closedView, Selector.SelectedIndexProperty));
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

    private static ListView CreateBoundList(ObservableCollection<string> items, SelectionState state)
    {
        ListView listView = new() { ItemsSource = items };
        BindingOperations.SetBinding(listView, Selector.SelectedIndexProperty, new Binding(nameof(SelectionState.SelectedIndex))
        {
            Source = state,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
        return listView;
    }

    private static void PumpDataBinding()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
    }

    private sealed class SelectionState : INotifyPropertyChanged
    {
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndex)));
            }
        }
        private int _selectedIndex = -1;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
