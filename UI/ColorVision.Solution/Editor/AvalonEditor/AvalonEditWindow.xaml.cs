using ColorVision.Themes;
using ColorVision.Solution.Workspace;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution.Editor.AvalonEditor
{
    /// <summary>
    /// Standalone host for the same editor surface used by the Solution workspace.
    /// </summary>
    public partial class AvalonEditWindow : Window
    {
        public AvalonEditWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            Closing += AvalonEditWindow_Closing;
            Closed += (_, _) => EditorControl.Dispose();
        }

        public AvalonEditWindow(string currentFileName)
            : this()
        {
            if (EditorControl.OpenFile(currentFileName))
                Title = $"{Path.GetFileName(currentFileName)} - 编辑";
        }

        public void SetJsonText(string text)
        {
            EditorControl.SetJsonText(text);
        }

        public string GetJsonText()
        {
            return EditorControl.GetJsonText();
        }

        private void AvalonEditWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!EditorControl.CanSave || !EditorControl.IsDirty)
                return;

            MessageBoxResult result = MessageBox.Show(
                "文件有未保存的更改，是否在关闭前保存？",
                "保存更改",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes && !EditorDocumentService.TrySaveDocument(EditorControl))
                e.Cancel = true;
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = EditorControl.CanSave && EditorControl.IsDirty;
            e.Handled = true;
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            EditorDocumentService.TrySaveDocument(EditorControl);
            e.Handled = true;
        }

        private void Reload_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = EditorControl.CanSave;
            e.Handled = true;
        }

        private void Reload_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!EditorControl.IsDirty || MessageBox.Show(
                    "重新加载将放弃当前未保存的更改，是否继续？",
                    "重新加载",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                EditorControl.ReloadFromDisk();
            }
            e.Handled = true;
        }
    }
}
