using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services
{
    public static class Extension
    {
        public static void ApplyChangedSelectedColor(this IDisPlayControl disPlayControl,Border border)
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
                userControl.PreviewMouseDown += (s, e) =>
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

    }
}
