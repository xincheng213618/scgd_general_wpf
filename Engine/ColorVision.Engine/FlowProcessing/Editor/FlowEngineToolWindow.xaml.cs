using ColorVision.Common.MVVM;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    [FileExtension(".stn", ".cvflow")]
    public sealed class FlowFileOpenProcessor : IFileOpenActionProcessor
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

            Closing += (_, e) =>
            {
                if (!View.ConfirmStandaloneDocumentReplacement())
                    e.Cancel = true;
            };

            Closed += (_, _) =>
            {
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
