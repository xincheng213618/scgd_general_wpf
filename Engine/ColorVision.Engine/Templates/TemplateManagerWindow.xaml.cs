using ColorVision.Engine.Services;
using ColorVision.UI;
using ColorVision.UI.Menus;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates
{

    public class MenuTemplateManagerWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "模板管理";
        public override int Order => 3;

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
        List<KeyValuePair<string, ITemplateName>> keyValuePairs = new List<KeyValuePair<string, ITemplateName>>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            keyValuePairs = TemplateControl.ITemplateNames.ToList();
            ListView2.ItemsSource = keyValuePairs;
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
                if (keyValuePairs[ListView2.SelectedIndex].Value is ITemplate template)
                {
                    new TemplateEditorWindow(template) { Owner = Application.Current.GetActiveWindow() }.Show();
                }
            }
        }
    }
}
