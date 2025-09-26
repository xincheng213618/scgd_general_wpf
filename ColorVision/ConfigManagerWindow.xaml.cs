using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.Common.MVVM; // Added for RelayCommand
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision
{

    public class MenuConfigManagerWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override string Header => "配置管理窗口";

        public override int Order => 9009;

        public override void Execute()
        {
            new ConfigManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    /// <summary>
    /// 用于持久化 ConfigManagerWindow 的位置/尺寸设置
    /// </summary>
    public class ConfigManagerWindowConfig : WindowConfig
    {
        public static ConfigManagerWindowConfig Instance => ConfigService.Instance.GetRequiredService<ConfigManagerWindowConfig>();
    }

    /// <summary>
    /// 针对配置项的搜索封装
    /// </summary>
    internal class ConfigSearch : ISearch
    {
        public SearchType Type => SearchType.Menu; // 复用枚举
        public string? GuidId { get; init; }
        public string? Header { get; init; }
        public object? Icon { get; init; }
        public ICommand? Command { get; init; }
    }

    /// <summary>
    /// ConfigManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigManagerWindow : Window
    {
        public ConfigManagerWindow()
        {
            InitializeComponent();
            // 标题栏主题与图标
            this.ApplyCaption();
            // 恢复窗口位置
            ConfigManagerWindowConfig.Instance.SetWindow(this);
        }
        List<KeyValuePair<Type, IConfig>> Templates = new List<KeyValuePair<Type, IConfig>>();

        public ObservableCollection<ISearch> Searches { get; set; } = new ObservableCollection<ISearch>();
        public List<ISearch> filteredResults { get; set; } = new List<ISearch>();
        private readonly char[] Chars = new[] { ' ' };

        private void Window_Initialized(object sender, EventArgs e)
        {
            // 读取所有配置实例
            Templates = ConfigHandler.GetInstance().Configs.ToList();

            // 排序：按类型名
            Templates = Templates.OrderBy(p => p.Key.Name).ToList();

            // 绑定到 ListView
            ListView2.ItemsSource = Templates;

            // 统计信息（类型数量 == 模板类别，实例数量 == 与类型数量一致）
            int typeCount = Templates.Count;
            SummaryText.Text = $"共计{typeCount}类配置";

            BuildSearchItems();
        }

        private void BuildSearchItems()
        {
            Searches.Clear();
            foreach (var pair in Templates)
            {
                var type = pair.Key;
                var value = pair.Value;

                string displayName = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                    ?? (value?.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name);

                RelayCommand relayCommand = new RelayCommand(_ =>
                {
                    new PropertyEditorWindow(value).Show();
                });

                Searches.Add(new ConfigSearch
                {
                    GuidId = type.FullName,
                    Header = displayName,
                    Command = relayCommand
                });
            }
        }

        private void Searchbox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 预留：如需动态刷新，可在此重新 BuildSearchItems()
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
                        .Where(item => keywords.All(keyword =>
                            (item.Header?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (item.GuidId?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)))
                        .ToList();
                    ListView1.ItemsSource = filteredResults;
                    if (filteredResults.Count > 0)
                    {
                        ListView1.SelectedIndex = 0;
                    }
                }
            }
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 可扩展：显示预览信息
        }

        private void Searchbox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (ListView1.SelectedIndex > -1 && ListView1.Items.Count > 0)
                {
                    Searchbox.Text = string.Empty;
                    filteredResults[ListView1.SelectedIndex].Command?.Execute(this);
                    SearchPopup.IsOpen = false;
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.Up)
            {
                if (ListView1.SelectedIndex > 0)
                {
                    ListView1.SelectedIndex -= 1;
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.Down)
            {
                if (ListView1.SelectedIndex < filteredResults.Count - 1)
                {
                    ListView1.SelectedIndex += 1;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                SearchPopup.IsOpen = false;
            }
        }

        private void ListView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListView1.SelectedIndex > -1 && ListView1.Items.Count > 0)
            {
                Searchbox.Text = string.Empty;
                filteredResults[ListView1.SelectedIndex].Command?.Execute(this);
                SearchPopup.IsOpen = false;
            }
        }

        private void ListView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView2.SelectedIndex > -1)
            {
                if (Templates[ListView2.SelectedIndex].Value is IConfig template)
                {
                    SummaryText1.Text = $"当前选择：{GetDisplayName(Templates[ListView2.SelectedIndex].Key, template)}";
                }
                else
                {
                    SummaryText1.Text = string.Empty;
                }
            }
            else
            {
                SummaryText1.Text = string.Empty;
            }
        }

        private static string GetDisplayName(Type type, IConfig instance)
        {
            return type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                ?? instance.GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                ?? type.Name;
        }

        private void ListView2_MouseDoubleCick(object sender, MouseButtonEventArgs e)
        {
            if (ListView2.SelectedIndex > -1)
            {
                if (Templates[ListView2.SelectedIndex].Value is IConfig template)
                {
                    new PropertyEditorWindow(template).Show();
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ConfigHandler.GetInstance().SaveConfigs();
            MessageBox.Show("保存成功");
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            PlatformHelper.OpenFolderAndSelectFile(ConfigHandler.GetInstance().ConfigFilePath);
        }
    }
}
