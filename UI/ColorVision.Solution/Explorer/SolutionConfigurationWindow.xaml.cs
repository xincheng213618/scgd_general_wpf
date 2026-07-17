using System.Windows;

namespace ColorVision.Solution.Explorer
{
    public partial class SolutionConfigurationWindow : Window
    {
        private readonly SolutionExplorer _solutionExplorer;
        private readonly SolutionConfigurationEditorModel _model;

        public SolutionConfigurationWindow(SolutionExplorer solutionExplorer)
        {
            InitializeComponent();
            _solutionExplorer = solutionExplorer;
            _model = new SolutionConfigurationEditorModel(
                solutionExplorer.DirectoryInfo.FullName,
                solutionExplorer.LoadProjectsForConfigurationEditing(),
                solutionExplorer.Config.ActiveConfiguration,
                solutionExplorer.Config.StartupProject,
                solutionExplorer.Config.ProjectConfigurations);
            _model.ModelChanged += (_, _) => UpdateSaveState();
            DataContext = _model;
            UpdateSaveState();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _model.Validate();
            if (_model.HasErrors)
            {
                UpdateSaveState();
                MessageBox.Show(
                    this,
                    "请先修复验证结果中的错误。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!_solutionExplorer.TryApplyConfigurationChanges(
                _model.CreateChanges(),
                out string errorMessage))
            {
                MessageBox.Show(
                    this,
                    errorMessage,
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void UpdateSaveState()
        {
            SaveButton.IsEnabled = !_model.HasErrors;
        }
    }
}
