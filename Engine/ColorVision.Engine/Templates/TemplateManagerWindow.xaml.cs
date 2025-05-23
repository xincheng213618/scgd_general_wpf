using ColorVision.Engine.Templates.Menus;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates
{

    public class MenuTemplateManagerWindow : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string Header => ColorVision.Engine.Properties.Resources.Settings;

        public override int Order => 999999;
        public override object? Icon
        {
            get
            {
                TextBlock text = new()
                {
                    Text = "\uE713", // 使用Unicode字符
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                return text;
            }
        }

        public override void Execute()
        {
            new TemplateManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    /// <summary>
    /// TemplateManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateManagerWindow : Window
    {
        public TemplateManagerWindow()
        {
            InitializeComponent();
        }
        List<KeyValuePair<string, ITemplate>> Templates  = new List<KeyValuePair<string, ITemplate>>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            Templates  = TemplateControl.ITemplateNames
                .OrderBy(x => x.Key)
                .ToList();

            // 统计模板总数
            int totalTemplateCount = Templates.Sum(x => x.Value.Count);

            // 绑定到 ListView
            ListView2.ItemsSource = Templates;

            // 显示汇总信息
            SummaryText.Text = $"共计{Templates.Count}类模板，{totalTemplateCount}个模板";
        }

        public ObservableCollection<ISearch> Searches { get; set; } = new ObservableCollection<ISearch>();
        public List<ISearch> filteredResults { get; set; } = new List<ISearch>();

        private readonly char[] Chars = new[] { ' ' };
        private void Searchbox_GotFocus(object sender, RoutedEventArgs e)
        {
            Searches = new ObservableCollection<ISearch>(new SearchProvider().GetSearchItems());
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
                            template.Header.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                            template.GuidId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            ))
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

        }

        private void Searchbox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (ListView1.SelectedIndex > -1)
                {
                    Searchbox.Text = string.Empty;
                    filteredResults[ListView1.SelectedIndex].Command?.Execute(this);
                }
            }
            if (e.Key == System.Windows.Input.Key.Up)
            {
                if (ListView1.SelectedIndex > 0)
                    ListView1.SelectedIndex -= 1;
            }
            if (e.Key == System.Windows.Input.Key.Down)
            {
                if (ListView1.SelectedIndex < filteredResults.Count - 1)
                    ListView1.SelectedIndex += 1;

            }
        }

        private void ListView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                Searchbox.Text = string.Empty;
                filteredResults[ListView1.SelectedIndex].Command?.Execute(this);
            }
        }

        private void ListView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView2.SelectedIndex > -1)
            {
                if (Templates[ListView2.SelectedIndex].Value is ITemplate template)
                {
                    SummaryText1.Text = $"当前选择：{template.Title}，共计{template.Count}个模板";
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

        private void ListView2_MouseDoubleCick(object sender, MouseButtonEventArgs e)
        {
            if (ListView2.SelectedIndex > -1)
            {
                if (Templates[ListView2.SelectedIndex].Value is ITemplate template)
                {
                    new TemplateEditorWindow(template) { Owner = Application.Current.GetActiveWindow() }.Show();
                }
            }
        }
    }
}
