using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Explorer
{
    public partial class AddNewProjectWindow : Window
    {
        private readonly string _targetDirectory;
        private readonly List<IProjectTemplate> _allTemplates = new();
        private List<IProjectTemplate> _filteredTemplates = new();

        public IProjectTemplate? SelectedTemplate { get; private set; }
        public string? ProjectName { get; private set; }

        public AddNewProjectWindow(string targetDirectory)
        {
            InitializeComponent();
            _targetDirectory = targetDirectory;
            RefreshTemplates();
            ProjectTemplateRegistry.TemplatesChanged += ProjectTemplateRegistry_TemplatesChanged;
        }

        protected override void OnClosed(EventArgs e)
        {
            ProjectTemplateRegistry.TemplatesChanged -= ProjectTemplateRegistry_TemplatesChanged;
            base.OnClosed(e);
        }

        private void ProjectTemplateRegistry_TemplatesChanged(object? sender, EventArgs e)
        {
            if (Dispatcher.CheckAccess())
                RefreshTemplates();
            else
                Dispatcher.BeginInvoke(RefreshTemplates);
        }

        private void RefreshTemplates()
        {
            string? selectedTemplateId = (TemplateListView.SelectedItem as IProjectTemplate)?.Id;
            string? selectedCategory = CategoryComboBox.SelectedItem as string;
            string existingProjectName = ProjectNameTextBox.Text;
            _allTemplates.Clear();
            _allTemplates.AddRange(ProjectTemplateRegistry.GetTemplates());
            InitializeCategories(selectedCategory);
            ApplyFilter(selectedTemplateId);
            if (!string.IsNullOrWhiteSpace(existingProjectName))
            {
                ProjectNameTextBox.Text = existingProjectName;
                ProjectNameTextBox.CaretIndex = existingProjectName.Length;
            }
        }

        private void InitializeCategories(string? selectedCategory = null)
        {
            var categories = new List<string> { "全部" };
            categories.AddRange(_allTemplates
                .Select(template => template.Category)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase));
            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedItem = categories.FirstOrDefault(category => string.Equals(
                category,
                selectedCategory,
                StringComparison.OrdinalIgnoreCase)) ?? "全部";
        }

        private void ApplyFilter(string? preferredTemplateId = null)
        {
            preferredTemplateId ??= (TemplateListView.SelectedItem as IProjectTemplate)?.Id;
            string selectedCategory = CategoryComboBox.SelectedItem as string ?? "全部";
            string searchText = SearchBar.Text?.Trim() ?? "";

            _filteredTemplates = _allTemplates.Where(template =>
            {
                if (selectedCategory != "全部"
                    && !string.Equals(template.Category, selectedCategory, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.IsNullOrEmpty(searchText) &&
                    !template.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) &&
                    !template.Description.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
                    return false;
                return true;
            }).ToList();

            TemplateListView.ItemsSource = _filteredTemplates;
            TemplateListView.SelectedItem = preferredTemplateId == null
                ? _filteredTemplates.FirstOrDefault()
                : _filteredTemplates.FirstOrDefault(template => string.Equals(
                    template.Id,
                    preferredTemplateId,
                    StringComparison.OrdinalIgnoreCase)) ?? _filteredTemplates.FirstOrDefault();
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
            if (sender is ListView listView
                && ItemsControl.ContainerFromElement(listView, e.OriginalSource as DependencyObject) is ListViewItem)
            {
                TryCreate();
            }
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
