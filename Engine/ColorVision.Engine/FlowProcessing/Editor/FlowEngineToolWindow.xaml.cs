using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;

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
                if (_discardFailedOpen) return;
                if (View.IsExecutionActive)
                {
                    e.Cancel = true;
                    string message = EngineLocalization.Get(View.IsExecutionStopping
                        ? "正在停止，等待当前环节结束…"
                        : "Flow_StandaloneCloseWhileRunning");
                    View.STNodeEditorMain.ShowAlert(message, Color.White, Color.FromArgb(230, 95, 62, 16),
                        3500, AlertLocation.RightBottom, bRedraw: true);
                    return;
                }
                if (!View.ConfirmStandaloneDocumentReplacement()) e.Cancel = true;
            };

            Closed += async (_, _) =>
            {
                try
                {
                    if (_discardFailedOpen) View.Dispose();
                    else await View.DisposeWhenExecutionSettlesAsync();
                }
                catch (Exception error)
                {
                    Log.Error("Could not dispose the closed standalone flow window.", error);
                }
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
                if (owner != null && new WindowInteropHelper(owner).Handle != IntPtr.Zero) window.Owner = owner;
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
