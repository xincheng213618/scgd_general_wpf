using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.PreProcess
{
    public partial class PreProcessTypeWindow : Window
    {
        public PreProcessTypeOption? SelectedProcessType => ProcessTypesList.SelectedItem as PreProcessTypeOption;

        public PreProcessTypeWindow(PreProcessManager manager)
        {
            InitializeComponent();
            DataContext = manager;
            this.ApplyCaption();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProcessType != null) DialogResult = true;
        }
    }
}
