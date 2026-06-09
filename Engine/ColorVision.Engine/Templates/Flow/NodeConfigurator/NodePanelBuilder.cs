#pragma warning disable CS8625
using ColorVision.Engine.Properties;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    public class NodePanelBuilder
    {
        private const double DefaultSignLabelMinWidth = 90;
        private const double ShortSignLabelMinWidth = 40;

        private readonly NodeConfiguratorContext _context;

        public NodePanelBuilder(NodeConfiguratorContext context)
        {
            _context = context;
        }

        private static TextBlock CreateSignLabel(string text, double minWidth = DefaultSignLabelMinWidth)
        {
            return new TextBlock
            {
                Text = text,
                MinWidth = minWidth,
                Margin = new Thickness(0, 0, 6, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = text,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };
        }

        public void Device<TDevice>(Func<string> getDeviceCode, Action<string> updateDeviceCode, string signName = "") where TDevice : DeviceService
        {
            AddDevicePanel(updateDeviceCode, getDeviceCode(), signName, ServiceManager.GetInstance().DeviceServices.OfType<TDevice>().ToList());
        }

        public void AddImagePath(Action<string> updateStorageAction, string filename, string tag = null)
        {
            tag ??= Properties.Resources.Image;
            var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(CreateSignLabel(tag));

            var textBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 0),
                Style = (Style)Application.Current.FindResource("TextBox.Small"),
                Text = filename
            };
            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Common.NativeMethods.Keyboard.PressKey(0x09);
                    e.Handled = true;
                }
            };

            textBox.TextChanged += (s, e) =>
            {
                updateStorageAction?.Invoke(textBox.Text);
            };

            var selectButton = new Button
            {
                Content = "...",
                Margin = new Thickness(5, 0, 0, 0)
            };
            selectButton.Click += (s, e) =>
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog();
#if NET8_0
                if (File.Exists(textBox.Text))
                {
                    openFileDialog.DefaultDirectory = Path.GetDirectoryName(textBox.Text);
                }
