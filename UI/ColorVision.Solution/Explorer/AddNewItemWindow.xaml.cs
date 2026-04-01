using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    public partial class AddNewItemWindow : Window
    {
        private readonly string _targetDirectory;
        private readonly List<INewItemTemplate> _allTemplates;
        private List<INewItemTemplate> _filteredTemplates;

        public INewItemTemplate? SelectedTemplate { get; private set; }
        public string? NewFileName { get; private set; }

        public AddNewItemWindow(string targetDirectory)
        {
            InitializeComponent();
            _targetDirectory = targetDirectory;
            _allTemplates = NewItemTemplateRegistry.GetTemplates().ToList();

            InitializeCategories();
            ApplyFilter();
        }

        private void InitializeCategories()
        {
            var categories = new List<string> { "全部" };
            categories.AddRange(_allTemplates.Select(t => t.Category).Distinct().OrderBy(c => c));
            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedIndex = 0;
        }

        private void ApplyFilter()
        {
            string selectedCategory = CategoryComboBox.SelectedItem as string ?? "全部";
            string searchText = SearchBar.Text?.Trim() ?? "";

            _filteredTemplates = _allTemplates.Where(t =>
            {
                if (selectedCategory != "全部" && t.Category != selectedCategory)
                    return false;
                if (!string.IsNullOrEmpty(searchText) &&
                    !t.Name.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) &&
                    !(t.Extension?.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) ?? false))
                    return false;
                return true;
            }).ToList();

            TemplateListView.ItemsSource = _filteredTemplates;
            if (_filteredTemplates.Count > 0)
                TemplateListView.SelectedIndex = 0;
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void TemplateListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateListView.SelectedItem is INewItemTemplate template)
            {
                string baseName = template.GetDefaultFileName() ?? template.Name;
                string ext = template.Extension ?? "";

                // Auto-number if file exists
                string fileName = baseName + ext;
                string fullPath = Path.Combine(_targetDirectory, fileName);
                int count = 2;
                while (File.Exists(fullPath))
                {
                    fileName = $"{baseName}({count}){ext}";
                    fullPath = Path.Combine(_targetDirectory, fileName);
                    count++;
                }

                FileNameTextBox.Text = fileName;
                FileNameTextBox.SelectAll();
                FileNameTextBox.Focus();
            }
        }

        private void TemplateListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TryAdd();
        }

        private void FileNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                TryAdd();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            TryAdd();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TryAdd()
        {
            if (TemplateListView.SelectedItem is not INewItemTemplate template)
            {
                MessageBox.Show(this, "请选择一个模板", "添加新建项", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fileName = FileNameTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(fileName))
            {
                MessageBox.Show(this, "请输入文件名", "添加新建项", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate file name characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(invalidChars) >= 0)
            {
                MessageBox.Show(this, "文件名包含无效字符", "添加新建项", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fullPath = Path.Combine(_targetDirectory, fileName);
            if (File.Exists(fullPath))
            {
                var result = MessageBox.Show(this, $"文件 \"{fileName}\" 已存在，是否覆盖？", "添加新建项",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            SelectedTemplate = template;
            NewFileName = fileName;
            DialogResult = true;
        }
    }
}
