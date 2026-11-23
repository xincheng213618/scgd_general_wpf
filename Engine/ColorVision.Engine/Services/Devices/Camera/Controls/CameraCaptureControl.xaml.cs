using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Camera.Controls
{
    // Input and presentation only. The owning camera card coordinates operations.
    public partial class CameraCaptureControl : UserControl
    {
        public CameraCaptureControl() => InitializeComponent();

        internal void SetFocusOperationState(bool operating, bool busy, Button? activeFocus, bool canStop, bool stopping)
        {
            IsEnabled = !operating || activeFocus != null;
            ExposurePanel.IsEnabled = !operating;
            AcquisitionParameters.IsEnabled = !operating;
            CaptureActionsPanel.IsEnabled = !operating;
            MotorPanel.IsEnabled = !busy || activeFocus != null;
            MotorMovementPanel.IsEnabled = !busy;
            EditFocusButton.IsEnabled = !busy;
            ComboxAutoFocus.IsEnabled = !busy;
            AutoFocusButton.IsEnabled = !busy || ReferenceEquals(activeFocus, AutoFocusButton) && canStop;
            FineFocusButton.IsEnabled = !busy || ReferenceEquals(activeFocus, FineFocusButton) && canStop;
            if (activeFocus == null) return;
            activeFocus.Content = EngineLocalization.Get(stopping ? "CameraPanel_AutoFocusStopping" : "CameraPanel_StopAutoFocus");
            activeFocus.ToolTip = EngineLocalization.Get("CameraPanel_StopAutoFocusHint");
        }

        internal void ResetFocusButton(Button button)
        {
            bool fine = ReferenceEquals(button, FineFocusButton);
            button.Content = fine ? EngineLocalization.Get("CameraPanel_FineFocus") : Properties.Resources.AutoFocus;
            button.ToolTip = fine ? EngineLocalization.Get("CameraPanel_FineFocusHint") : null;
        }
    }
}