#endif
                if (openFileDialog.ShowDialog() == true)
                {
                    textBox.Text = openFileDialog.FileName;
                }
            };
            DockPanel.SetDock(selectButton, Dock.Right);

            var openFolderButton = new Button
            {
                Content = "🗁",
                Margin = new Thickness(5, 0, 0, 0)
            };
            openFolderButton.Click += (s, e) =>
            {
                Common.Utilities.PlatformHelper.OpenFolder(textBox.Text);
            };
            DockPanel.SetDock(openFolderButton, Dock.Right);

            dockPanel.Children.Add(openFolderButton);
            dockPanel.Children.Add(selectButton);
            dockPanel.Children.Add(textBox);

            _context.SignStackPanel.Children.Add(dockPanel);
        }

        public void AddDevicePanel<T>(Action<string> updateStorageAction, string deviceCode, string signName, List<T> itemSource) where T : DeviceService
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                DisplayMemberPath = "Name",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small")
            };

            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = itemSource;
            var selectedItem = itemSource.FirstOrDefault(x => x.Code == deviceCode);
            if (selectedItem != null)
                comboBox.SelectedIndex = itemSource.IndexOf(selectedItem);

            Grid myGrid = new Grid();
            myGrid.DataContext = selectedItem;

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Code;
                    myGrid.DataContext = templateModel;
                }
                updateStorageAction(selectedName);
                _context.RefreshPropertyEditor();
                _context.OnActiveChanged?.Invoke();
            };

            var button = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            var toggleButton = new ToggleButton
            {
                Style = (Style)Application.Current.FindResource("ButtonMQTTConnect"),
                Height = 10,
                Width = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsEnabled = false
            };
            var binding = new Binding("DService.IsAlive")
            {
                Mode = BindingMode.OneWay
            };
            toggleButton.SetBinding(ToggleButton.IsCheckedProperty, binding);
            var image = new Image
            {
                Source = (ImageSource)Application.Current.FindResource("DrawingImageProperty"),
                Height = 18,
                Margin = new Thickness(0)
            };

            var binding1 = new Binding("PropertyCommand")
            {
                Mode = BindingMode.OneWay
            };
            button.SetBinding(Button.CommandProperty, binding1);

            myGrid.Children.Add(toggleButton);
            myGrid.Children.Add(image);
            myGrid.Children.Add(button);

            DockPanel.SetDock(myGrid, Dock.Right);

            dockPanel.Children.Add(myGrid);
            dockPanel.Children.Add(comboBox);
            _context.SignStackPanel.Children.Add(dockPanel);
        }

        public void AddTemplateCollectionPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ObservableCollection<TemplateModel<T>> itemSource) where T : ParamModBase
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(CreateSignLabel(signName));

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                SelectedValuePath = "Value",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small")
            };

            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = itemSource;
            var selectedItem = itemSource.FirstOrDefault(x => x.Key == tempName);
            if (selectedItem != null)
                comboBox.SelectedIndex = itemSource.IndexOf(selectedItem);

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Name;
                }
                updateStorageAction(selectedName);
                _context.RefreshPropertyEditor();
            };

            dockPanel.Children.Add(comboBox);
            _context.SignStackPanel.Children.Add(dockPanel);
        }

        public void AddSensorTemplatePanel(Action<string> updateStorageAction, string tempName, string signName, Func<string?>? getCategory = null)
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(CreateSignLabel(signName));

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                SelectedValuePath = "Value",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small")
            };

            void RefreshItemsSource()
            {
                var allParams = TemplateSensor.AllParams;
                comboBox.ItemsSource = allParams;
                var selectedItem = allParams.FirstOrDefault(x => x.Key == tempName);
                comboBox.SelectedIndex = selectedItem == null ? -1 : allParams.IndexOf(selectedItem);
            }

            TemplateSensor ResolveTemplate(string? selectedTemplateName)
            {
                var category = getCategory?.Invoke();
                if (!string.IsNullOrWhiteSpace(category))
                    return new TemplateSensor(category);

                if (!string.IsNullOrWhiteSpace(selectedTemplateName))
                {
                    var matchedCategory = TemplateSensor.Params.FirstOrDefault(x => x.Value.Any(item => item.Key == selectedTemplateName)).Key;
                    if (!string.IsNullOrWhiteSpace(matchedCategory))
                        return new TemplateSensor(matchedCategory);
                }

                var firstCategory = TemplateSensor.Params.Keys.FirstOrDefault();
                return new TemplateSensor(string.IsNullOrWhiteSpace(firstCategory) ? "Sensor.Default" : firstCategory);
            }

            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            RefreshItemsSource();

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is SensorParam templateModel)
                {
                    selectedName = templateModel.Name;
                }
                tempName = selectedName;
                updateStorageAction(selectedName);
                _context.RefreshPropertyEditor();
            };

            Grid grid = new Grid
            {
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            Image image = new Image
            {
                Source = (ImageSource)Application.Current.Resources["DrawingImageEdit"],
                Width = 12,
                Margin = new Thickness(0)
            };

            grid.Children.Add(image);

            Button buttonEdit = new Button
            {
                Name = "ButtonEdit",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            buttonEdit.Click += (s, e) =>
            {
                var template = ResolveTemplate(tempName);
                var defaultIndex = comboBox.SelectedIndex >= 0 ? comboBox.SelectedIndex : 0;
                if (!string.IsNullOrWhiteSpace(tempName))
                {
                    var selectedItem = template.TemplateParams.FirstOrDefault(x => x.Key == tempName);
                    if (selectedItem != null)
                    {
                        defaultIndex = template.TemplateParams.IndexOf(selectedItem);
                    }
                }

                if (template.TemplateParams.Count == 0)
                {
                    defaultIndex = 0;
                }
                else if (defaultIndex >= template.TemplateParams.Count)
                {
                    defaultIndex = 0;
                }

                new TemplateEditorWindow(template, defaultIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                RefreshItemsSource();
            };

            grid.Children.Add(buttonEdit);
            DockPanel.SetDock(grid, Dock.Right);
            dockPanel.Children.Add(grid);

            dockPanel.Children.Add(comboBox);
            _context.SignStackPanel.Children.Add(dockPanel);
        }

        public void AddTemplateJsonPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplateJson<T> template) where T : TemplateJsonParam, new()
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(CreateSignLabel(signName));
            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                SelectedValuePath = "Value",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small"),
            };

            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = template.TemplateParams;
            var selectedItem = template.TemplateParams.FirstOrDefault(x => x.Key == tempName);
            if (selectedItem != null)
                comboBox.SelectedIndex = template.TemplateParams.IndexOf(selectedItem);

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Name;
                }
                updateStorageAction(selectedName);
                _context.RefreshPropertyEditor();
            };

            TextBlock textBlock = new TextBlock
            {
                Text = "\uE713",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            Button openTemplateEditorButton = new Button
            {
                Width = 20,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(5, 0, 0, 0),
            };
            openTemplateEditorButton.Content = textBlock;
            openTemplateEditorButton.Click += (s, e) =>
            {
                new TemplateEditorWindow(template, comboBox.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            };

            DockPanel.SetDock(openTemplateEditorButton, Dock.Right);
            dockPanel.Children.Add(openTemplateEditorButton);

            dockPanel.Children.Add(comboBox);

            _context.SignStackPanel.Children.Add(dockPanel);
        }

        public void AddTemplatePanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplate<T> template) where T : ParamModBase, new()
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(CreateSignLabel(signName));

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                SelectedValuePath = "Value",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small"),
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = template.TemplateParams;
            var selectedItem = template.TemplateParams.FirstOrDefault(x => x.Key == tempName);
            if (selectedItem != null)
                comboBox.SelectedIndex = template.TemplateParams.IndexOf(selectedItem);

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is T templateModel)
                {
                    selectedName = templateModel.Name;
                }
                updateStorageAction(selectedName);
                _context.RefreshPropertyEditor();
            };

            TextBlock textBlock = new TextBlock
            {
                Text = "\uE713",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            Button openTemplateEditorButton = new Button
            {
                Width = 20,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(5, 0, 0, 0),
            };
            openTemplateEditorButton.Content = textBlock;
            openTemplateEditorButton.Click += (s, e) =>
            {
                new TemplateEditorWindow(template, comboBox.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            };

            DockPanel.SetDock(openTemplateEditorButton, Dock.Right);
            dockPanel.Children.Add(openTemplateEditorButton);

            dockPanel.Children.Add(comboBox);

            _context.SignStackPanel.Children.Add(dockPanel);
        }

        public void AddTemplateKBPanel(Action<string> updateStorageAction, string tempName, string signName, TemplateKB template)
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(CreateSignLabel(signName, ShortSignLabelMinWidth));
            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                SelectedValuePath = "Value",
                DisplayMemberPath = "Key",
                Style = (Style)Application.Current.FindResource("ComboBoxPlus.Small"),
                Width = 120
            };

            HandyControl.Controls.InfoElement.SetShowClearButton(comboBox, true);
            comboBox.ItemsSource = template.TemplateParams;
            var selectedItem = template.TemplateParams.FirstOrDefault(x => x.Key == tempName);
            if (selectedItem != null)
                comboBox.SelectedIndex = template.TemplateParams.IndexOf(selectedItem);

            comboBox.SelectionChanged += (s, e) =>
            {
                string selectedName = string.Empty;

                if (comboBox.SelectedValue is TemplateJsonKBParam templateModel)
                {
                    selectedName = templateModel.Name;
                }
                updateStorageAction(selectedName);
                _context.RefreshPropertyEditor();
            };

            Grid grid = new Grid
            {
                Width = 20,
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            TextBlock textBlock = new TextBlock
            {
                Text = "\uE713",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            Button button = new Button
            {
                Width = 20,
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            button.Click += (s, e) =>
            {
                new TemplateEditorWindow(template, comboBox.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            };

            grid.Children.Add(textBlock);
            grid.Children.Add(button);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(grid);

            Grid grid1 = new Grid
            {
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            Image image = new Image
            {
                Source = (ImageSource)Application.Current.Resources["DrawingImageEdit"],
                Width = 12,
                Margin = new Thickness(0)
            };

            grid1.Children.Add(image);

            Button buttonEdit = new Button
            {
                Name = "ButtonEdit",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            buttonEdit.Click += (s, e) =>
            {
                if (comboBox.SelectedIndex >= 0)
                {
                    new EditPoiParam1(TemplateKB.Params[comboBox.SelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                }
            };

            grid1.Children.Add(buttonEdit);
            dockPanel.Children.Add(grid1);
            _context.SignStackPanel.Children.Add(dockPanel);
        }
    }
}