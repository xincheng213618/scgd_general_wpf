using ColorVision.Common.MVVM;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Globalization;

namespace ProjectARVRPro.Process
{
    /// <summary>
    /// Per-process camera values applied only to the transient runtime graph.
    /// </summary>
    public sealed class FlowCameraParameterOverrideConfig : ViewModelBase
    {
        internal const float DefaultExposureTimeMs = 100;

        [Browsable(false)]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;

                _isEnabled = value;
                OnPropertyChanged();
            }
        }
        private bool _isEnabled;

        [DisplayName("曝光(ms)")]
        public float ExposureTimeMs
        {
            get => _exposureTimeMs;
            set
            {
                if (_exposureTimeMs.Equals(value))
                    return;

                _exposureTimeMs = value;
                OnPropertyChanged();
            }
        }
        private float _exposureTimeMs = DefaultExposureTimeMs;

        [DisplayName("校正模板")]
        [PropertyEditorType(typeof(FlowCalibrationTemplateEditor))]
        public string CalibrationTemplateName { get => _calibrationTemplateName; set => SetString(ref _calibrationTemplateName, value); }
        private string _calibrationTemplateName = string.Empty;

        [Browsable(false), JsonIgnore]
        public string DeviceCode { get; private set; } = string.Empty;

        [Browsable(false), JsonIgnore]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The shared template editor reads this value from the edited instance.")]
        public string NodeType => "Camera";

        internal void SetEditorDeviceCode(string? deviceCode)
        {
            DeviceCode = deviceCode ?? string.Empty;
        }

        internal string? GetRuntimeSignature()
        {
            if (!IsEnabled)
                return null;

            return string.Join(
                "|",
                ExposureTimeMs.ToString("R", CultureInfo.InvariantCulture),
                CalibrationTemplateName);
        }

        public FlowCameraParameterOverrideConfig Clone()
        {
            return new FlowCameraParameterOverrideConfig
            {
                IsEnabled = IsEnabled,
                ExposureTimeMs = ExposureTimeMs,
                CalibrationTemplateName = CalibrationTemplateName
            };
        }

        private void SetString(ref string storage, string? value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            string normalized = value ?? string.Empty;
            if (string.Equals(storage, normalized, StringComparison.Ordinal))
                return;

            storage = normalized;
            OnPropertyChanged(propertyName);
        }
    }
}
