using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro
{


    /// <summary>
    /// EditFixWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditFixWindow : Window
    {
        FixManager FixManager { get; set; }
        public EditFixWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            FixManager = FixManager.GetInstance();
            foreach (var item in FixManager.FixConfig.Configs)
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
            FixManager.Save();
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {

            foreach (var item in FixManager.FixConfig.Configs)
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

            FixManager.Save();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            FixManager.Save();
            this.Close();
        }
    }
}
