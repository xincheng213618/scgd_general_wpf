using ColorVision.Adorners;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI
{


    public static class DisPlayManagerExtension
    {

        public static void AddViewConfig(this UserControl userControl, IView view, ComboBox comboBox)
        {
            if (view is not Control control) throw new NotImplementedException();

            ViewGridManager.GetInstance().AddView(control);


            void ViewMaxChangedEvent(int max)
            {
                List<KeyValuePair<string, int>> KeyValues = new()
                {
                    new KeyValuePair<string, int>(UI.Properties.Resources.WindowSingle, -2),
                    new KeyValuePair<string, int>(UI.Properties.Resources.WindowHidden, -1)
                };
                for (int i = 0; i < max; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                comboBox.ItemsSource = KeyValues;

                if (view.View.ViewIndex >= max)
                {
                    view.View.ViewIndex = -1;
                }
                comboBox.SelectedValue = view.View.ViewIndex;
            }

            ViewMaxChangedEvent(ViewGridManager.GetInstance().ViewMax);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += ViewMaxChangedEvent;

            //移除掉所有的订阅者
            view.View.ClearViewIndexChangedSubscribers();

            view.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                comboBox.SelectedIndex = e2 + 2;
            };
            comboBox.SelectionChanged += (s, e) =>
            {
                if (s is ComboBox combo && combo.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    view.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(control, KeyValue.Value);
                }
            };
            view.View.ViewIndex = -1;

            if (userControl is IDisPlayControl disPlayControl)
            {
                disPlayControl.Selected += (s, e) =>
                {
                    if (ViewConfig.Instance.IsAutoSelect)
                    {
                        if (ViewGridManager.GetInstance().ViewMax == 1)
                        {
                            view.View.ViewIndex = 0;
                            ViewGridManager.GetInstance().SetViewIndex(control, 0);
                        }
                    }
                };

            }

        }


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
            disPlayControl.SelectChanged += (s, e) =>
            {
                UpdateDisPlayBorder();
                if (disPlayControl.IsSelected)
                {
                    DisPlayManager.GetInstance().SelectedControlChanged?.Invoke(DisPlayManager.GetInstance(), disPlayControl);
                }
            };
            ThemeManager.Current.CurrentUIThemeChanged += (s) => UpdateDisPlayBorder();
            UpdateDisPlayBorder();
            if (disPlayControl is UserControl userControl)
            {
                userControl.Focusable = true;
                userControl.MouseDown += (s, e) =>
                {
                    if (userControl.Parent is StackPanel stackPanel)
                    {
                        if (stackPanel.Tag is IDisPlayControl lastDisPlayControl)
                            lastDisPlayControl.IsSelected = false;
                        stackPanel.Tag = userControl;
                        disPlayControl.IsSelected = true;
                    }
                    userControl.Focus();
                    DisPlayManagerConfig.Instance.LastSelectIndex = DisPlayManager.GetInstance().IDisPlayControls.IndexOf(disPlayControl);
                };
            }
        }
    }





    public class DisPlayManagerConfig : ViewModelBase,IConfig
    {
        public static DisPlayManagerConfig Instance => ConfigService.Instance.GetRequiredService<DisPlayManagerConfig>();
        public Dictionary<string, int> StoreIndex { get; set; } = new Dictionary<string, int>();

        public int LastSelectIndex { get => _LastSelectIndex; set { _LastSelectIndex = value; OnPropertyChanged(); } }
        private int _LastSelectIndex ;
    }



    public class DisPlayManager
    {
        private static DisPlayManager _instance;
        private static readonly object _locker = new();
        public static DisPlayManager GetInstance() { lock (_locker) { return _instance ??= new DisPlayManager(); } }
        public ObservableCollection<IDisPlayControl> IDisPlayControls { get; private set; }

        /// <summary>
        /// 当控件选中状态改变时触发，用于更新状态栏信息
        /// </summary>
        public event EventHandler<IDisPlayControl> SelectedControlChanged;

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
                        DisPlayManagerConfig.Instance.StoreIndex[disPlayControl.DisPlayName] = i;
                }
            };

            if (IDisPlayControls.Count > 0)
            {
                int index = DisPlayManagerConfig.Instance.LastSelectIndex;
                if (index < 0 || index >= IDisPlayControls.Count)
                {
                    index = 0;
                    DisPlayManagerConfig.Instance.LastSelectIndex = index;
                }

                var selectedControl = IDisPlayControls[index];
                selectedControl.IsSelected = true;
                StackPanel.Tag = selectedControl;
            }

        }

        public void RestoreControl()
        {
            var nameToIndexMap = DisPlayManagerConfig.Instance.StoreIndex;
            IDisPlayControls.Sort((a, b) =>
            {
                if (nameToIndexMap.TryGetValue(a.DisPlayName, out int indexA) && nameToIndexMap.TryGetValue(b.DisPlayName, out int indexB))
                {
                    return indexA.CompareTo(indexB);
                }
                else if (nameToIndexMap.ContainsKey(a.DisPlayName))
                {
                    return -1; // a should come before b
                }
                else if (nameToIndexMap.ContainsKey(b.DisPlayName))
                {
                    return 1; // b should come before a
                }
                return 0; // keep original order if neither a nor b are in playControls
            });

            for (int i = 0; i < IDisPlayControls.Count; i++)
                DisPlayManagerConfig.Instance.StoreIndex[IDisPlayControls[i].DisPlayName] = i;
        }


    }
}
