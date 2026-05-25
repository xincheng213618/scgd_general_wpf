namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private void InitializeRibbonControls()
        {
            RefreshFlowTemplates();
            RefreshCameraDevices();
            EnsureCaptureTimedButtonOperations();
            InitializePreprocessControls();
            InitializeAnalysisRibbonControls();
            RefreshActiveViewControlState(ActiveView);
        }

        private void RefreshRibbonState(ConoscopeView? activeView)
        {
            RefreshActiveViewControlState(activeView);
            RefreshAnalysisRibbonState(activeView);
        }
    }
}