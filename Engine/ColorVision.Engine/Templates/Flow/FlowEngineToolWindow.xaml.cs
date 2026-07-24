using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    [FileExtension(".stn", ".cvflow")]
    public class FileProcessorFlow : IFileOpenActionProcessor
    {
        public int Order => 1;

        public FileOpenRouteResult OpenFile(string filePath)
        {
            FlowEngineToolWindow window = new();
            window.OpenFlow(filePath);
            window.Show();
            return new FileOpenRouteResult(true, true);
        }
    }

    /// <summary>
    /// Standalone host for the same ViewFlow used by the main workspace.
    /// </summary>
    public partial class FlowEngineToolWindow : Window
    {
        public ViewFlow View { get; }

        public FlowEngineToolWindow()
        {
            InitializeComponent();
            this.ApplyCaption();

            View = new ViewFlow(FlowEngineManager.GetInstance(), true);
            ViewHost.Content = View;

            Closed += (_, _) =>
            {
                if (FlowEngineConfig.Instance.IsAutoEditSave &&
                    View.HasStandaloneChanges() &&
                    MessageBox.Show(Properties.Resources.SaveChangesPrompt, "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    View.Save();
                }

                View.STNodeEditorHelper?.HidePropertyEditor();
                View.Dispose();
            };
        }

        public FlowEngineToolWindow(FlowParam flowParam) : this()
        {
            View.OpenStandaloneFlowParam(flowParam, true);
        }

        public void OpenFlow(string filePath)
        {
            View.OpenStandaloneFile(filePath);
        }

        public void OpenFlowBase64(FlowParam flowParam)
        {
            View.OpenStandaloneFlowParam(flowParam, false);
        }
    }
}
