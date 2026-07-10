using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class RoiPropertiesEditor : IPropertyEditor
    {
        private static readonly string[] FieldToolTips = new[] { "X", "Y", "Width", "Height" };

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            if (obj is not PhyCameraCfg config)
            {
                throw new InvalidOperationException("ROI editor only supports PhyCameraCfg.");
            }

            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel { LastChildFill = true };
            var label = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(label);

            var editorPanel = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = PropertyEditorHelper.ControlMinWidth
            };

            var button = new Button
            {
                Content = Properties.Resources.Settings,
                MinWidth = 64,
                Height = 26,
                Padding = new Thickness(10, 1, 10, 1),
                Margin = new Thickness(8, 0, 0, 0)
            };
            DockPanel.SetDock(button, Dock.Right);

            var editorGrid = new Grid
            {
                MinWidth = PropertyEditorHelper.ControlMinWidth
            };

            var textBoxes = new TextBox[FieldToolTips.Length];
            for (int i = 0; i < FieldToolTips.Length; i++)
            {
                editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var textBox = CreateFieldTextBox(FieldToolTips[i], i == 0);
                Grid.SetColumn(textBox, i);
                editorGrid.Children.Add(textBox);
                textBoxes[i] = textBox;
            }

            bool isRefreshing = false;

            void Refresh()
            {
                isRefreshing = true;
                textBoxes[0].Text = config.PointX.ToString(CultureInfo.InvariantCulture);
                textBoxes[1].Text = config.PointY.ToString(CultureInfo.InvariantCulture);
                textBoxes[2].Text = config.Width.ToString(CultureInfo.InvariantCulture);
                textBoxes[3].Text = config.Height.ToString(CultureInfo.InvariantCulture);
                isRefreshing = false;
            }

            void Commit()
            {
                if (isRefreshing)
                {
                    return;
                }

                if (!TryParseInt(textBoxes[0].Text, out int x) ||
                    !TryParseInt(textBoxes[1].Text, out int y) ||
                    !TryParseInt(textBoxes[2].Text, out int width) ||
                    !TryParseInt(textBoxes[3].Text, out int height) ||
                    x < 0 ||
                    y < 0 ||
                    width < 0 ||
                    height < 0)
                {
                    Refresh();
                    return;
                }

                var newValue = new Int32Rect(x, y, width, height);
                if (property.GetValue(obj) is Int32Rect oldValue && oldValue.Equals(newValue))
                {
                    return;
                }

                property.SetValue(obj, newValue);
                Refresh();
            }

            foreach (var textBox in textBoxes)
            {
                textBox.LostFocus += (_, _) => Commit();
                textBox.PreviewKeyDown += (sender, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        Commit();
                    }

                    PropertyEditorHelper.TextBox_PreviewKeyDown(sender, e);
                };
            }

            button.Click += (_, _) =>
            {
                var owner = Window.GetWindow(button) ?? Application.Current?.MainWindow;
                var window = new RoiEditorWindow(config)
                {
                    WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
                };
                if (owner != null)
                {
                    window.Owner = owner;
                }

                if (window.ShowDialog() == true)
                {
                    Refresh();
                }
            };

            if (config is INotifyPropertyChanged notifyPropertyChanged)
            {
                void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
                {
                    if (string.IsNullOrEmpty(e.PropertyName) ||
                        e.PropertyName == nameof(PhyCameraCfg.ROI) ||
                        e.PropertyName == nameof(PhyCameraCfg.PointX) ||
                        e.PropertyName == nameof(PhyCameraCfg.PointY) ||
                        e.PropertyName == nameof(PhyCameraCfg.Width) ||
                        e.PropertyName == nameof(PhyCameraCfg.Height) ||
                        e.PropertyName == nameof(PhyCameraCfg.SensorWidth) ||
                        e.PropertyName == nameof(PhyCameraCfg.SensorHeight))
                    {
                        Refresh();
                    }
                }

                notifyPropertyChanged.PropertyChanged += OnPropertyChanged;
                dockPanel.Unloaded += (_, _) => notifyPropertyChanged.PropertyChanged -= OnPropertyChanged;
            }

            editorPanel.Children.Add(button);
            editorPanel.Children.Add(editorGrid);
            dockPanel.Children.Add(editorPanel);

            Refresh();
            return dockPanel;
        }

        private static TextBox CreateFieldTextBox(string toolTip, bool isFirst)
        {
            return new TextBox
            {
                Margin = new Thickness(isFirst ? 0 : 5, 0, 0, 0),
                Style = PropertyEditorHelper.TextBoxSmallStyle,
                ToolTip = toolTip
            };
        }

        private static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
        }
    }
}
