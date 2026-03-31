using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    public partial class AddNewProjectWindow : Window
    {
        private readonly string _targetDirectory;
        private readonly List<IProjectTemplate> _allTemplates;
        private List<IProjectTemplate> _filteredTemplates;

        public IProjectTemplate? SelectedTemplate { get; private set; }
        public string? ProjectName { get; private set; }

        public AddNewProjectWindow(string targetDirectory)
        {
            InitializeComponent();
            _targetDirectory = targetDirectory;
            _allTemplates = ProjectTemplateRegistry.GetTemplates().ToList();

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
                    !t.Description.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
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
            if (TemplateListView.SelectedItem is IProjectTemplate template)
            {
                DescriptionText.Text = template.Description;

                string baseName = "Project1";
                string projectDir = Path.Combine(_targetDirectory, baseName);
                int count = 2;
                while (Directory.Exists(projectDir))
                {
                    baseName = $"Project{count}";
                    projectDir = Path.Combine(_targetDirectory, baseName);
                    count++;
                }

                ProjectNameTextBox.Text = baseName;
                ProjectNameTextBox.SelectAll();
                ProjectNameTextBox.Focus();
            }
            else
            {
                DescriptionText.Text = "";
            }
        }

        private void TemplateListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TryCreate();
        }

        private void ProjectNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                TryCreate();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            TryCreate();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TryCreate()
        {
            if (TemplateListView.SelectedItem is not IProjectTemplate template)
            {
                MessageBox.Show(this, "请选择一个项目模板", "添加新项目", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string projectName = ProjectNameTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(projectName))
            {
                MessageBox.Show(this, "请输入项目名称", "添加新项目", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (projectName.IndexOfAny(invalidChars) >= 0)
            {
                MessageBox.Show(this, "项目名称包含无效字符", "添加新项目", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string projectDir = Path.Combine(_targetDirectory, projectName);
            if (Directory.Exists(projectDir))
            {
                MessageBox.Show(this, $"项目 \"{projectName}\" 已存在", "添加新项目",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedTemplate = template;
            ProjectName = projectName;
            DialogResult = true;
        }
    }
}
