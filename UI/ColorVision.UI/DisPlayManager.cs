using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Views;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ColorVision.UI
{


    public static class DisPlayManagerExtension
    {

        /// <summary>
        /// 注册视图控件到 DockViewManager。
        /// 控件只需是 UserControl，标题通过 title 参数传入。
        /// 当 IDisPlayControl 选中时，自动切换到对应视图标签。
        /// </summary>
 


        public static void ApplyChangedSelectedColor(this IDisPlayControl disPlayControl, Border border)
        {
            void UpdateDisPlayBorder()
            {
                if (disPlayControl.IsSelected)
                {
                    border.BorderBrush = ImageUtils.ConvertFromString(ThemeManager.Current.CurrentUITheme switch
                    {
                        Theme.Light => "#5649B0",
                        Theme.Dark => "#A79CF1",
                        Theme.Pink => "#F06292", // 粉色主题选中颜色
                        Theme.Cyan => "#00BCD4", // 青色主题选中颜色
                        _ => "#A79CF1" // 默认颜色
                    });
                }
                else
                {
                    Brush brush = Application.Current.FindResource("GlobalBorderBrush1") as Brush;
                    border.BorderBrush = brush;
                }
            }
            disPlayControl.SelectChanged += (s, e) => UpdateDisPlayBorder();
            ThemeManager.Current.CurrentUIThemeChanged += (s) => UpdateDisPlayBorder();
            UpdateDisPlayBorder();
            if (disPlayControl is UserControl userControl)
            {
                userControl.Focusable = true;
                userControl.MouseDown += (s, e) =>
                {
                    DisPlayManager.GetInstance().SelectControl(disPlayControl);
                    userControl.Focus();
                };
            }
        }

        public static void AddViewConfig(this UserControl userControl, UserControl viewControl, string title)
        {
            var manager = DockViewManager.GetInstance();
            manager.AddView(viewControl);
            if (!string.IsNullOrEmpty(title))
                manager.ViewTitles[viewControl] = title;

            userControl.PreviewMouseDown += (s, e) =>
            {
                manager.SelectView(viewControl);
            };
        }
    }

    public class DisPlayGroupConfig : ViewModelBase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = string.Empty;

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; OnPropertyChanged(); } }
        private bool _IsExpanded = true;
    }

    public class DisPlayManagerConfig : ViewModelBase,IConfig
    {
        public const string DefaultGroupId = "__default__";
        public static DisPlayManagerConfig Instance => ConfigService.Instance.GetRequiredService<DisPlayManagerConfig>();

        public Dictionary<string, int> StoreIndex { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, string> ControlGroups { get; set; } = new Dictionary<string, string>();
        public ObservableCollection<DisPlayGroupConfig> Groups { get; set; } = new ObservableCollection<DisPlayGroupConfig>();

        public int LastSelectIndex { get => _LastSelectIndex; set { _LastSelectIndex = value; OnPropertyChanged(); } }
        private int _LastSelectIndex ;
    }



    public class DisPlayManager
    {
        private static DisPlayManager _instance;
        private static readonly object _locker = new();
        public static DisPlayManager GetInstance() { lock (_locker) { return _instance ??= new DisPlayManager(); } }
        public ObservableCollection<IDisPlayControl> IDisPlayControls { get; private set; }
        private const string DragDataFormat = "ColorVision.UI.DisPlayControl";
        private IDisPlayControl? _selectedControl;
        private IDisPlayControl? _dragSourceControl;
        private Point _dragStartPoint;
        private bool _suppressCollectionChanged;
        private bool _isInitialized;

        private DisPlayManager()
        {
            IDisPlayControls = new ObservableCollection<IDisPlayControl>();
        }

        public StackPanel StackPanel { get; set; } = null!;

        public void Init(Window window, StackPanel stackPanel)
        {
            StackPanel = stackPanel;
            StackPanel.AllowDrop = true;
            StackPanel.Drop -= StackPanel_Drop;
            StackPanel.Drop += StackPanel_Drop;

            IDisPlayControls.CollectionChanged -= IDisPlayControls_CollectionChanged;
            IDisPlayControls.CollectionChanged += IDisPlayControls_CollectionChanged;
            _isInitialized = true;
            RebuildPanel();

            if (IDisPlayControls.Count > 0)
            {
                int index = DisPlayManagerConfig.Instance.LastSelectIndex;
                if (index < 0 || index >= IDisPlayControls.Count)
                {
                    index = 0;
                    DisPlayManagerConfig.Instance.LastSelectIndex = index;
                }

                SelectControl(IDisPlayControls[index]);
            }
        }

        private void IDisPlayControls_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressCollectionChanged || !_isInitialized)
                return;

            RebuildPanel();
        }

        public void SelectControl(IDisPlayControl disPlayControl)
        {
            if (_selectedControl != null && !ReferenceEquals(_selectedControl, disPlayControl))
                _selectedControl.IsSelected = false;

            _selectedControl = disPlayControl;
            if (!disPlayControl.IsSelected)
                disPlayControl.IsSelected = true;

            if (StackPanel != null)
                StackPanel.Tag = disPlayControl;

            int index = IDisPlayControls.IndexOf(disPlayControl);
            if (index >= 0)
                DisPlayManagerConfig.Instance.LastSelectIndex = index;
        }

        private static void EnsureConfigCollections()
        {
            var config = DisPlayManagerConfig.Instance;
            config.StoreIndex ??= new Dictionary<string, int>();
            config.ControlGroups ??= new Dictionary<string, string>();
            config.Groups ??= new ObservableCollection<DisPlayGroupConfig>();
        }

        private static DisPlayGroupConfig EnsureDefaultGroup()
        {
            EnsureConfigCollections();
            var config = DisPlayManagerConfig.Instance;
            var defaultGroup = config.Groups.FirstOrDefault(a => a.Id == DisPlayManagerConfig.DefaultGroupId);
            if (defaultGroup != null)
                return defaultGroup;

            defaultGroup = new DisPlayGroupConfig
            {
                Id = DisPlayManagerConfig.DefaultGroupId,
                Name = "默认分组",
                IsExpanded = true
            };
            config.Groups.Insert(0, defaultGroup);
            return defaultGroup;
        }

        private static bool IsDefaultGroup(string groupId) => groupId == DisPlayManagerConfig.DefaultGroupId;

        private static List<DisPlayGroupConfig> GetGroupsInOrder()
        {
            EnsureDefaultGroup();

            var config = DisPlayManagerConfig.Instance;
            return config.Groups
                .Where(a => !string.IsNullOrWhiteSpace(a.Id))
                .GroupBy(a => a.Id)
                .Select(a => a.First())
                .OrderBy(a => IsDefaultGroup(a.Id) ? 0 : 1)
                .ThenBy(a => config.Groups.IndexOf(a))
                .ToList();
        }

        private static string GetGroupKey(IDisPlayControl disPlayControl)
        {
            EnsureDefaultGroup();
            var config = DisPlayManagerConfig.Instance;
            if (config.ControlGroups.TryGetValue(disPlayControl.DisPlayName, out string? groupId)
                && config.Groups.Any(a => a.Id == groupId))
            {
                return groupId;
            }

            return DisPlayManagerConfig.DefaultGroupId;
        }

        private static int GetGroupOrder(string groupId)
        {
            var groups = GetGroupsInOrder().ToList();
            int index = groups.FindIndex(a => a.Id == groupId);
            return index < 0 ? int.MaxValue : index;
        }

        private static int GetStoredIndex(IDisPlayControl disPlayControl)
        {
            return DisPlayManagerConfig.Instance.StoreIndex.TryGetValue(disPlayControl.DisPlayName, out int index)
                ? index
                : int.MaxValue;
        }

        private static int CompareDisplayControls(IDisPlayControl a, IDisPlayControl b)
        {
            int groupCompare = GetGroupOrder(GetGroupKey(a)).CompareTo(GetGroupOrder(GetGroupKey(b)));
            if (groupCompare != 0)
                return groupCompare;

            int indexCompare = GetStoredIndex(a).CompareTo(GetStoredIndex(b));
            if (indexCompare != 0)
                return indexCompare;

            return string.Compare(a.DisPlayName, b.DisPlayName, StringComparison.OrdinalIgnoreCase);
        }

        private List<IDisPlayControl> GetControlsInGroup(string groupId)
        {
            return IDisPlayControls
                .Where(a => GetGroupKey(a) == groupId)
                .OrderBy(GetStoredIndex)
                .ThenBy(a => a.DisPlayName)
                .ToList();
        }

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

            StackPanel.Children.Add(CreateGroupManagerFooter());
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

        private static string GetGroupDisplayName(DisPlayGroupConfig group)
        {
            if (IsDefaultGroup(group.Id))
                return "默认";

            return string.IsNullOrWhiteSpace(group.Name) ? "未命名分组" : group.Name.Trim();
        }

        private static void AttachGroupDropFeedback(Border border)
        {
            border.MouseEnter += (s, e) => SetGroupHeaderHighlight(border, true);
            border.MouseLeave += (s, e) => SetGroupHeaderHighlight(border, false);
            border.DragEnter += GroupDropTarget_DragEnter;
            border.DragOver += GroupDropTarget_DragOver;
            border.DragLeave += GroupDropTarget_DragLeave;
            border.Drop += GroupDropTarget_Drop;
        }

        private static void GroupDropTarget_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border border && e.Data.GetDataPresent(DragDataFormat))
            {
                SetGroupHeaderHighlight(border, true);
                e.Effects = DragDropEffects.Move;
            }
        }

        private static void GroupDropTarget_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DragDataFormat))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private static void GroupDropTarget_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                SetGroupHeaderHighlight(border, false);
        }

        private static void GroupDropTarget_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                SetGroupHeaderHighlight(border, false);
        }

        private static void SetGroupHeaderHighlight(Border border, bool isHighlighted)
        {
            border.SetResourceReference(Border.BackgroundProperty, isHighlighted ? "GlobalBorderBrush1" : "GlobalBackground");
        }

        private ContextMenu CreateGroupContextMenu(DisPlayGroupConfig group)
        {
            var contextMenu = new ContextMenu();

            var renameItem = new MenuItem { Header = "重命名" };
            renameItem.Click += (s, e) => RenameGroup(group);
            contextMenu.Items.Add(renameItem);

            if (!IsDefaultGroup(group.Id))
            {
                var deleteItem = new MenuItem { Header = "删除分组" };
                deleteItem.Click += (s, e) => DeleteGroup(group);
                contextMenu.Items.Add(deleteItem);
            }

            return contextMenu;
        }

        private Border CreateGroupManagerFooter()
        {
            var border = new Border
            {
                Margin = new Thickness(0, 2, 3, 2),
                Padding = new Thickness(4, 3, 2, 3),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            border.SetResourceReference(Border.BorderBrushProperty, "GlobalBorderBrush");

            var dockPanel = new DockPanel();
            border.Child = dockPanel;

            var button = new Button
            {
                Content = "+ 分组",
                MinWidth = 56,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonDefault.Small");
            button.Click += (s, e) => CreateGroup();
            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);

            int groupCount = DisPlayManagerConfig.Instance.Groups.Count(a => !IsDefaultGroup(a.Id));
            var textBlock = new TextBlock
            {
                Text = groupCount > 0 ? $"{groupCount} 个分组" : "分组",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.55
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            dockPanel.Children.Add(textBlock);

            return border;
        }

        private void AddDisplayControl(StackPanel panel, IDisPlayControl item)
        {
            if (item is not UserControl userControl)
                return;

            userControl.AllowDrop = true;
            userControl.PreviewMouseLeftButtonDown -= DisplayControl_PreviewMouseLeftButtonDown;
            userControl.PreviewMouseLeftButtonDown += DisplayControl_PreviewMouseLeftButtonDown;
            userControl.PreviewMouseMove -= DisplayControl_PreviewMouseMove;
            userControl.PreviewMouseMove += DisplayControl_PreviewMouseMove;
            userControl.Drop -= DisplayControl_Drop;
            userControl.Drop += DisplayControl_Drop;
            panel.Children.Add(userControl);
        }

        private void DisplayControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSourceControl = null;
            if (sender is IDisPlayControl disPlayControl && sender is DependencyObject owner && CanStartDisplayDrag(e.OriginalSource as DependencyObject, owner))
            {
                _dragSourceControl = disPlayControl;
                _dragStartPoint = e.GetPosition(null);
            }
        }

        private void DisplayControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSourceControl == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (_dragSourceControl is UserControl userControl)
            {
                var data = new DataObject(DragDataFormat, _dragSourceControl.DisPlayName);
                DragDrop.DoDragDrop(userControl, data, DragDropEffects.Move);
            }
            _dragSourceControl = null;
        }

        private static bool CanStartDisplayDrag(DependencyObject? source, DependencyObject owner)
        {
            ToggleButton? headerToggle = null;
            while (source != null)
            {
                if (source is TextBoxBase || source is Slider || source is ComboBox || source is ScrollBar || source is Thumb)
                    return false;

                if (source is FrameworkElement { Name: "DisPlayBorder" })
                    return false;

                if (headerToggle == null && source is ToggleButton toggleButton && IsDisplayHeaderToggle(toggleButton))
                    headerToggle = toggleButton;

                if (ReferenceEquals(source, owner))
                    return headerToggle != null;

                source = GetDisplayDragParent(source);
            }
            return headerToggle != null;
        }

        private static DependencyObject? GetDisplayDragParent(DependencyObject source)
        {
            if (source is Visual || source is Visual3D)
                return VisualTreeHelper.GetParent(source);

            if (source is FrameworkContentElement contentElement)
                return contentElement.Parent;

            return LogicalTreeHelper.GetParent(source);
        }

        private static bool IsDisplayHeaderToggle(ToggleButton toggleButton)
        {
            return toggleButton.Style != null && ReferenceEquals(toggleButton.Style, toggleButton.TryFindResource("ButtonPageControl1"));
        }

        private void DisplayControl_Drop(object sender, DragEventArgs e)
        {
            if (sender is not IDisPlayControl targetControl || !TryGetDraggedDisplayName(e, out string draggedName))
                return;

            string targetGroupId = GetGroupKey(targetControl);
            var controls = GetControlsInGroup(targetGroupId).Where(a => a.DisPlayName != draggedName).ToList();
            int targetIndex = controls.FindIndex(a => ReferenceEquals(a, targetControl));
            if (targetIndex < 0)
                targetIndex = controls.Count;

            if (sender is FrameworkElement targetElement && e.GetPosition(targetElement).Y > targetElement.ActualHeight / 2)
                targetIndex++;

            MoveControlToGroup(draggedName, targetGroupId, targetIndex);
            e.Handled = true;
        }

        private void Group_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedDisplayName(e, out string draggedName))
                return;

            string groupId = (sender as FrameworkElement)?.Tag as string ?? DisPlayManagerConfig.DefaultGroupId;
            MoveControlToGroup(draggedName, groupId, GetControlsInGroup(groupId).Count);
            e.Handled = true;
        }

        private void StackPanel_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedDisplayName(e, out string draggedName))
                return;

            MoveControlToGroup(draggedName, DisPlayManagerConfig.DefaultGroupId, GetControlsInGroup(DisPlayManagerConfig.DefaultGroupId).Count);
            e.Handled = true;
        }

        private static bool TryGetDraggedDisplayName(DragEventArgs e, out string displayName)
        {
            displayName = e.Data.GetDataPresent(DragDataFormat)
                ? e.Data.GetData(DragDataFormat) as string ?? string.Empty
                : string.Empty;

            return !string.IsNullOrWhiteSpace(displayName);
        }

        private void MoveControlToGroup(string displayName, string groupId, int insertIndex)
        {
            EnsureDefaultGroup();
            if (!DisPlayManagerConfig.Instance.Groups.Any(a => a.Id == groupId))
                groupId = DisPlayManagerConfig.DefaultGroupId;

            var draggedControl = IDisPlayControls.FirstOrDefault(a => a.DisPlayName == displayName);
            if (draggedControl == null)
                return;

            var config = DisPlayManagerConfig.Instance;
            string oldGroupId = GetGroupKey(draggedControl);
            config.ControlGroups[displayName] = groupId;

            var targetControls = GetControlsInGroup(groupId).Where(a => a.DisPlayName != displayName).ToList();
            insertIndex = Math.Clamp(insertIndex, 0, targetControls.Count);
            targetControls.Insert(insertIndex, draggedControl);
            UpdateGroupIndexes(targetControls);

            if (oldGroupId != groupId)
                UpdateGroupIndexes(GetControlsInGroup(oldGroupId).ToList());

            ArrangeCollectionByConfig();
            RebuildPanel();
        }

        private static void UpdateGroupIndexes(List<IDisPlayControl> controls)
        {
            for (int i = 0; i < controls.Count; i++)
                DisPlayManagerConfig.Instance.StoreIndex[controls[i].DisPlayName] = i;
        }

        private void ArrangeCollectionByConfig()
        {
            _suppressCollectionChanged = true;
            try
            {
                IDisPlayControls.Sort(CompareDisplayControls);
            }
            finally
            {
                _suppressCollectionChanged = false;
            }
        }

        private void CreateGroup()
        {
            string defaultName = $"分组 {DisPlayManagerConfig.Instance.Groups.Count(a => !IsDefaultGroup(a.Id)) + 1}";
            string? name = ShowTextDialog("新建分组", "分组名称", defaultName);
            if (string.IsNullOrWhiteSpace(name))
                return;

            DisPlayManagerConfig.Instance.Groups.Add(new DisPlayGroupConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name.Trim(),
                IsExpanded = true
            });

            RebuildPanel();
        }

        private void RenameGroup(DisPlayGroupConfig group)
        {
            string? name = ShowTextDialog("重命名分组", "分组名称", group.Name);
            if (string.IsNullOrWhiteSpace(name))
                return;

            group.Name = name.Trim();
            RebuildPanel();
        }

        private void DeleteGroup(DisPlayGroupConfig group)
        {
            if (IsDefaultGroup(group.Id))
                return;

            MessageBoxResult result = MessageBox.Show(Application.Current.GetActiveWindow(), $"删除分组“{group.Name}”？分组内控件会移回默认分组。", "ColorVision", MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK)
                return;

            var config = DisPlayManagerConfig.Instance;
            foreach (var key in config.ControlGroups.Where(a => a.Value == group.Id).Select(a => a.Key).ToList())
                config.ControlGroups[key] = DisPlayManagerConfig.DefaultGroupId;

            config.Groups.Remove(group);
            ArrangeCollectionByConfig();
            RebuildPanel();
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

            var okButton = new Button { Content = "确定", Width = 70, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
            var cancelButton = new Button { Content = "取消", Width = 70, IsCancel = true };
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

        public void RestoreControl()
        {
            EnsureDefaultGroup();
            ArrangeCollectionByConfig();

            foreach (var group in GetGroupsInOrder())
                UpdateGroupIndexes(GetControlsInGroup(group.Id).ToList());

            if (_isInitialized)
                RebuildPanel();
        }
    }
}
