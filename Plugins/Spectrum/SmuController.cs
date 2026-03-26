using ColorVision.Common.MVVM;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using Spectrum.Configs;
using System.Text;

namespace Spectrum
{
    /// <summary>
    /// Local SMU (source meter) controller wrapping PassSx C++ calls.
    /// Used in the Spectrum plugin to directly read V/I from a connected source meter.
    /// </summary>
    public class SmuController : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SmuController));

        public SmuConfig Config => SmuConfig.Instance;

        [JsonIgnore]
        public bool IsOpen { get => _DevID >= 0; }

        private int _DevID = -1;

        [JsonIgnore]
        public string Version { get => _Version; private set { _Version = value; OnPropertyChanged(); } }
        private string _Version = string.Empty;

        [JsonIgnore]
        public double? V { get => _V; set { _V = value; OnPropertyChanged(); } }
        private double? _V;

        [JsonIgnore]
        public double? I { get => _I; set { _I = value; OnPropertyChanged(); } }
        private double? _I;

        /// <summary>
        /// Opens the source meter connection.
        /// </summary>
        public bool Open()
        {
            if (IsOpen) return true;
            if (string.IsNullOrWhiteSpace(Config.DevName)) return false;

            _DevID = PassSx.OpenNetDevice(Config.IsNet, Config.DevName, Config.PssType);
            if (_DevID >= 0)
            {
                int strLen = 1024;
                byte[] pszIdn = new byte[strLen];
                PassSx.cvPssSxGetIDN(_DevID, pszIdn, ref strLen);
                Version = Encoding.Default.GetString(pszIdn, 0, strLen);
                log.Info($"SMU opened: DevID={_DevID}, Version={Version}");

                // Apply settings
                PassSx.cvPssSxSetSourceV(_DevID, Config.IsSourceV);
                PassSx.Set4WireFront(_DevID, Config.Is4Wire, Config.IsFront);
                PassSx.SetSrcAorB(_DevID, Config.IsChannelA);

                OnPropertyChanged(nameof(IsOpen));
                return true;
            }
            log.Warn($"SMU open failed: DevName={Config.DevName}");
            return false;
        }

        /// <summary>
        /// Closes the source meter connection.
        /// </summary>
        public void Close()
        {
            if (IsOpen)
            {
                PassSx.CvPssSxCloseOutput(_DevID);
                PassSx.CloseDevice(_DevID);
                log.Info($"SMU closed: DevID={_DevID}");
                _DevID = -1;
                V = null;
                I = null;
                Version = string.Empty;
                OnPropertyChanged(nameof(IsOpen));
            }
        }

        /// <summary>
        /// Applies the current config settings (source mode, 4-wire, channel) to the open device.
        /// </summary>
        public void ApplySettings()
        {
            if (!IsOpen) return;
            PassSx.cvPssSxSetSourceV(_DevID, Config.IsSourceV);
            PassSx.Set4WireFront(_DevID, Config.Is4Wire, Config.IsFront);
            PassSx.SetSrcAorB(_DevID, Config.IsChannelA);
        }

        /// <summary>
        /// Measures V and I from the source meter using the configured parameters.
        /// Returns true if successful.
        /// </summary>
        public bool MeasureData()
        {
            if (!IsOpen) return false;
            double rstV = 0, rstI = 0;
            bool ok = PassSx.cvMeasureData(_DevID, Config.MeasureVal, Config.LimitVal, ref rstV, ref rstI);
            if (ok)
            {
                V = rstV;
                I = rstI * 1000; // convert A to mA
                log.Info($"SMU MeasureData: V={V}, I={I} mA");
            }
            else
            {
                log.Warn("SMU MeasureData failed");
            }
            return ok;
        }

        /// <summary>
        /// Gets the last measured V and I as float values suitable for EQE calculation.
        /// </summary>
        public (float voltage, float currentMA) GetVI()
        {
            return ((float)(V ?? 0), (float)(I ?? 0));
        }

        /// <summary>
        /// Closes the source output (turns off the source but keeps connection open).
        /// </summary>
        public void CloseOutput()
        {
            if (IsOpen)
            {
                PassSx.CvPssSxCloseOutput(_DevID);
                log.Info("SMU output closed");
            }
        }
    }
}
