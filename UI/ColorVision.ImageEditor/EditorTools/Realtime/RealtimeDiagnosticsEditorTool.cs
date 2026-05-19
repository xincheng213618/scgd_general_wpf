using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Realtime;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Realtime
{
    public sealed class RealtimeDiagnosticsEditorTool : IEditorTool
    {
        private readonly RealtimeEditorContext _editorContext;
        private RealtimeDiagnosticsWindow? _window;

        public RealtimeDiagnosticsEditorTool(RealtimeEditorContext editorContext)
        {
            _editorContext = editorContext;
            Command = new RelayCommand(_ => ShowDiagnostics());
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;

        public string? GuidId => nameof(RealtimeDiagnosticsEditorTool);

        public int Order => 8;

        public object Icon { get; } = IEditorToolFactory.TryFindResource("DrawingImageProperty");

        public ICommand? Command { get; }

        private void ShowDiagnostics()
        {
            if (_window != null && _window.IsVisible)
            {
                _window.Activate();
                return;
            }

            _window = new RealtimeDiagnosticsWindow(_editorContext.Realtime)
            {
                Owner = _editorContext.GetOwnerWindow(),
            };
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        }
    }
}
