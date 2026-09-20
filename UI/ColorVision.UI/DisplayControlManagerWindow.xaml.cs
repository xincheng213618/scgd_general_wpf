using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace ColorVision.UI;

public partial class DisplayControlManagerWindow : Window
{
    private readonly DisPlayManager _manager;

    public ObservableCollection<DisPlayGroupConfig> Groups => _manager.Groups;
    public ObservableCollection<DisplayControlManagementItem> Controls { get; } = new();

    internal DisplayControlManagerWindow(DisPlayManager manager)
    {
        _manager = manager;
        InitializeComponent();
        DataContext = this;
        RefreshControls();
        GroupsListBox.SelectedIndex = 0;
    }

    private void RefreshControls(IDisPlayControl? selectedControl = null)
    {
        selectedControl ??= (ControlsDataGrid.SelectedItem as DisplayControlManagementItem)?.Control;
        for (int index = Controls.Count - 1; index >= 0; index--)
        {
            if (!_manager.IDisPlayControls.Contains(Controls[index].Control))
                Controls.RemoveAt(index);
        }

        for (int index = 0; index < _manager.IDisPlayControls.Count; index++)
        {
            IDisPlayControl control = _manager.IDisPlayControls[index];
            DisplayControlManagementItem? item = Controls.FirstOrDefault(item => ReferenceEquals(item.Control, control));
            if (item == null)
                Controls.Insert(index, new DisplayControlManagementItem(_manager, control, ScheduleControlsRefresh));
            else
            {
                int currentIndex = Controls.IndexOf(item);
                if (currentIndex != index)
                    Controls.Move(currentIndex, index);
                item.RefreshState();
            }
        }

        ControlsDataGrid.SelectedItem = Controls.FirstOrDefault(item => ReferenceEquals(item.Control, selectedControl));
    }

    private void ScheduleControlsRefresh(IDisPlayControl control)
    {
        Dispatcher.BeginInvoke(() => RefreshControls(control), DispatcherPriority.Background);
    }

    private void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        _manager.CreateGroup();
        GroupsListBox.SelectedItem = Groups.LastOrDefault();
    }

    private void RenameGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DisPlayGroupConfig group })
            _manager.RenameGroup(group);
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DisPlayGroupConfig group } || DisPlayManager.IsDefaultGroup(group.Id))
            return;

        _manager.DeleteGroup(group);
        if (!Groups.Contains(group))
            GroupsListBox.SelectedIndex = 0;
        RefreshControls();
    }

    private void MoveGroupUp_Click(object sender, RoutedEventArgs e) => MoveGroup(sender, -1);

    private void MoveGroupDown_Click(object sender, RoutedEventArgs e) => MoveGroup(sender, 1);

    private void MoveGroup(object sender, int offset)
    {
        if (sender is not FrameworkElement { DataContext: DisPlayGroupConfig group })
            return;

        _manager.MoveGroup(group, offset);
        GroupsListBox.SelectedItem = group;
        RefreshControls();
    }

    private void GroupExpanded_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { DataContext: DisPlayGroupConfig group } toggle)
            _manager.SetGroupExpanded(group, toggle.IsChecked == true);
    }

    private void GroupActions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || ItemsControl.ContainerFromElement(GroupsListBox, button) is not ListBoxItem row)
            return;

        row.IsSelected = true;
        row.ContextMenu.PlacementTarget = button;
        row.ContextMenu.Placement = PlacementMode.Bottom;
        row.ContextMenu.IsOpen = true;
    }

    private void GroupMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu { DataContext: DisPlayGroupConfig group } menu)
            return;

        int index = Groups.IndexOf(group);
        foreach (MenuItem item in menu.Items.OfType<MenuItem>())
        {
            item.IsEnabled = item.Tag switch
            {
                "-1" => !DisPlayManager.IsDefaultGroup(group.Id) && index > 1,
                "1" => !DisPlayManager.IsDefaultGroup(group.Id) && index < Groups.Count - 1,
                "Delete" => !DisPlayManager.IsDefaultGroup(group.Id),
                _ => true
            };
        }
    }

    private void MoveControlUp_Click(object sender, RoutedEventArgs e) => MoveControl(sender, -1);

    private void MoveControlDown_Click(object sender, RoutedEventArgs e) => MoveControl(sender, 1);

    private void MoveControl(object sender, int offset)
    {
        if (sender is not FrameworkElement { DataContext: DisplayControlManagementItem item })
            return;

        _manager.MoveControl(item.Control, offset);
        RefreshControls(item.Control);
        ControlsDataGrid.ScrollIntoView(item);
    }
}

public sealed class DisplayControlManagementItem : ViewModelBase
{
    private readonly DisPlayManager _manager;
    private readonly Action<IDisPlayControl> _changed;

    internal IDisPlayControl Control { get; }
    public string Name => Control.DisPlayName;

    internal void RefreshState() => OnPropertyChanged(string.Empty);

    public bool IsVisible
    {
        get => DisPlayManager.IsControlVisible(Control);
        set
        {
            if (value == IsVisible)
                return;
            _manager.SetControlVisible(Control, value);
            OnPropertyChanged();
            _changed(Control);
        }
    }

    public bool IsPinned
    {
        get => DisPlayManager.IsPinned(Control);
        set
        {
            if (value == IsPinned)
                return;
            _manager.SetPinned(Control, value);
            OnPropertyChanged();
            _changed(Control);
        }
    }

    public bool IsExpanded
    {
        get => _manager.IsControlExpanded(Control);
        set
        {
            if (value == IsExpanded)
                return;
            _manager.SetControlExpanded(Control, value);
            OnPropertyChanged();
        }
    }

    public string GroupId
    {
        get => _manager.GetControlGroupId(Control);
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == GroupId)
                return;
            _manager.SetControlGroup(Control, value);
            OnPropertyChanged();
            _changed(Control);
        }
    }

    internal DisplayControlManagementItem(
        DisPlayManager manager,
        IDisPlayControl control,
        Action<IDisPlayControl> changed)
    {
        _manager = manager;
        Control = control;
        _changed = changed;
    }
}
