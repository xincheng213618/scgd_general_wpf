using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Copilot
{
    public partial class CopilotModelEditorWindow : Window
    {
        private readonly CopilotSettingsViewModel _settings;
        private readonly CopilotProfileConfig? _original;
        public CopilotModelEditorWindow(CopilotSettingsViewModel settings, CopilotProfileConfig? model)
        {
            InitializeComponent();
            this.ApplyCaption();
            _settings = settings;
            _original = model;
            DataContext = new CopilotModelEditor(settings.CreateModelDraft(model), settings.AvailableModelPresets);
        }
        private CopilotModelEditor Editor => (CopilotModelEditor)DataContext;
        private void Reset_Click(object sender, RoutedEventArgs e) => Editor.Reset();
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!Editor.Validate()) return;
            if (!_settings.CommitModelDraft(Editor.Draft, _original, out var error)) { Editor.Error = error; return; }
            DialogResult = true;
        }
    }
}
