using ColorVision.Engine.Templates;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam
{
    public partial class CameraRunParamEditor : UserControl, ITemplateUserControl
    {
        private CameraRunParam? _param;

        public CameraRunParamEditor()
        {
            InitializeComponent();
            PropertyGrid1.PropertyValueChanged += (_, _) => UpdateUnifiedExposureText();
        }

        public void SetParam(object param)
        {
            if (param is CameraRunParam cameraRunParam)
            {
                SetParam(cameraRunParam);
            }
        }

        public void SetParam(CameraRunParam param)
        {
            _param = param;
            PropertyGrid1.SelectedObject = param;
            UpdateUnifiedExposureText();
        }

        private void ApplyUnifiedExposure_Click(object sender, RoutedEventArgs e)
        {
            if (!TryApplyUnifiedExposure())
            {
                UnifiedExposureTextBox.Focus();
                UnifiedExposureTextBox.SelectAll();
            }
        }

        private void UnifiedExposureTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && TryApplyUnifiedExposure())
            {
                e.Handled = true;
            }
        }

        private bool TryApplyUnifiedExposure()
        {
            if (_param == null || !TryParseExposure(UnifiedExposureTextBox.Text, out float exposure))
            {
                return false;
            }

            _param.SetAllExposure(exposure);
            PropertyGrid1.Refresh();
            UpdateUnifiedExposureText();
            UnifiedExposureTextBox.SelectAll();
            return true;
        }

        private void UpdateUnifiedExposureText()
        {
            if (_param == null)
            {
                UnifiedExposureTextBox.Clear();
                return;
            }

            bool isUnified = _param.ExpTime == _param.ExpTimeR
                && _param.ExpTime == _param.ExpTimeG
                && _param.ExpTime == _param.ExpTimeB;
            UnifiedExposureTextBox.Text = isUnified
                ? _param.ExpTime.ToString(CultureInfo.CurrentCulture)
                : string.Empty;
        }

        private static bool TryParseExposure(string text, out float exposure)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out exposure)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out exposure);
        }
    }
}
