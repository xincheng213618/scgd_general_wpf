using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.Flow
{
    public partial class TemplateSelectionDialog : Window
    {
        public TemplateModel<FlowParam>? SelectedTemplate { get; private set; }

        public TemplateSelectionDialog(ObservableCollection<TemplateModel<FlowParam>> templates)
        {
            InitializeComponent();
            TemplateList.ItemsSource = templates;
            if (templates.Count > 0)
                TemplateList.SelectedIndex = 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateList.SelectedItem is TemplateModel<FlowParam> selected)
            {
                SelectedTemplate = selected;
                DialogResult = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TemplateList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TemplateList.SelectedItem is TemplateModel<FlowParam> selected)
            {
                SelectedTemplate = selected;
                DialogResult = true;
            }
        }
    }
}
