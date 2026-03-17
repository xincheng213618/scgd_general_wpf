using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow.NodeConfigurator
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class NodeConfiguratorAttribute : Attribute
    {
        public Type NodeType { get; }
        public NodeConfiguratorAttribute(Type nodeType) => NodeType = nodeType;
    }

    public interface INodeConfigurator
    {
        void Configure(NodeConfiguratorContext context);
    }

    public abstract class NodeConfiguratorBase : INodeConfigurator
    {
        public abstract void Configure(NodeConfiguratorContext context);
    }

    public static class NodeConfiguratorRegistry
    {
        private static ConcurrentDictionary<Type, INodeConfigurator> _configurators;

        public static INodeConfigurator? GetConfigurator(Type nodeType)
        {
            EnsureInitialized();
            if (_configurators.TryGetValue(nodeType, out var configurator))
                return configurator;
            return null;
        }

        private static void EnsureInitialized()
        {
            if (_configurators != null) return;
            _configurators = new ConcurrentDictionary<Type, INodeConfigurator>();

            var assemblies = AssemblyHandler.Instance.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); } catch { continue; }

                foreach (var type in types)
                {
                    if (!typeof(INodeConfigurator).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                        continue;

                    var attr = type.GetCustomAttribute<NodeConfiguratorAttribute>();
                    if (attr != null && Activator.CreateInstance(type) is INodeConfigurator configurator)
                    {
                        _configurators[attr.NodeType] = configurator;
                    }
                }
            }
        }
    }

    public class NodeConfiguratorContext
    {
        public STNode Node { get; set; }
        public StackPanel SignStackPanel { get; set; }
        public STNodePropertyGrid STNodePropertyGrid { get; set; }
        public STNodeEditor STNodeEditor { get; set; }
        public StackPanel PropertyStackPanel { get; set; }
        public Action OnActiveChanged { get; set; }

        public void RefreshPropertyEditor()
        {
            STNodePropertyGrid?.Refresh();
            PropertyStackPanel?.Children.Clear();
            PropertyStackPanel?.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Node, ST.Library.UI.Properties.Resources.ResourceManager));
        }

        public void AddImagePath(Action<string> updateStorageAction, string filename, string tag = "图像")
        {
            var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock
            {
                Text = tag,
                Width = 70,
                Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"]
            });

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

            SignStackPanel.Children.Add(dockPanel);
        }

        public void AddDevicePanel<T>(Action<string> updateStorageAction, string deviceCode, string signName, List<T> itemSource) where T : DeviceService
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });

            HandyControl.Controls.ComboBox comboBox = new HandyControl.Controls.ComboBox()
            {
                DisplayMemberPath = "Code",
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
                RefreshPropertyEditor();
                OnActiveChanged?.Invoke();
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
            SignStackPanel.Children.Add(dockPanel);
        }

        public void AddTemplateCollectionPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ObservableCollection<TemplateModel<T>> itemSource) where T : ParamModBase
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 70, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });

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
                RefreshPropertyEditor();
            };

            dockPanel.Children.Add(comboBox);
            SignStackPanel.Children.Add(dockPanel);
        }

        public void AddTemplateJsonPanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplateJson<T> template) where T : TemplateJsonParam, new()
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 70, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });
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
                RefreshPropertyEditor();
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

            SignStackPanel.Children.Add(dockPanel);
        }

        public void AddTemplatePanel<T>(Action<string> updateStorageAction, string tempName, string signName, ITemplate<T> template) where T : ParamModBase, new()
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 70, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });

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
                RefreshPropertyEditor();
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

            SignStackPanel.Children.Add(dockPanel);
        }

        public void AddTemplateKBPanel(Action<string> updateStorageAction, string tempName, string signName, TemplateKB template)
        {
            DockPanel dockPanel = new DockPanel() { Margin = new Thickness(0, 0, 0, 2) };
            dockPanel.Children.Add(new TextBlock() { Text = signName, Width = 30, Foreground = (Brush)Application.Current.Resources["GlobalTextBrush"] });
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
                RefreshPropertyEditor();
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
            SignStackPanel.Children.Add(dockPanel);
        }
    }
}
