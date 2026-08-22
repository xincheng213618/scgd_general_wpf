using ColorVision.Common.MVVM;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;

namespace Spectrum.Configs
{
    /// <summary>
    /// A single calibration group containing file paths for wavelength and maguide calibration files.
    /// </summary>
    public class CalibrationGroup : ViewModelBase
    {
        public string GroupName { get => _GroupName; set { _GroupName = value; OnPropertyChanged(); } }
        private string _GroupName = "Default";

        public string WavelengthFile { get => _WavelengthFile; set { _WavelengthFile = value; OnPropertyChanged(); } }
        private string _WavelengthFile = "WavaLength.dat";

        public string MaguideFile { get => _MaguideFile; set { _MaguideFile = value; OnPropertyChanged(); } }
        private string _MaguideFile = "Magiude.dat";

        /// <summary>
        /// Filter wheel position (0-4) associated with this calibration group.
        /// -1 means no association.
        /// </summary>
        public int FilterWheelPosition { get => _FilterWheelPosition; set { _FilterWheelPosition = value; OnPropertyChanged(); } }
        private int _FilterWheelPosition = -1;
    }

    /// <summary>
    /// Per-device calibration config stored in Documents/Spectrometer/{SN}/.
    /// Manages groups of calibration files, supporting ND-linked auto-switching.
    /// </summary>
    public class CalibrationGroupConfig : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CalibrationGroupConfig));

        public ObservableCollection<CalibrationGroup> Groups { get; set; } = new ObservableCollection<CalibrationGroup>();

        public string ActiveGroupName { get => _ActiveGroupName; set { _ActiveGroupName = value; OnPropertyChanged(); } }
        private string _ActiveGroupName = "Default";

        [JsonIgnore]
        public CalibrationGroup? ActiveGroup => Groups.FirstOrDefault(g => g.GroupName == ActiveGroupName);

        public CalibrationGroupConfig Clone()
        {
            CalibrationGroupConfig clone = new() { ActiveGroupName = ActiveGroupName };
            foreach (CalibrationGroup group in Groups)
            {
                clone.Groups.Add(new CalibrationGroup
                {
                    GroupName = group.GroupName,
                    WavelengthFile = group.WavelengthFile,
                    MaguideFile = group.MaguideFile,
                    FilterWheelPosition = group.FilterWheelPosition
                });
            }
            return clone;
        }

        /// <summary>
        /// Gets the config directory for a specific spectrometer SN.
        /// Path: {MyDocuments}/Spectrometer/{SN}/
        /// </summary>
        public static string GetConfigDirectory(string sn)
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Spectrometer",
                SanitizeFolderName(sn));
            return basePath;
        }

        /// <summary>
        /// Gets the config file path for a specific spectrometer SN.
        /// </summary>
        public static string GetConfigFilePath(string sn)
        {
            return Path.Combine(GetConfigDirectory(sn), "CalibrationGroups.json");
        }

        /// <summary>
        /// Loads calibration group config for a given SN. Returns default if not found.
        /// </summary>
        public static CalibrationGroupConfig Load(string sn)
        {
            try
            {
                string filePath = GetConfigFilePath(sn);
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var config = JsonConvert.DeserializeObject<CalibrationGroupConfig>(json);
                    if (config != null && config.Groups.Count > 0)
                    {
                        log.Info($"Loaded calibration config for SN={sn}, {config.Groups.Count} groups");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Failed to load calibration config for SN={sn}", ex);
            }

            // Return default config
            var defaultConfig = new CalibrationGroupConfig();
            defaultConfig.Groups.Add(new CalibrationGroup { GroupName = "Default" });
            return defaultConfig;
        }

        /// <summary>
        /// Saves calibration group config for a given SN.
        /// </summary>
        public bool TrySave(string sn, out string errorMessage)
        {
            string? temporaryPath = null;
            try
            {
                string dir = GetConfigDirectory(sn);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string filePath = GetConfigFilePath(sn);
                temporaryPath = Path.Combine(dir, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (StreamWriter writer = new(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(filePath))
                    File.Replace(temporaryPath, filePath, destinationBackupFileName: null);
                else
                    File.Move(temporaryPath, filePath);
                temporaryPath = null;
                log.Info($"Saved calibration config for SN={sn}");
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to save calibration config for SN={sn}", ex);
                errorMessage = ex.GetBaseException().Message;
                return false;
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) { log.Warn($"Failed to remove temporary calibration config: {temporaryPath}", ex); }
                }
            }
        }

        /// <summary>
        /// Tries to find a group matching an ND position name (e.g., "ND0", "ND10", "ND100", "Empty").
        /// Returns the group if found, null otherwise.
        /// </summary>
        public CalibrationGroup? FindGroupForNDPosition(string ndPositionName)
        {
            if (string.IsNullOrEmpty(ndPositionName)) return null;
            return Groups.FirstOrDefault(g =>
                string.Equals(g.GroupName, ndPositionName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds a calibration group associated with a specific filter wheel position (0-4).
        /// Returns the group if found, null otherwise.
        /// </summary>
        public CalibrationGroup? FindGroupForFilterWheelPosition(int position)
        {
            return Groups.FirstOrDefault(g => g.FilterWheelPosition == position);
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        /// <summary>
        /// Reloads config from disk, discarding any unsaved in-memory changes.
        /// </summary>
        public void Reload(string sn)
        {
            if (string.IsNullOrEmpty(sn)) return;
            try
            {
                var reloaded = Load(sn);
                Groups.Clear();
                foreach (var g in reloaded.Groups)
                    Groups.Add(g);
                ActiveGroupName = reloaded.ActiveGroupName;
                log.Info($"Reloaded calibration config for SN={sn}");
            }
            catch (Exception ex)
            {
                log.Error($"Failed to reload calibration config for SN={sn}", ex);
                // Restore default if reload failed and groups were cleared
                if (Groups.Count == 0)
                {
                    Groups.Add(new CalibrationGroup { GroupName = "Default" });
                    ActiveGroupName = "Default";
                }
            }
        }
    }
}
