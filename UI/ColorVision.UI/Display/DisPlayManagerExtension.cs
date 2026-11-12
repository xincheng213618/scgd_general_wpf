using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI
{
    public static class DisPlayManagerExtension
    {

        /// <summary>
        /// 注册视图控件到 DockViewManager。
        /// 控件只需是 UserControl，标题通过 title 参数传入。
        /// 双击控件时激活对应视图，单击只保留控件自身的选择和编辑行为。
        /// </summary>
 


        public static void ApplyChangedSelectedColor(this IDisPlayControl disPlayControl, Border border)
        {
            disPlayControl.SelectChanged += (s, e) => UpdateDisPlayBorder(disPlayControl, border);
            SubscribeToThemeChanges(
                new WeakReference<IDisPlayControl>(disPlayControl),
                new WeakReference<Border>(border));
            UpdateDisPlayBorder(disPlayControl, border);
            if (disPlayControl is UserControl userControl)
                RegisterSelectionInput(disPlayControl, userControl);
        }

        private static void SubscribeToThemeChanges(
            WeakReference<IDisPlayControl> displayReference,
            WeakReference<Border> borderReference)
        {
            ThemeManager themeManager = ThemeManager.Current;
            ThemeChangedHandler? themeChangedHandler = null;
            themeChangedHandler = (s) =>
            {
                if (displayReference.TryGetTarget(out IDisPlayControl? target)
                    && borderReference.TryGetTarget(out Border? targetBorder))
                {
                    UpdateDisPlayBorder(target, targetBorder);
                    return;
                }

                themeManager.CurrentUIThemeChanged -= themeChangedHandler;
            };
            themeManager.CurrentUIThemeChanged += themeChangedHandler;
        }

        private static void UpdateDisPlayBorder(IDisPlayControl target, Border targetBorder)
        {
            if (target.IsSelected)
            {
                targetBorder.BorderBrush = ImageUtils.ConvertFromString(ThemeManager.Current.CurrentUITheme switch
                {
                    Theme.Light => "#5649B0",
                    Theme.Dark => "#A79CF1",
                    _ => "#A79CF1" // 默认颜色
                });
            }
            else
            {
                targetBorder.SetResourceReference(Border.BorderBrushProperty, "CV.Border.Weak");
            }
        }

        internal static void RegisterSelectionInput(IDisPlayControl disPlayControl, UserControl userControl)
        {
            userControl.Focusable = true;
            userControl.PreviewMouseDown += (s, e) =>
            {
                DisPlayManager.GetInstance().SelectControl(disPlayControl);
            };
            userControl.MouseDown += (s, e) =>
            {
                userControl.Focus();
            };
        }

        public static void AddViewConfig(this UserControl userControl, UserControl viewControl, string title)
        {
            var manager = DockViewManager.GetInstance();
            if (userControl is IDisPlayControl display)
            {
                string key = DisPlayManager.GetControlKey(display);
                if (!string.IsNullOrWhiteSpace(key)) manager.SetViewIdentity(viewControl, key);
                manager.SetViewVisibility(viewControl, DisPlayManager.IsControlVisible(display));
            }
            if (!string.IsNullOrEmpty(title))
                manager.SetViewTitle(viewControl, title);
            manager.AddView(viewControl);

            userControl.MouseDoubleClick += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;

                if (DisplayPinButton.IsPinInput(e.OriginalSource as DependencyObject))
                    return;

                manager.ActiveView(viewControl);
                e.Handled = true;
            };
        }

        public static void AddViewConfig(this UserControl userControl, DockViewRegistration registration, string title)
        {
            var manager = DockViewManager.GetInstance();
            registration.Title = title;
            if (userControl is IDisPlayControl display)
            {
                registration.PersistenceKey = DisPlayManager.GetControlKey(display);
                registration.IsVisible = DisPlayManager.IsControlVisible(display);
                if (registration.Current is { } current && !string.IsNullOrWhiteSpace(registration.PersistenceKey))
                    manager.SetViewIdentity(current, registration.PersistenceKey);
            }
            manager.RegisterView(registration);
            userControl.MouseDoubleClick += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Left || DisplayPinButton.IsPinInput(e.OriginalSource as DependencyObject)) return;
                manager.OpenView(registration);
                e.Handled = true;
            };
        }

    }
}
