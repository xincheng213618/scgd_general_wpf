using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ProjectLUX
{

    /// <summary>
    /// EditRecipeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditRecipeWindow : Window
    {
        RecipeManager RecipeManager { get; set; }
        public EditRecipeWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            RecipeManager = RecipeManager.GetInstance();

            foreach (var item in RecipeManager.RecipeConfig.Configs)
            {
                StackPanel stackPanel = PropertyEditorHelper.GenPropertyEditorControl(item.Value);
                TreeViewItem treeViewItem = new TreeViewItem() { Header = item.Key.Name, Tag = stackPanel };
                treeView.Items.Add(treeViewItem);

                EditStackPanel.Children.Add(stackPanel);
            }
        }
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is TreeView treeView && treeView.SelectedItem is TreeViewItem treeViewItem && treeViewItem.Tag is StackPanel obj)
            {
                obj.BringIntoView();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            RecipeManager.Save();
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RecipeManager.RecipeConfig.Configs)
            {
                object source = Activator.CreateInstance(item.Key);

                var properties = item.Key.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite);
                foreach (var property in properties)
                {
                    var propertyValue = property.GetValue(source);
                    property.SetValue(item.Value, propertyValue);
                }
            }
            RecipeManager.Save();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RecipeManager.Save();
            this.Close();
        }
    }
}
