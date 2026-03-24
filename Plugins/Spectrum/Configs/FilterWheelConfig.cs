using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using Spectrum.PropertyEditor;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Spectrum.Configs
{
    /// <summary>
    /// Mapping of a single filter wheel hole position to a name (e.g., ND0, ND10, etc.)
    /// and an optional calibration group name for auto-switching.
    /// </summary>
    public class FilterWheelHoleMap : ViewModelBase
    {
        public int HoleIndex { get => _HoleIndex; set { _HoleIndex = value; OnPropertyChanged(); } }
        private int _HoleIndex;

        public string HoleName { get => _HoleName; set { _HoleName = value; OnPropertyChanged(); } }
        private string _HoleName = string.Empty;

        /// <summary>
        /// The calibration group name to auto-switch to when this position is selected.
        /// Empty means no auto-switch.
        /// </summary>
        public string CalibrationGroupName { get => _CalibrationGroupName; set { _CalibrationGroupName = value; OnPropertyChanged(); } }
        private string _CalibrationGroupName = string.Empty;

        public FilterWheelHoleMap Clone() => new() { HoleIndex = HoleIndex, HoleName = HoleName, CalibrationGroupName = CalibrationGroupName };
    }

    /// <summary>
    /// Configuration for the filter wheel serial port and position mapping.
    /// Baud rate defaults to 9600. Positions 0-4 map to ND0, ND10, ND100, ND1000, Empty.
    /// </summary>
    public class FilterWheelConfig : ViewModelBase
    {
        public FilterWheelConfig()
        {
            if (HoleMapping == null)
            {
                HoleMapping = new ObservableCollection<FilterWheelHoleMap>
                {
                    new FilterWheelHoleMap { HoleIndex = 0, HoleName = "ND0" },
                    new FilterWheelHoleMap { HoleIndex = 1, HoleName = "ND10" },
                    new FilterWheelHoleMap { HoleIndex = 2, HoleName = "ND100" },
                    new FilterWheelHoleMap { HoleIndex = 3, HoleName = "ND1000" },
                    new FilterWheelHoleMap { HoleIndex = 4, HoleName = "Empty" },
                };
            }
        }

        [DisplayName("Serial"), PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string SzComName { get => _SzComName; set { _SzComName = value; OnPropertyChanged(); } }
        private string _SzComName = "COM1";

        [DisplayName("BaudRate"), PropertyEditorType(typeof(TextBaudRatePropertiesEditor))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<FilterWheelHoleMap> HoleMapping
        {
            get => _HoleMapping;
            set { _HoleMapping = value; OnPropertyChanged(); }
        }
        private ObservableCollection<FilterWheelHoleMap> _HoleMapping = null!;

        /// <summary>
        /// Gets the hole name for a given position index.
        /// </summary>
        public string? GetHoleName(int position)
        {
            var hole = HoleMapping?.FirstOrDefault(h => h.HoleIndex == position);
            return hole?.HoleName;
        }

        /// <summary>
        /// Gets the calibration group name mapped to a given position index.
        /// </summary>
        public string? GetCalibrationGroupName(int position)
        {
            var hole = HoleMapping?.FirstOrDefault(h => h.HoleIndex == position);
            return hole?.CalibrationGroupName;
        }
    }
}
