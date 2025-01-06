using ColorVision.Adorners;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI
{
    public static class DisPlayManagerExtension
    {
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
                userControl.MouseDown += (s, e) =>
                {
                    if (userControl.Parent is StackPanel stackPanel)
                    {
                        if (stackPanel.Tag is IDisPlayControl lastDisPlayControl)
                            lastDisPlayControl.IsSelected = false;
                        stackPanel.Tag = userControl;
                        disPlayControl.IsSelected = true;
                    }
                };
        }
    }





    public class DisPlayManagerConfig : IConfig
    {
        public static DisPlayManagerConfig Instance => ConfigService.Instance.GetRequiredService<DisPlayManagerConfig>();
        public Dictionary<string, int> PlayControls { get; set; } = new Dictionary<string, int>();
    }

    public class DisPlayManager
    {
        private static DisPlayManager _instance;
        private static readonly object _locker = new();
        public static DisPlayManager GetInstance() { lock (_locker) { return _instance ??= new DisPlayManager(); } }
        public ObservableCollection<IDisPlayControl> IDisPlayControls { get; private set; }

        private DisPlayManager()
        {
            IDisPlayControls = new ObservableCollection<IDisPlayControl>();
        }

        public StackPanel StackPanel { get; set; }

        public void Init(Window window, StackPanel stackPanel)
        {
            StackPanel = stackPanel;
            foreach (var item in IDisPlayControls)
            {
                if (item is UserControl userControl)
                    StackPanel.Children.Add(userControl);
            }
            IDisPlayControls.CollectionChanged += (s, e) =>
            {
                if (s is ObservableCollection<IDisPlayControl> disPlayControls)
                {
                    switch (e.Action)
                    {
                        case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                            if (e.NewItems != null)
                                foreach (IDisPlayControl newItem in e.NewItems)
                                    if (newItem is UserControl userControl)
                                        StackPanel.Children.Add(userControl);
                            break;
                        case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                            if (e.OldItems != null)
                                foreach (IDisPlayControl oldItem in e.OldItems)
                                    if (oldItem is UserControl userControl)
                                        StackPanel.Children.Remove(userControl);
                            break;
                        case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                            if (e.OldItems != null && e.NewItems != null && e.OldItems.Count == e.NewItems.Count)
                            {
                                for (int i = 0; i < e.OldItems.Count; i++)
                                {
                                    IDisPlayControl oldItem = (IDisPlayControl)e.OldItems[i];
                                    IDisPlayControl newItem = (IDisPlayControl)e.NewItems[i];
                                    if (oldItem is UserControl oldUserControl && newItem is UserControl newUserControl)
                                    {
                                        int index = StackPanel.Children.IndexOf(oldUserControl);
                                        if (index >= 0)
                                        {
                                            StackPanel.Children[index] = newUserControl;
                                        }
                                    }
                                }
                            }
                            break;
                        case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                            if (e.OldItems != null && e.NewItems != null)
                            {
                                // Assuming only one item is moved at a time
                                IDisPlayControl movedItem = (IDisPlayControl)e.NewItems[0];
                                if (movedItem is UserControl movedUserControl)
                                {
                                    StackPanel.Children.Remove(movedUserControl);
                                    StackPanel.Children.Insert(e.NewStartingIndex, movedUserControl);
                                }
                            }
                            break;
                        case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                            StackPanel.Children.Clear();
                            break;
                        default:
                            break;
                    }
                }
            };

            var dorners = StackPanel.AddAdorners(window);

            dorners.Changed += (s, e) =>
            {
                for (int i = 0; i < StackPanel.Children.Count; i++)
                {
                    if (StackPanel.Children[i] is IDisPlayControl disPlayControl)
                        DisPlayManagerConfig.Instance.PlayControls[disPlayControl.DisPlayName] = i;
                }
            };

            if (IDisPlayControls.Count > 0)
            {
                IDisPlayControls[0].IsSelected = true;
                StackPanel.Tag = IDisPlayControls[0];
            }

        }


    }
}
