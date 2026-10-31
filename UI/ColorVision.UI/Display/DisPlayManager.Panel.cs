using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ColorVision.UI
{
    public partial class DisPlayManager
    {
        private readonly Dictionary<ToggleButton, RoutedEventHandler> _expansionHandlers = new();

        private void RebuildPanel()
        {
            if (StackPanel == null)
                return;

            EnsureDefaultGroup();
            DetachDisplayControlsFromParents();
            StackPanel.Children.Clear();

            bool showGroups = DisPlayManagerConfig.Instance.Groups.Any(a => !IsDefaultGroup(a.Id));
            if (!showGroups)
            {
                StackPanel.Tag = _selectedControl;
                StackPanel.AllowDrop = true;
                foreach (IDisPlayControl item in GetControlsInGroup(DisPlayManagerConfig.DefaultGroupId))
                    AddDisplayControl(StackPanel, item);
            }
            else
            {
                var groupSections = GetGroupsInOrder()
                    .Select(group => (Group: group, Controls: GetControlsInGroup(group.Id).ToList()))
                    .Where(section => !IsDefaultGroup(section.Group.Id) || section.Controls.Count > 0)
                    .ToList();

                foreach (var sectionData in groupSections)
                {
                    var section = CreateGroupSection(sectionData.Group, sectionData.Controls);
                    StackPanel.Children.Add(section);
                }
            }
        }

        private void DetachDisplayControlsFromParents()
        {
            foreach (var userControl in IDisPlayControls.OfType<UserControl>())
            {
                if (userControl.Parent is Panel parent)
                    parent.Children.Remove(userControl);
                else if (userControl.Parent is ContentControl contentControl && ReferenceEquals(contentControl.Content, userControl))
                    contentControl.Content = null;
            }
        }

        private StackPanel CreateGroupSection(DisPlayGroupConfig group, List<IDisPlayControl> controls)
        {
            var section = new StackPanel
            {
                Margin = new Thickness(0, 0, 3, 2),
                Tag = group.Id,
                AllowDrop = true
            };
            section.Drop += Group_Drop;

            var header = new Border
            {
                MinHeight = 22,
                Padding = new Thickness(4, 1, 2, 1),
                Margin = new Thickness(0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = Cursors.Hand,
                Tag = group.Id,
                AllowDrop = true
            };
            header.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            header.SetResourceReference(Border.BorderBrushProperty, "GlobalBorderBrush");
            AttachGroupDropFeedback(header);
            header.MouseLeftButtonUp += (s, e) =>
            {
                group.IsExpanded = !group.IsExpanded;
                RebuildPanel();
                ScheduleConfigSave();
                e.Handled = true;
            };
            header.Drop += Group_Drop;

            var headerPanel = new DockPanel();
            header.Child = headerPanel;

            var menuButton = new Button
            {
                Content = "⋯",
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(4, 0, 0, 0),
                MinWidth = 18,
                Height = 18,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Opacity = 0.6,
                Tag = group
            };
            menuButton.ContextMenu = CreateGroupContextMenu(group);
            menuButton.Click += (s, e) =>
            {
                if (menuButton.ContextMenu != null)
                {
                    menuButton.ContextMenu.PlacementTarget = menuButton;
                    menuButton.ContextMenu.IsOpen = true;
                }
                e.Handled = true;
            };
            DockPanel.SetDock(menuButton, Dock.Right);
            headerPanel.Children.Add(menuButton);

            var arrow = new TextBlock
            {
                Text = group.IsExpanded ? "▾" : "▸",
                Width = 14,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            arrow.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            DockPanel.SetDock(arrow, Dock.Left);
            headerPanel.Children.Add(arrow);

            var count = new TextBlock
            {
                Text = controls.Count.ToString(),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Opacity = 0.55
            };
            count.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            DockPanel.SetDock(count, Dock.Right);
            headerPanel.Children.Add(count);

            var title = new TextBlock
            {
                Text = GetGroupDisplayName(group),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            headerPanel.Children.Add(title);

            section.Children.Add(header);

            var content = new StackPanel
            {
                Tag = group.Id,
                AllowDrop = true,
                Visibility = group.IsExpanded ? Visibility.Visible : Visibility.Collapsed
            };
            content.Drop += Group_Drop;
            foreach (IDisPlayControl item in controls)
                AddDisplayControl(content, item);

            section.Children.Add(content);
            return section;
        }

        internal static string GetGroupDisplayName(DisPlayGroupConfig group)
        {
            if (IsDefaultGroup(group.Id))
                return Properties.Resources.DisplayControlDefaultGroup;

            return string.IsNullOrWhiteSpace(group.Name) ? Properties.Resources.DisplayControlUnnamedGroup : group.Name.Trim();
        }


        private ContextMenu CreateGroupContextMenu(DisPlayGroupConfig group)
        {
            var contextMenu = new ContextMenu();

            var renameItem = new MenuItem { Header = Properties.Resources.Rename };
            renameItem.Click += (s, e) => RenameGroup(group);
            contextMenu.Items.Add(renameItem);

            if (!IsDefaultGroup(group.Id))
            {
                var deleteItem = new MenuItem { Header = Properties.Resources.DisplayControlDeleteGroup };
                deleteItem.Click += (s, e) => DeleteGroup(group);
                contextMenu.Items.Add(deleteItem);
            }

            return contextMenu;
        }

        private void AddDisplayControl(StackPanel panel, IDisPlayControl item)
        {
            if (item is not UserControl userControl)
                return;

            AttachExpansionState(item, userControl);
            userControl.AllowDrop = true;
            userControl.PreviewMouseLeftButtonDown -= DisplayControl_PreviewMouseLeftButtonDown;
            userControl.PreviewMouseLeftButtonDown += DisplayControl_PreviewMouseLeftButtonDown;
            userControl.PreviewMouseLeftButtonUp -= DisplayControl_PreviewMouseLeftButtonUp;
            userControl.PreviewMouseLeftButtonUp += DisplayControl_PreviewMouseLeftButtonUp;
            userControl.PreviewMouseRightButtonDown -= DisplayControl_PreviewMouseRightButtonDown;
            userControl.PreviewMouseRightButtonDown += DisplayControl_PreviewMouseRightButtonDown;
            userControl.PreviewMouseMove -= DisplayControl_PreviewMouseMove;
            userControl.PreviewMouseMove += DisplayControl_PreviewMouseMove;
            userControl.ContextMenuOpening -= DisplayControl_ContextMenuOpening;
            userControl.ContextMenuOpening += DisplayControl_ContextMenuOpening;
            userControl.Drop -= DisplayControl_Drop;
            userControl.Drop += DisplayControl_Drop;
            userControl.Margin = new Thickness(userControl.Margin.Left, 0, userControl.Margin.Right, 2);
            panel.Children.Add(userControl);
        }

        private void AttachExpansionState(IDisPlayControl control, UserControl userControl)
        {
            ToggleButton? header = FindDisplayHeaderToggle(userControl);
            if (header == null)
                return;

            if (_expansionHandlers.Remove(header, out RoutedEventHandler? existingHandler))
            {
                header.Checked -= existingHandler;
                header.Unchecked -= existingHandler;
            }

            EnsureConfigCollections();
            var config = DisPlayManagerConfig.Instance;
            string key = GetControlKey(control);
            bool isExpanded;
            bool configChanged = false;
            if (!config.ControlExpandedStates.TryGetValue(key, out isExpanded))
            {
                isExpanded = header.IsChecked != false;
                config.ControlExpandedStates[key] = isExpanded;
                configChanged = true;
            }

            header.SetCurrentValue(ToggleButton.IsCheckedProperty, isExpanded);
            RoutedEventHandler handler = (_, _) =>
            {
                bool current = header.IsChecked == true;
                if (config.ControlExpandedStates.TryGetValue(key, out bool stored) && stored == current)
                    return;

                config.ControlExpandedStates[key] = current;
                ScheduleConfigSave();
            };
            header.Checked += handler;
            header.Unchecked += handler;
            _expansionHandlers[header] = handler;

            if (configChanged)
                ScheduleConfigSave();
        }

        private void DetachExpansionState(IDisPlayControl control)
        {
            if (control is not UserControl userControl)
                return;

            ToggleButton? header = FindDisplayHeaderToggle(userControl);
            if (header != null && _expansionHandlers.Remove(header, out RoutedEventHandler? handler))
            {
                header.Checked -= handler;
                header.Unchecked -= handler;
            }
        }

        private static ToggleButton? FindDisplayHeaderToggle(DependencyObject owner)
        {
            if (owner is ToggleButton { Name: "DisplayHeaderToggle" } namedHeader)
                return namedHeader;

            if (owner is FrameworkElement element
                && element.FindName("DisplayHeaderToggle") is ToggleButton registeredHeader)
            {
                return registeredHeader;
            }

            int childCount = owner is Visual || owner is Visual3D
                ? VisualTreeHelper.GetChildrenCount(owner)
                : 0;
            for (int i = 0; i < childCount; i++)
            {
                if (FindDisplayHeaderToggle(VisualTreeHelper.GetChild(owner, i)) is ToggleButton childHeader)
                    return childHeader;
            }

            return null;
        }

        private static string? ShowTextDialog(string title, string label, string value)
        {
            var owner = Application.Current.GetActiveWindow();
            var window = new Window
            {
                Title = title,
                Width = 320,
                Height = 135,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Owner = owner,
                Background = Application.Current.FindResource("GlobalBackground") as Brush
            };
            ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(window);

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) };
            labelBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            root.Children.Add(labelBlock);

            var textBox = new TextBox { Text = value, MinHeight = 24 };
            Grid.SetRow(textBox, 1);
            root.Children.Add(textBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttons, 2);

            var okButton = new Button { Content = Properties.Resources.OK, MinWidth = 70, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
            var cancelButton = new Button { Content = Properties.Resources.Cancel, MinWidth = 70, Padding = new Thickness(12, 4, 12, 4), IsCancel = true };
            okButton.Click += (s, e) =>
            {
                window.DialogResult = true;
                window.Close();
            };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            root.Children.Add(buttons);

            window.Content = root;
            window.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            return window.ShowDialog() == true ? textBox.Text : null;
        }

        private void ShowManagementWindow()
        {
            if (!_isInitialized)
                return;

            Window? owner = Application.Current?.GetActiveWindow();
            var window = new DisplayControlManagerWindow(this)
            {
                Owner = owner,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
            FlushPendingConfigSave();
        }
    }
}
