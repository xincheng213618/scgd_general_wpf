using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectKB
{
    /// <summary>
    /// EditRecipeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditRecipeWindow : Window
    {
        private readonly ObservableCollection<RecipeEditorItem> _recipeRows = new();
        private RecipeManager _recipeManager = null!;
        private RecipeEditorItem? _selectedRecipeRow;
        private KBRecipeConfig? _observedRecipeConfig;

        public EditRecipeWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _recipeManager = RecipeManager.GetInstance();
            _recipeManager.SyncTemplateRecipes(removeDeletedRecipes: true);

            RecipeDataGrid.ItemsSource = _recipeRows;
            CopySourceComboBox.ItemsSource = _recipeRows;

            string currentTemplateName = GetCurrentTemplateName();
            if (!string.IsNullOrWhiteSpace(currentTemplateName))
            {
                _recipeManager.SetCurrentTemplate(currentTemplateName);
            }

            RefreshRecipeRows();
            SelectInitialRecipe();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _recipeManager.Save();
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRecipeRow == null) return;

            _recipeManager.ApplyDefaultTo(_selectedRecipeRow.Config);
            RefreshSelectedStatus();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _recipeManager.Save();
            this.Close();
        }

        private void RecipeDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecipeDataGrid.SelectedItem is RecipeEditorItem row)
            {
                SelectRecipe(row);
            }
        }

        private void CopyFrom_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRecipeRow == null || CopySourceComboBox.SelectedItem is not RecipeEditorItem sourceRow) return;

            _recipeManager.CopyRecipe(sourceRow.Config, _selectedRecipeRow.Config);
            RefreshSelectedStatus();
        }

        private void ApplyDefault_Click(object sender, RoutedEventArgs e)
        {
            Reset_Click(sender, e);
        }

        private void SetDefault_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRecipeRow == null) return;

            _recipeManager.SetDefaultFrom(_selectedRecipeRow.Config);
            RefreshSelectedStatus();
        }

        private void EditDefault_Click(object sender, RoutedEventArgs e)
        {
            PropertyEditorWindow propertyEditorWindow = new(_recipeManager.DefaultRecipeConfig, false)
            {
                Title = "Recipe初始值",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            propertyEditorWindow.ShowDialog();
            _recipeManager.Save();
        }

        private void RefreshTemplates_Click(object sender, RoutedEventArgs e)
        {
            RefreshRecipeRows();
        }

        private void OpenRecipeFolder_Click(object sender, RoutedEventArgs e)
        {
            PlatformHelper.OpenFolder(RecipeManager.RecipeDirectoryPath);
        }

        private void RefreshRecipeRows()
        {
            string selectedName = _selectedRecipeRow?.TemplateName ?? GetCurrentTemplateName();
            _recipeRows.Clear();

            foreach (RecipeEditorItem item in _recipeManager.GetRecipeEditorItems())
            {
                _recipeRows.Add(item);
            }

            RecipeDataGrid.Items.Refresh();
            CopySourceComboBox.Items.Refresh();

            RecipeEditorItem? selected = _recipeRows.FirstOrDefault(item => string.Equals(item.TemplateName, selectedName, StringComparison.OrdinalIgnoreCase))
                ?? _recipeRows.FirstOrDefault();
            RecipeDataGrid.SelectedItem = selected;
        }

        private void SelectInitialRecipe()
        {
            string currentTemplateName = GetCurrentTemplateName();
            RecipeEditorItem? current = _recipeRows.FirstOrDefault(item => string.Equals(item.TemplateName, currentTemplateName, StringComparison.OrdinalIgnoreCase));
            RecipeDataGrid.SelectedItem = current ?? _recipeRows.FirstOrDefault();
        }

        private string GetCurrentTemplateName()
        {
            int selectedIndex = ProjectKBConfig.Instance.TemplateSelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < TemplateFlow.Params.Count)
                return TemplateFlow.Params[selectedIndex].Key;

            if (!string.IsNullOrWhiteSpace(_recipeManager.CurrentTemplateName))
                return _recipeManager.CurrentTemplateName;

            return string.Empty;
        }

        private void SelectRecipe(RecipeEditorItem row)
        {
            if (_observedRecipeConfig != null)
            {
                _observedRecipeConfig.PropertyChanged -= RecipeConfig_PropertyChanged;
            }

            _selectedRecipeRow = row;
            _observedRecipeConfig = row.Config;
            _observedRecipeConfig.PropertyChanged += RecipeConfig_PropertyChanged;

            EditStackPanel.Children.Clear();
            EditStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(row.Config));

            foreach (RecipeEditorItem item in _recipeRows)
            {
                item.IsCurrentTemplate = string.Equals(item.TemplateName, GetCurrentTemplateName(), StringComparison.OrdinalIgnoreCase);
            }

            CopySourceComboBox.SelectedItem = _recipeRows.FirstOrDefault(item => item != row && item.HasLimit) ?? _recipeRows.FirstOrDefault(item => item != row);
            RefreshSelectedStatus();
        }

        private void RecipeConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RefreshSelectedStatus();
        }

        private void RefreshSelectedStatus()
        {
            if (_selectedRecipeRow == null) return;

            _selectedRecipeRow.RefreshStatus();
            RecipeDataGrid.Items.Refresh();

            SelectedTemplateTextBlock.Text = _selectedRecipeRow.TemplateName;
            RecipeStatusTextBlock.Text = _selectedRecipeRow.HasLimit ? "当前模板已启用Recipe判定项" : "当前模板未启用任何Recipe判定项，运行时不会做Recipe限制";
            RecipeStatusTextBlock.Foreground = _selectedRecipeRow.HasLimit ? FindResource("GlobalTextBrush") as Brush : Brushes.OrangeRed;
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (ProjectKBConfig.Instance.SNlocked)
                    ProjectKBConfig.Instance.SNlocked = false;
            }
        }
    }
}
