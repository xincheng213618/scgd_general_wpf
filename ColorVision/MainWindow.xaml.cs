using ColorVision.Common.Utilities;
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Serach;
using ColorVision.UI.Shell;
using ColorVision.UI.Views;
using log4net;
using Microsoft.Xaml.Behaviors;
using Microsoft.Xaml.Behaviors.Layout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision
{
    public class CommadnInitialized : IMainWindowInitialized
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CommadnInitialized));

        public Task Initialize()
        {
            log.Info("CommadnInitialized");
            try
            {
                var parser = ArgumentParser.GetInstance();
                parser.AddArgument("cmd", false, "c");
                parser.Parse();

                string cmd = parser.GetValue("cmd");
                if (cmd != null)
                {
                    List<IMenuItem> IMenuItems = new List<IMenuItem>();
                    foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                    {
                        foreach (Type type in assembly.GetTypes().Where(t => typeof(IMenuItem).IsAssignableFrom(t) && !t.IsAbstract))
                        {
                            if (Activator.CreateInstance(type) is IMenuItem menuitem)
                            {
                                IMenuItems.Add(menuitem);
                            }
                        }
                    }
                    if (IMenuItems.Find(a => a.GuidId == cmd) is IMenuItem menuitem1)
                    {
                        log.Info($"Execute{menuitem1.Header}");
                        menuitem1.Command?.Execute(this);
                    }
                }
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }

            return Task.CompletedTask;  
        }
    }


        /// <summary>
        /// Interaction logic for MarkdownViewWindow.xaml
        /// </summary>
        /// 
    public partial class MainWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
        public ViewGridManager ViewGridManager { get; set; }
        public static MainWindowConfig Config => MainWindowConfig.Instance;

        public MainWindow()
        {
            InitializeComponent();
            Config.SetWindow(this);

            var IsAdministrator = Tool.IsAdministrator();
            //Title += $"- {(IsAdministrator ? Properties.Resources.RunAsAdmin : Properties.Resources.NotRunAsAdmin)}";
            Title = "ColorVision";
            this.ApplyCaption();
            this.SetWindowFull(Config);
            
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MenuManager.GetInstance().Menu = Menu1;
            this.SizeChanged += (s, e) =>
            {
                SearchGrid.Visibility = this.ActualWidth < 700 ? Visibility.Collapsed : Visibility.Visible;
                RightMenuItemPanel.Visibility = this.ActualWidth < Menu1.ActualWidth + RightMenuItemPanel.ActualWidth + 100 ? Visibility.Collapsed : Visibility.Visible;
            };

            this.DataContext = Config;

            WorkspaceMainView solutionView = new WorkspaceMainView();
            SolutionGrid.Children.Add(solutionView);

            ViewGridManager = ViewGridManager.GetInstance();
            ViewGridManager.MainView = ViewGrid;

            ViewGridManager.SetViewGrid(ViewConfig.Instance.ViewMaxCount);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) => ViewConfig.Instance.ViewMaxCount = e;

            DisPlayManager.GetInstance().Init(this, StackPanelSPD);

            Debug.WriteLine(Properties.Resources.LaunchSuccess);
            
            SolutionTab1.Content = new TreeViewControl();

            MenuManager.GetInstance().LoadMenuItemFromAssembly();
            this.LoadHotKeyFromAssembly();
            StatusBarGridInitialized();

            Application.Current.MainWindow = this;
            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LoadIMainWindowInitialized();

                    FluidMoveBehavior fluidMoveBehavior = new()
                    {
                        AppliesTo = FluidMoveScope.Children,
                        Duration = TimeSpan.FromSeconds(0.1)
                    };
                    Interaction.GetBehaviors(StackPanelSPD).Add(fluidMoveBehavior);
                });
            });
            ProgramTimer.StopAndReport();

            // 设置快捷键 Ctrl + F
            var gesture = new KeyGesture(Key.F, ModifierKeys.Control);
            var command = new RoutedCommand();
            command.InputGestures.Add(gesture);
            CommandBindings.Add(new CommandBinding(command, FocusSearchBox));
            InitRightMenuItemPanel();

            StartupRegistryChecker.Clear();

            this.AllowDrop = true;
            this.Drop += MainWindow_Drop;

        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);
            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                var fn = a?.First();

                if (File.Exists(fn))
                { 
                    FileProcessorFactory.GetInstance().HandleFile(fn);
                    e.Handled = true;
                }
                else if (Directory.Exists(fn))
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(fn);
                }
            }
        }

        private void InitRightMenuItemPanel()
        {
            var allSettings = new List<MenuItemMetadata>();
            foreach (var item in AssemblyService.Instance.LoadImplementations<IRightMenuItemProvider>())
            {
                allSettings.AddRange(item.GetMenuItems());
            }
            allSettings.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (var item in allSettings)
            {
                Button button = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Width = 20,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 5, 0)
                };
                button.Content = item.Icon;
                button.Command = item.Command;
                RightMenuItemPanel.Children.Add(button);
            }
        }


        private void FocusSearchBox(object sender, ExecutedRoutedEventArgs e)
        {
            Searchbox.Focus();
        }

        public static async void LoadIMainWindowInitialized() 
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IMainWindowInitialized).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IMainWindowInitialized componentInitialize)
                    {
                        try
                        {
                            await componentInitialize.Initialize();
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                        }
                    }
                }
            }
        }


        private void ViewGrid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag.ToString(), out int nums))
            {
                switch (nums)
                {
                    case 20:
                        ViewGridManager.SetViewGridTwo();
                        break;
                    case 21:
                        ViewGridManager.SetViewGrid(2);
                        break;
                    case 30:
                        ViewGridManager.SetViewGridThree();
                        break;
                    case 31:
                        ViewGridManager.SetViewGridThree(false);
                        break;
                    default:
                        ViewGridManager.SetViewGrid(nums);
                        break;
                }
            }
        }


        private void StatusBarGridInitialized()
        {
            ContextMenu contextMenu= new ContextMenu();
            StatusBarGrid.ContextMenu = contextMenu;

            void AddStatusBarIconMetadata(StatusBarMeta statusBarIconMetadata)
            {
                if (statusBarIconMetadata.Type == StatusBarType.Icon)
                {
                    // 创建 StatusBarItem
                    StatusBarItem statusBarItem = new StatusBarItem { ToolTip = statusBarIconMetadata.Description };
                    statusBarItem.DataContext = statusBarIconMetadata.Source;

                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var visibilityBinding = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter")
                        };
                        statusBarItem.SetBinding(VisibilityProperty, visibilityBinding);
                    }
                    // 设置 MouseLeftButtonDown 事件处理程序
                    if (statusBarIconMetadata.Command != null)
                    {
                        statusBarItem.MouseLeftButtonDown += (s, e) => statusBarIconMetadata.Command.Execute(e);
                    }

                    ToggleButton toggleButton = new ToggleButton { IsEnabled = false };
                    if (Application.Current.TryFindResource(statusBarIconMetadata.ButtonStyleName) is Style styleResource)
                        toggleButton.Style = styleResource;

                    // 设置 IsChecked 绑定
                    var isCheckedBinding = new Binding(statusBarIconMetadata.BindingName) { Mode = BindingMode.OneWay };
                    toggleButton.SetBinding(ToggleButton.IsCheckedProperty, isCheckedBinding);
                    statusBarItem.Content = toggleButton;
                    toggleButton.DataContext = statusBarIconMetadata.Source;

                    StatusBarTextDocker.Children.Add(statusBarItem);

                    MenuItem menuItem = new MenuItem() { Header = statusBarIconMetadata.Name };
                    menuItem.Click += (s, e) => menuItem.IsChecked = !menuItem.IsChecked;
                    menuItem.DataContext = statusBarIconMetadata.Source;
                    // 绑定 MenuItem 的 IsChecked 属性到 VisibilityBindingName
                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var isCheckedBinding1 = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Mode = BindingMode.TwoWay,
                        };
                        menuItem.SetBinding(MenuItem.IsCheckedProperty, isCheckedBinding1);
                    }
                    contextMenu.Items.Add(menuItem);
                }
                else if (statusBarIconMetadata.Type == StatusBarType.Text)
                {
                    StatusBarItem statusBarItem = new StatusBarItem();
                    statusBarItem.DataContext = statusBarIconMetadata.Source;

                    var Binding = new Binding(statusBarIconMetadata.BindingName) { Mode = BindingMode.OneWay };
                    statusBarItem.SetBinding(ContentProperty, Binding);


                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var visibilityBinding = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter")
                        };
                        statusBarItem.SetBinding(VisibilityProperty, visibilityBinding);
                    }

                    StatusBarTextDocker.Children.Add(statusBarItem);

                    MenuItem menuItem = new MenuItem() { Header = statusBarIconMetadata.Name };
                    menuItem.Click += (s, e) => menuItem.IsChecked = !menuItem.IsChecked;
                    menuItem.DataContext = statusBarIconMetadata.Source;
                    // 绑定 MenuItem 的 IsChecked 属性到 VisibilityBindingName
                    if (statusBarIconMetadata.VisibilityBindingName != null)
                    {
                        var isCheckedBinding = new Binding(statusBarIconMetadata.VisibilityBindingName)
                        {
                            Mode = BindingMode.TwoWay,
                        };
                        menuItem.SetBinding(MenuItem.IsCheckedProperty, isCheckedBinding);
                    }
                    contextMenu.Items.Add(menuItem);
                }

            }

            var allSettings = new List<StatusBarMeta>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IStatusBarProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IStatusBarProvider configSetting)
                    {
                        allSettings.AddRange(configSetting.GetStatusBarIconMetadata());
                    }
                }
            }

            // 先按 ConfigSettingType 分组，再在每个组内按 Order 排序
            var sortedSettings = allSettings
                .GroupBy(setting => setting.Type)
                .SelectMany(group => group.OrderBy(setting => setting.Order));

            // 将排序后的配置设置添加到集合中
            foreach (var item in sortedSettings)
            {
                AddStatusBarIconMetadata(item);
            }
        }

        public ObservableCollection<ISearch> Searches { get; set; } = new ObservableCollection<ISearch>();
        public List<ISearch> filteredResults { get; set; } = new List<ISearch>();

        private readonly char[] Chars = new[] { ' ' };
        private void Searchbox_GotFocus(object sender, RoutedEventArgs e)
        {
            Searches = new ObservableCollection<ISearch>(SearchManager.GetInstance().GetISearches());
        }
        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string searchtext = textBox.Text;
                if (string.IsNullOrWhiteSpace(searchtext))
                {
                    SearchPopup.IsOpen = false;
                }
                else
                {
                    SearchPopup.IsOpen = true;
                    var keywords = searchtext.Split(Chars, StringSplitOptions.RemoveEmptyEntries);

                    filteredResults = Searches
                        .OfType<ISearch>()
                        .Where(template => keywords.All(keyword =>
                            (!string.IsNullOrEmpty(template.Header) && template.Header.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                            (template.GuidId != null && template.GuidId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        ))
                        .ToList();


                    string everythingpath = "C:\\Program Files\\Everything\\Everything.exe";

                    if (File.Exists(everythingpath))
                    {
                        void Search()
                        {
                            ProcessStartInfo startInfo = new();
                            startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
                            startInfo.WorkingDirectory = Environment.CurrentDirectory;
                            startInfo.FileName = everythingpath;
                            startInfo.Arguments = $"-s {searchtext}";
                            try
                            {
                                Process p = Process.Start(startInfo);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                            }
                        }

                        SearchMeta search = new SearchMeta
                        {
                            GuidId = Guid.NewGuid().ToString(),
                            Header = $"{Properties.Resources.Search} {searchtext}",
                            Command = new Common.MVVM.RelayCommand(a => Search())
                        };
                        filteredResults.Add(search);
                    }


                    // 添加“在浏览器中搜索”选项
                    void SearchInBrowser()
                    {
                        string url = $"https://www.baidu.com/s?wd={Uri.EscapeDataString(searchtext)}";
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                        }
                    }

                    SearchMeta browserSearch = new SearchMeta
                    {
                        GuidId = Guid.NewGuid().ToString(),
                        Header = $"{Properties.Resources.Search} {searchtext}（百度搜索）",
                        Command = new Common.MVVM.RelayCommand(a => SearchInBrowser())
                    };
                    filteredResults.Add(browserSearch);

                    ListViewSearch.ItemsSource = filteredResults;
                    if (filteredResults.Count > 0)
                    {
                        ListViewSearch.SelectedIndex = 0;
                    }
                }
            }
        }

        private void ListViewSearch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Searchbox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (ListViewSearch.SelectedIndex > -1)
                {
                    Searchbox.Text = string.Empty;
                    filteredResults[ListViewSearch.SelectedIndex].Command?.Execute(this);
                }
            }
            if (e.Key == System.Windows.Input.Key.Up)
            {
                if (ListViewSearch.SelectedIndex > 0)
                    ListViewSearch.SelectedIndex -= 1;
            }
            if (e.Key == System.Windows.Input.Key.Down)
            {
                if (ListViewSearch.SelectedIndex < filteredResults.Count - 1)
                    ListViewSearch.SelectedIndex += 1;
            }
        }

        private void ListViewSearch_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListViewSearch.SelectedIndex > -1)
            {
                Searchbox.Text = string.Empty;
                filteredResults[ListViewSearch.SelectedIndex].Command?.Execute(this);
            }
        }
    }
}
