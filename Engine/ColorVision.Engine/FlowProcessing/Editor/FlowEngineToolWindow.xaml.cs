using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using System;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    [FileExtension(".stn|.cvflow")]
    public sealed class FlowFileOpenProcessor : IFileOpenActionProcessor
    {
        public int Order => 1;

        public FileOpenRouteResult OpenFile(string filePath)
        {
            try
            {
                FlowEngineToolWindow.ShowFile(filePath);
                return new FileOpenRouteResult(true, true);
            }
            catch (Exception error)
            {
                return new FileOpenRouteResult(true, false, error.Message);
            }
        }
    }

    /// <summary>
    /// Standalone host for the same ViewFlow used by the main workspace.
    /// </summary>
    public partial class FlowEngineToolWindow : Window
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FlowEngineToolWindow));
        private bool _discardFailedOpen;

        public ViewFlow View { get; }

        public FlowEngineToolWindow()
        {
            InitializeComponent();
            this.ApplyCaption();

            View = new ViewFlow(FlowEngineManager.GetInstance(), true);
            ViewHost.Content = View;

            Closing += (_, e) =>
            {
                if (!_discardFailedOpen && !View.ConfirmStandaloneDocumentReplacement())
                    e.Cancel = true;
            };

            Closed += (_, _) =>
            {
                View.Dispose();
            };
        }

        public FlowEngineToolWindow(FlowParam? flowParam) : this()
        {
            if (flowParam != null) View.OpenStandaloneFlowParam(flowParam, true);
        }

        internal static void ShowFile(string filePath, Window? owner = null)
        {
            var window = new FlowEngineToolWindow();
            try
            {
                window.View.LoadStandaloneFile(filePath);
                if (owner != null) window.Owner = owner;
                window.Show();
            }
            catch (Exception error)
            {
                Log.Error("Open standalone flow file failed.", error);
                window._discardFailedOpen = true;
                try { window.Close(); }
                catch (Exception closeError) { Log.Warn("Could not close the failed standalone flow window.", closeError); }
                throw;
            }
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
