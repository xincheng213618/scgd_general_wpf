using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public partial class TemplateSampleSaveWindow : Window
    {
        public string GroupName => GroupComboBox.Text?.Trim() ?? string.Empty;

        public string SampleName => NameTextBox.Text?.Trim() ?? string.Empty;

        public string Description => DescriptionTextBox.Text?.Trim() ?? string.Empty;

        public TemplateSampleSaveWindow(IEnumerable<string> groups, string defaultName, int selectedCount)
        {
            InitializeComponent();

            GroupComboBox.ItemsSource = groups?.Distinct().OrderBy(it => it).ToList() ?? new List<string>();
            GroupComboBox.Text = TemplateSampleLibrary.DefaultGroupName;
            NameTextBox.Text = defaultName;
            InfoText.Text = selectedCount > 1 ? $"将保存 {selectedCount} 个模板样例，可选择已有组或输入新组" : "将当前模板保存为可复用样例，可选择已有组或输入新组";

            if (selectedCount > 1)
            {
                NameTextBox.IsEnabled = false;
                NameTextBox.Text = "批量保存时使用原模板名";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GroupName))
            {
                MessageBox.Show(this, "请输入组别", "ColorVision");
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}