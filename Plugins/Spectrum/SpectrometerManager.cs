using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using Spectrum.Calibration;
using Spectrum.Configs;
using Spectrum.Data;
using Spectrum.License;
using Spectrum.Models;
using Spectrum.PropertyEditor;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Spectrum
{
    [DisplayName("EmissionSP100设置")]
    public class SetEmissionSP100Config : ViewModelBase, IConfig
    {
        public static SetEmissionSP100Config Instance => ConfigService.Instance.GetRequiredService<SetEmissionSP100Config>();

        public event EventHandler EditChanged;

        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        public SetEmissionSP100Config()
        {
            EditCommand = new RelayCommand(a =>
            {
                new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                EditChanged?.Invoke(this, new EventArgs());
            } );
        }


        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        public int nStartPos { get => _nStartPos; set { _nStartPos = value; OnPropertyChanged(); } }
        private int _nStartPos = 1691;

        public int nEndPos { get => _nEndPos; set { _nEndPos = value; OnPropertyChanged(); } }
        private int _nEndPos = 2048;

        public double dMeanThreshold { get => _dMeanThreshold; set { _dMeanThreshold = value; OnPropertyChanged(); } }
        private double _dMeanThreshold = 80;
    }
    [DisplayName("自动积分时间配置")]
    public class IntTimeConfig : ViewModelBase, IConfig
    {
        [DisplayName("积分时间上限Ms")]
        public int IntLimitTime { get => _IntLimitTime; set { _IntLimitTime = value; OnPropertyChanged(); } }
        private int _IntLimitTime = 6000;

        [DisplayName("自动积分时间起始")]
        public float AutoIntTimeB { get => _AutoIntTimeB; set { _AutoIntTimeB = value; OnPropertyChanged(); } }
        private float _AutoIntTimeB = 1;

        [DisplayName("自动积分阈值(%)")]
        public double MaxPercent { get => _MaxPercent; set { _MaxPercent = value; OnPropertyChanged(); Max = (int)(_MaxPercent * 655.35); } } // 655.35 = 65535 / 100, converts percentage to 16-bit ADC scale
        private double _MaxPercent = 76.3;

        [Browsable(false)]
        public int Max { get => _Max; set { _Max = value; OnPropertyChanged(); } }
        private int _Max = 50000;

        [DisplayName("旧版本模式")]
        public bool IsOldVersion { get => _IsOldVersion; set { _IsOldVersion = value; OnPropertyChanged(); } }
        private bool _IsOldVersion = false;
    }

    public class GetDataConfig : ViewModelBase, IConfig
    {
        [DisplayName("是否开启同步频率")]
        public bool IsSyncFrequencyEnabled { get => _IsSyncFrequencyEnabled; set { _IsSyncFrequencyEnabled = value; OnPropertyChanged(); } }
        private bool _IsSyncFrequencyEnabled;

        [DisplayName("同步频率")]
        public double Syncfreq { get => _Syncfreq; set { _Syncfreq = value; OnPropertyChanged(); } }
        private double _Syncfreq = 1000;

        [DisplayName("同步频率系数")]
        public int SyncfreqFactor { get => _SyncfreqFactor; set { _SyncfreqFactor = value; OnPropertyChanged(); } }
        private int _SyncfreqFactor = 10;

        [DisplayName("滤波宽度")]
        public int FilterBW { get => _FilterBW; set { _FilterBW = value; OnPropertyChanged(); } }
        private int _FilterBW = 5;

        [DisplayName("起始波长")]
        public float SetWL1 { get => _SetWL1; set { _SetWL1 = value; OnPropertyChanged(); } }
        private float _SetWL1 = 380;

        [DisplayName("结束波长")]
        public float SetWL2 { get => _SetWL2; set { _SetWL2 = value; OnPropertyChanged(); } }
        private float _SetWL2 = 780;
    }

    [DisplayName("自动积分与数据采集配置")]
    public class MeasurementDataConfig : ViewModelBase
    {

        [DisplayName("自动积分时间配置")]
        public IntTimeConfig IntTimeConfig { get => _IntTimeConfig; set { _IntTimeConfig = value; OnPropertyChanged(); } }
        private IntTimeConfig _IntTimeConfig = new IntTimeConfig();

        [DisplayName("数据采集配置")]
        public GetDataConfig GetDataConfig { get => _GetDataConfig; set { _GetDataConfig = value; OnPropertyChanged(); } }
        private GetDataConfig _GetDataConfig = new GetDataConfig();
    }


    public class AutodarkParam : ViewModelBase,IConfig
    {
        [DisplayName("起始时间(ms)")]
        public float fTimeStart { get => _fTimeStart; set { _fTimeStart = value; OnPropertyChanged(); } }
        private float _fTimeStart = 50f;

        [DisplayName("步进(ms)")]
        public int nStepTime { get => _nStepTime; set { _nStepTime = value; OnPropertyChanged(); } }
        private int _nStepTime = 100;

        [DisplayName("测量次数")]
        public int nStepCount { get => _nStepCount; set { _nStepCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(nEndTime)); } }
        private int _nStepCount = 1;

        [DisplayName("结束时间(ms)")]
        public int nEndTime { get => (int)Math.Round(fTimeStart + _nStepCount * nStepTime); set { RecalculateStepCount(value); ; } }

        private void RecalculateStepCount(int _nEndTime)
        {
            if (nStepTime > 0)
            {
                var count = Math.Round((_nEndTime - fTimeStart) / nStepTime);
                nStepCount = (int)count;
            }
            else
            {
                nStepCount = 0;
            }
        }

        /// <summary>
        /// Action delegate set by MainWindow to execute adaptive auto dark calibration
        /// </summary>
        [JsonIgnore]
        [Browsable(false)]
        public Action ExecuteAdaptiveAutoDark { get; set; }
    }


    public class SpectrometerManager : ViewModelBase,IConfig
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrometerManager));

        public SpectrumConfig Config => ConfigService.Instance.GetRequiredService<SpectrumConfig>();

        public static SpectrometerManager Instance => ConfigService.Instance.GetRequiredService<SpectrometerManager>();

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        [JsonIgnore]
        public ShutterController ShutterController { get; set; } = new ShutterController();

        [JsonIgnore]
        public FilterWheelController FilterWheelController { get; set; } = new FilterWheelController();

        [JsonIgnore]
        public SmuController SmuController { get; set; } = new SmuController();

        public static SetEmissionSP100Config SetEmissionSP100Config => SetEmissionSP100Config.Instance;

        [JsonIgnore]
        public IntPtr Handle { get; set; } = IntPtr.Zero;
        
        [JsonIgnore]
        public bool IsConnected { get => _IsConnected; set { _IsConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectionTypeDisplay)); } }
        private bool _IsConnected = false;

        /// <summary>
        /// The serial number of the currently connected spectrometer.
        /// </summary>
        [JsonIgnore]
        public string SerialNumber { get => _SerialNumber; set { _SerialNumber = value; OnPropertyChanged(); } }
        private string _SerialNumber = string.Empty;

        /// <summary>
        /// Readable connection type string for the status bar.
        /// </summary>
        [JsonIgnore]
        public string ConnectionTypeDisplay
        {
            get
            {
                if (!IsConnected) return "未连接";
                return Config.IsComPort ? $"COM: {Config.SzComName}" : "USB";
            }
        }

        /// <summary>
        /// Calibration group config, loaded per-SN from Documents/Spectrometer/{SN}/
        /// </summary>
        [JsonIgnore]
        public CalibrationGroupConfig CalibrationGroupConfig { get => _CalibrationGroupConfig; set { _CalibrationGroupConfig = value; OnPropertyChanged(); OnPropertyChanged(nameof(CalibrationGroupNames)); } }
        private CalibrationGroupConfig _CalibrationGroupConfig = CreateDefaultCalibrationGroupConfig();

        private static CalibrationGroupConfig CreateDefaultCalibrationGroupConfig()
        {
            var config = new CalibrationGroupConfig();
            config.Groups.Add(new CalibrationGroup { GroupName = "Default" });
            return config;
        }

        /// <summary>
        /// The group names for ComboBox binding.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> CalibrationGroupNames => CalibrationGroupConfig.Groups.Select(g => g.GroupName);

        /// <summary>
        /// The active calibration group name. Changing this triggers file reload when connected.
        /// </summary>
        [JsonIgnore]
        public string ActiveCalibrationGroupName
        {
            get => CalibrationGroupConfig.ActiveGroupName;
            set
            {
                if (CalibrationGroupConfig.ActiveGroupName == value) return;
                CalibrationGroupConfig.ActiveGroupName = value;
                OnPropertyChanged();
                ApplyActiveGroup();
                SaveCalibrationConfig();
            }
        }

        [JsonIgnore]
        public RelayCommand AddCalibrationGroupCommand { get; set; }
        [JsonIgnore]
        public RelayCommand RemoveCalibrationGroupCommand { get; set; }
        [JsonIgnore]
        public RelayCommand SetGroupWavelengthFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand SetGroupMaguideFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenCalibrationGroupWindowCommand { get; set; }
        [JsonIgnore]
        public RelayCommand ApplyActiveGroupCommand { get; set; }

        /// <summary>
        /// Loads calibration config for the current SN.
        /// </summary>
        public void LoadCalibrationConfig()
        {
            if (string.IsNullOrEmpty(SerialNumber)) return;
            CalibrationGroupConfig = CalibrationGroupConfig.Load(SerialNumber);
            OnPropertyChanged(nameof(CalibrationGroupNames));
            OnPropertyChanged(nameof(ActiveCalibrationGroupName));
            ApplyActiveGroup();
        }

        /// <summary>
        /// Saves calibration config for the current SN.
        /// </summary>
        public void SaveCalibrationConfig()
        {
            if (string.IsNullOrEmpty(SerialNumber)) return;
            CalibrationGroupConfig.Save(SerialNumber);
        }

        /// <summary>
        /// Applies the active calibration group: updates WavelengthFile/MaguideFile and reloads if connected.
        /// </summary>
        private void ApplyActiveGroup()
        {
            var group = CalibrationGroupConfig.ActiveGroup;
            if (group == null) return;

            WavelengthFile = group.WavelengthFile;
            MaguideFile = group.MaguideFile;

            if (IsConnected && Handle != IntPtr.Zero)
            {
                int r1 = Spectrometer.CM_Emission_LoadWavaLengthFile(Handle, WavelengthFile);
                if (r1 == 1)
                    log.Info($"校准组切换: 加载波长文件成功 {WavelengthFile}");
                else
                    log.Warn($"校准组切换: 加载波长文件失败 {WavelengthFile}, {Spectrometer.GetErrorMessage(r1)}");
                int r2 = Spectrometer.CM_Emission_LoadMagiudeFile(Handle, MaguideFile);
                if (r2 == 1)
                    log.Info($"校准组切换: 加载幅值文件成功 {MaguideFile}");
                else
                    log.Warn($"校准组切换: 加载幅值文件失败 {MaguideFile}, {Spectrometer.GetErrorMessage(r2)}");
            }
        }

        /// <summary>
        /// Called when the CFW (filter wheel) switches to a position. If a calibration group
        /// matches the ND position name, it auto-switches.
        /// </summary>
        public void OnNDPositionChanged(string ndPositionName)
        {
            var group = CalibrationGroupConfig.FindGroupForNDPosition(ndPositionName);
            if (group != null)
            {
                log.Debug($"ND 位置切换至 '{ndPositionName}'，自动切换校准组 '{group.GroupName}'");
                ActiveCalibrationGroupName = group.GroupName;
            }
            else
            {
                log.Debug($"ND 位置切换至 '{ndPositionName}'，无匹配校准组");
            }
        }

        /// <summary>
        /// Called when the filter wheel controller changes position.
        /// Auto-switches calibration group if a mapping exists (by FilterWheelPosition or by ND name).
        /// </summary>
        private void OnFilterWheelPositionChanged(int position)
        {
            // First try to find a group by FilterWheelPosition
            var group = CalibrationGroupConfig.FindGroupForFilterWheelPosition(position);
            if (group != null)
            {
                log.Debug($"滤光轮位置切换至 {position}，自动切换校准组 '{group.GroupName}'");
                Application.Current.Dispatcher.Invoke(() => ActiveCalibrationGroupName = group.GroupName);
                return;
            }

            // Fallback: try to find by ND name
            string? ndName = FilterWheelConfig.GetHoleName(position);
            if (!string.IsNullOrEmpty(ndName))
            {
                OnNDPositionChanged(ndName);
            }
            else
            {
                log.Debug($"滤光轮位置切换至 {position}，无匹配校准组");
            }
        }

        /// <summary>
        /// Whether the CFW (filter wheel) is connected.
        /// </summary>
        [JsonIgnore]
        public bool IsNDConnected { get => _IsNDConnected; set { _IsNDConnected = value; OnPropertyChanged(); } }
        private bool _IsNDConnected;

        [JsonIgnore]
        public RelayCommand SetWavelengthFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand LoadWavelengthFileCommand { get; set; }

        [JsonIgnore]
        public RelayCommand SetCSFileCommand { get; set; }

        [JsonIgnore]
        public RelayCommand SetMaguideFileCommand { get; set; }
        [JsonIgnore]
        public RelayCommand SetMaguideOutputFileCommand { get; set; }

        [JsonIgnore]
        public RelayCommand LoadMaguideFileCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GetDarkDataCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GetLightDataCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GetIntTimeCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GenerateAmplitudeCommand { get; set; }

        [JsonIgnore]
        public RelayCommand GenerateAmplitudeFromExistingCommand { get; set; }

        [JsonIgnore]
        public RelayCommand ConnectCommand { get; set; }

        [JsonIgnore]
        public RelayCommand DisconnectCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditIntTimeConfigCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditAutodarkParamCommand { get; set; }


        public SpectrometerManager()
        {
            SetCSFileCommand = new RelayCommand(a => SetFile(path => CSFile = path));
            GetIntTimeCommand = new RelayCommand(a => GetIntTime());
            GetDarkDataCommand = new RelayCommand(a => GetDarkData());
            GetLightDataCommand = new RelayCommand(a => GetLightData());
            GenerateAmplitudeCommand = new RelayCommand(a => GenerateAmplitude());
            GenerateAmplitudeFromExistingCommand = new RelayCommand(a => GenerateAmplitudeFromExisting());

            ConnectCommand = new RelayCommand(a => Connect());
            DisconnectCommand = new RelayCommand(a => Disconnect());
            MaguideFile = "Magiude.dat";

            LoadWavelengthFileCommand = new RelayCommand(a => LoadWavelengthFile());
            SetWavelengthFileCommand = new RelayCommand(a => SetFile(path => WavelengthFile = path));

            SetMaguideFileCommand = new RelayCommand(a => SetMaguideFile());
            LoadMaguideFileCommand = new RelayCommand(a => LoadMaguideFile());
            SetMaguideOutputFileCommand = new RelayCommand(a => SetMaguideOutputFile());
            EditIntTimeConfigCommand = new RelayCommand(a => EditMeasurementDataConfig());

            EditAutodarkParamCommand = new RelayCommand(a => EditAutodarkParam());

            GetSpectrSerialNumberCommand = new RelayCommand(a => GetSpectrSerialNumber());

            EditNDConfigCommand = new RelayCommand(a => new PropertyEditorWindow(NDConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            ConnectNDCommand = new RelayCommand(a => ConnectND());

            AddCalibrationGroupCommand = new RelayCommand(a => AddCalibrationGroup());
            RemoveCalibrationGroupCommand = new RelayCommand(a => RemoveCalibrationGroup());
            SetGroupWavelengthFileCommand = new RelayCommand(a => SetActiveGroupFile(isWavelength: true));
            SetGroupMaguideFileCommand = new RelayCommand(a => SetActiveGroupFile(isWavelength: false));
            OpenCalibrationGroupWindowCommand = new RelayCommand(a => OpenCalibrationGroupWindow());
            ApplyActiveGroupCommand = new RelayCommand(a => ApplyActiveGroup());

            EditFilterWheelConfigCommand = new RelayCommand(a => new PropertyEditorWindow(FilterWheelConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());

            EditShutterConfigCommand = new RelayCommand(a => new PropertyEditorWindow(ShutterController.Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());

            // Subscribe to filter wheel position changes for auto-switching calibration groups
            FilterWheelController.PositionChanged += OnFilterWheelPositionChanged;
        }
        public NDConfig NDConfig => Config.NDConfig;
        public FilterWheelConfig FilterWheelConfig => Config.FilterWheelConfig;

        public RelayCommand EditNDConfigCommand { get; set; }
        [JsonIgnore]
        public RelayCommand EditFilterWheelConfigCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditShutterConfigCommand { get; set; }

        public RelayCommand ConnectNDCommand { get; set; }

        public IntPtr NDHandle { get; set; } = IntPtr.Zero;

        public void ConnectND()
        {
            NDHandle = NdCFWPortAPI.CM_CreatNdCFWPort(NDConfig.SzComName, (uint)NDConfig.BaudRate, false);
            if (NDHandle == IntPtr.Zero)
            {
                log.Warn("ND 滤光轮连接失败");
                IsNDConnected = false;
            }
            else
            {
                log.Info("ND 滤光轮连接成功");
                IsNDConnected = true;
            }
        }

        private void AddCalibrationGroup()
        {
            // Generate a unique group name
            int idx = CalibrationGroupConfig.Groups.Count;
            string newName;
            do
            {
                newName = $"Group{idx}";
                idx++;
            } while (CalibrationGroupConfig.Groups.Any(g => g.GroupName == newName));

            CalibrationGroupConfig.Groups.Add(new CalibrationGroup
            {
                GroupName = newName,
                WavelengthFile = WavelengthFile,
                MaguideFile = MaguideFile,
            });
            OnPropertyChanged(nameof(CalibrationGroupNames));
            SaveCalibrationConfig();
        }

        private void RemoveCalibrationGroup()
        {
            if (CalibrationGroupConfig.Groups.Count <= 1)
            {
                log.Debug("不能删除最后一个校准组");
                return;
            }
            var group = CalibrationGroupConfig.ActiveGroup;
            if (group == null) return;
            CalibrationGroupConfig.Groups.Remove(group);
            ActiveCalibrationGroupName = CalibrationGroupConfig.Groups.FirstOrDefault()?.GroupName ?? "Default";
            OnPropertyChanged(nameof(CalibrationGroupNames));
            SaveCalibrationConfig();
        }

        private void SetActiveGroupFile(bool isWavelength)
        {
            var group = CalibrationGroupConfig.ActiveGroup;
            if (group == null) return;

            using var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "DAT files (*.dat)|*.dat|All Files|*.*";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (isWavelength)
                {
                    group.WavelengthFile = dialog.FileName;
                    WavelengthFile = dialog.FileName;
                }
                else
                {
                    group.MaguideFile = dialog.FileName;
                    MaguideFile = dialog.FileName;
                }
                SaveCalibrationConfig();
            }
        }

        private void OpenCalibrationGroupWindow()
        {
            new CalibrationGroupWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            // After dialog closed, refresh bindings
            OnPropertyChanged(nameof(CalibrationGroupNames));
            OnPropertyChanged(nameof(ActiveCalibrationGroupName));
        }


        public RelayCommand GetSpectrSerialNumberCommand { get; set; }
        public void GetSpectrSerialNumber()
        {
            int i = 0;

            if (Config.IsComPort)
            {
                if (int.TryParse(Config.SzComName.Replace("COM", ""), out int z))
                {
                    i = z;
                }
            }
            int bufferLength = 1024;
            StringBuilder stringBuilder = new StringBuilder(bufferLength);
            int ret = Spectrometer.CM_Emission_GetAllSN((int)Config.SpectrometerType, i, stringBuilder, bufferLength);

            string raw = stringBuilder.ToString();
            string display = FormatSerialNumberResult(raw);
            MessageBox1.Show(Application.Current.GetActiveWindow(), display, "Sprectrum");
        }


        public class SpectrometerSnResult
        {
            [JsonProperty("number")]
            public int Number { get; set; }

            [JsonProperty("ID")]
            public List<string> IDs { get; set; }
        }
        /// <summary>
        /// 将CM_Emission_GetAllSN返回的JSON格式化为用户友好的显示文本
        /// </summary>
        internal static string FormatSerialNumberResult(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "未检测到设备 (返回为空)";

            try
            {
                // 使用强类型反序列化（内部基于反射），直接将 JSON 映射到对象
                var result = JsonConvert.DeserializeObject<SpectrometerSnResult>(raw);

                // 如果解析出来的对象为空，或者包含的ID列表为空
                if (result == null || result.IDs == null || result.IDs.Count == 0)
                {
                    return "未检测到设备";
                }

                // 只有1台设备
                if (result.IDs.Count == 1)
                {
                    return $"设备序列号: {result.IDs[0]}";
                }

                // 多台设备
                var formattedList = result.IDs.Select((sn, idx) => $"  {idx + 1}. {sn}");
                return $"检测到 {result.Number} 台设备:\n" + string.Join("\n", formattedList);
            }
            catch (JsonException)
            {
                // 如果 C++ 那边发生了异常或者返回了非标准 JSON（比如报错信息），直接显示原始内容
                return $"解析失败，原始内容: {raw}";
            }
            catch (Exception ex)
            {
                return $"发生未知错误: {ex.Message}\n原始内容: {raw}";
            }
        }


        public void SetMaguideOutputFile()
        {
            using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
            {
                saveFileDialog.FileName = $"Magiude_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
                saveFileDialog.Filter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*";
                saveFileDialog.Title = "选择保存文件路径";

                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MaguideFileOutput = saveFileDialog.FileName;

                }
            }
        }

        public MeasurementDataConfig MeasurementDataConfig { get; set; } = new MeasurementDataConfig();
        public IntTimeConfig IntTimeConfig => MeasurementDataConfig.IntTimeConfig;

        public GetDataConfig GetDataConfig => MeasurementDataConfig.GetDataConfig;

        public void EditMeasurementDataConfig()
        {
            new PropertyEditorWindow(MeasurementDataConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }



        /// <summary>
        /// 连续测试时间
        /// </summary>
        public int MeasurementInterval { get => _MeasurementInterval; set { if (value <= 0) return;  _MeasurementInterval = value;  OnPropertyChanged(); } }
        private int _MeasurementInterval = 30;
        /// <summary>
        /// 连续测试次数
        /// </summary>
        public int MeasurementNum { get => _MeasurementNum; set { if (value <= 0) return; _MeasurementNum = value; OnPropertyChanged(); } }
        private int _MeasurementNum = 30;
        /// <summary>
        /// 当前测试数
        /// </summary>
        public int LoopMeasureNum { get => _LoopMeasureNum; set { _LoopMeasureNum = value; OnPropertyChanged(); } }
        private int _LoopMeasureNum;


        public static int MyCallback(IntPtr strText, int nLen)
        {
            string text = Marshal.PtrToStringAnsi(strText, nLen);
            log.Debug("光谱仪回调: " + text);
            return 0;
        }

        public void Connect()
        {
            // Sync licenses from DB before connecting
            LicenseDatabase.Instance.SyncToLocal();

            Handle = Spectrometer.CM_CreateEmission((int)Config.SpectrometerType, MyCallback);
            int ncom = 0;
            if (Config.IsComPort)
            {
                 ncom = int.Parse(Config.SzComName.Replace("COM", ""));

            }
            int iR = Spectrometer.CM_Emission_Init(Handle, ncom, Config.BaudRate);
            if (iR == 1)
            {
                log.Info("光谱仪连接成功");
                MessageBox.Show("连接成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(iR);
                log.Error($"光谱仪连接失败: {errorMsg}");
                CheckDeviceAndPromptLicense(errorMsg);
            }
        }

        /// <summary>
        /// On connection failure, detect if a device exists.
        /// If exactly one device is found, it's likely a license issue - prompt user.
        /// </summary>
        private void CheckDeviceAndPromptLicense(string errorMsg)
        {
            try
            {
                int comPort = 0;
                if (Config.IsComPort)
                {
                    if (int.TryParse(Config.SzComName.Replace("COM", ""), out int z))
                        comPort = z;
                }

                int bufferLength = 1024;
                StringBuilder sb = new StringBuilder(bufferLength);
                Spectrometer.CM_Emission_GetAllSN((int)Config.SpectrometerType, comPort, sb, bufferLength);
                string raw = sb.ToString();

                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var result = JsonConvert.DeserializeObject<SpectrometerSnResult>(raw);
                    if (result?.IDs != null && result.IDs.Count == 1)
                    {
                        log.Info($"检测到设备 {result.IDs[0]}，连接失败可能是许可证问题");
                        var msgResult = MessageBox.Show(
                            Application.Current.GetActiveWindow(),
                            $"连接失败: {errorMsg}\n\n检测到设备: {result.IDs[0]}\n连接失败可能是许可证问题。\n\n是否打开许可证管理器?",
                            "连接失败 - 许可证检查",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (msgResult == MessageBoxResult.Yes)
                        {
                            new License.LicenseManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug($"设备检测失败: {ex.Message}");
            }

            MessageBox.Show($"连接失败: {errorMsg}");
        }
        public int Disconnect()
        {
            if (Handle != IntPtr.Zero)
            {
                int ret = Spectrometer.CM_Emission_Close(Handle);
                ret = Spectrometer.CM_ReleaseEmission(Handle);
                IsConnected = false;
            }
            return -1;
        }

        /// <summary>
        /// 执行校零操作，自动处理快门控制
        /// 可被定时任务和Socket指令共享调用
        /// </summary>
        /// <returns>校零结果：1=成功，其他=失败</returns>
        public async Task<int> PerformDarkCalibrationAsync()
        {
            if (ShutterController.IsConnected)
            {
                log.Debug("开启快门进行校零");
                await ShutterController.OpenShutter();
            }

            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fDarkData);

            if (ShutterController.IsConnected)
            {
                log.Debug("关闭快门");
                await ShutterController.CloseShutter();
            }

            return ret;
        }

        /// <summary>
        /// 获取自动积分时间，自动处理同步频率调整
        /// 可被定时任务和Socket指令共享调用
        /// </summary>
        /// <returns>成功返回积分时间，失败返回null</returns>
        public float? GetAutoIntegrationTime()
        {
            float fIntTime = 0;
            int ret;

            if (IntTimeConfig.IsOldVersion)
            {
                ret = Spectrometer.CM_Emission_GetAutoTime(
                    Handle, ref fIntTime, IntTimeConfig.IntLimitTime,
                    IntTimeConfig.AutoIntTimeB, (int)IntTimeConfig.MaxPercent);
            }
            else
            {
                ret = Spectrometer.CM_Emission_GetAutoTimeEx(
                    Handle, ref fIntTime, IntTimeConfig.IntLimitTime,
                    IntTimeConfig.AutoIntTimeB, IntTimeConfig.Max, null);
            }

            if (ret != 1)
            {
                log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                return null;
            }

            // Apply sync frequency adjustment if enabled
            if (GetDataConfig.IsSyncFrequencyEnabled)
            {
                float syncIntTime = fIntTime;
                COLOR_PARA cOLOR_PARA = new COLOR_PARA();
                int syncRet = Spectrometer.CM_Emission_GetDataSyncfreq(
                    Handle, 0, GetDataConfig.Syncfreq, GetDataConfig.SyncfreqFactor,
                    ref syncIntTime, Average, GetDataConfig.FilterBW,
                    fDarkData, 0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2,
                    ref cOLOR_PARA);
                if (syncRet == 1)
                {
                    log.Info($"同步频率调整积分时间: {fIntTime}ms → {syncIntTime}ms");
                    fIntTime = syncIntTime;
                }
                else
                {
                    log.Warn($"同步频率调整积分时间失败: {Spectrometer.GetErrorMessage(syncRet)}");
                }
            }

            log.Info($"自动积分时间获取成功: {fIntTime}ms");
            return fIntTime;
        }

        /// <summary>
        /// Event raised when dark data or light data has been acquired, for chart refresh.
        /// </summary>
        public event EventHandler DataAcquired;

        public void GenerateAmplitude()  
        {
            string outputPath = MaguideFileOutput;
            if (string.IsNullOrEmpty(outputPath))
            {
                using var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.FileName = $"Magiude_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
                saveFileDialog.Filter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*";
                saveFileDialog.Title = "选择幅值标定文件保存路径";
                if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                outputPath = saveFileDialog.FileName;
                MaguideFileOutput = outputPath;
            }

            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fLightData);
            if (ret != 1)
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"获取 LightData 失败: {errorMsg}");
                MessageBox.Show($"获取 LightData 失败: {errorMsg}");
                return;
            }
            DataAcquired?.Invoke(this, EventArgs.Empty);

            log.Debug($"生成幅值文件参数: IntTime={IntTime}, CSFile={CSFile}, WavelengthFile={WavelengthFile}, MaguideFileOutput={outputPath}");
            int ret1 = Spectrometer.CM_Emission_CreateMagiude(IntTime, fDarkData, fLightData, CSFile, WavelengthFile, outputPath);
            if (ret1 == 1)
            {
                log.Info($"幅值文件生成成功: {outputPath}");
                MessageBox.Show($"生成成功\n文件: {outputPath}");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret1);
                log.Error($"幅值文件生成失败: {errorMsg}");
                MessageBox.Show($"生成失败: {errorMsg}");
            }
        }

        /// <summary>
        /// Generate amplitude file from existing dark/light data (manual mode - no auto-acquire).
        /// </summary>
        public void GenerateAmplitudeFromExisting()
        {
            string outputPath = MaguideFileOutput;
            if (string.IsNullOrEmpty(outputPath))
            {
                using var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.FileName = $"Magiude_{DateTime.Now:yyyyMMdd_HHmmss}.dat";
                saveFileDialog.Filter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*";
                saveFileDialog.Title = "选择幅值标定文件保存路径";
                if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                outputPath = saveFileDialog.FileName;
                MaguideFileOutput = outputPath;
            }

            log.Debug($"手动生成幅值文件: IntTime={IntTime}, CSFile={CSFile}, WavelengthFile={WavelengthFile}, MaguideFileOutput={outputPath}");
            int ret = Spectrometer.CM_Emission_CreateMagiude(IntTime, fDarkData, fLightData, CSFile, WavelengthFile, outputPath);
            if (ret == 1)
            {
                log.Info($"幅值文件生成成功: {outputPath}");
                MessageBox.Show($"生成成功\n文件: {outputPath}");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"幅值文件生成失败: {errorMsg}");
                MessageBox.Show($"生成失败: {errorMsg}");
            }
        }

        public void GetLightData()
        {
            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fLightData);
            if (ret == 1)
            {
                log.Info("LightData 获取成功");
                DataAcquired?.Invoke(this, EventArgs.Empty);
                MessageBox.Show("获取成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"LightData 获取失败: {errorMsg}");
                MessageBox.Show($"获取失败: {errorMsg}");
            }
        }

        public void GetDarkData()
        {
            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fDarkData);
            if (ret == 1)
            {
                log.Info("校零成功");
                DataAcquired?.Invoke(this, EventArgs.Empty);
                MessageBox.Show("校零成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"校零失败: {errorMsg}");
                MessageBox.Show($"校零失败: {errorMsg}");
            }
        }

        public int MyAutoTimeCallback(int time,double spectum)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                log.Debug($"自动积分时间回调: 积分时间={time}, 光谱强度={spectum}");
            });
            return 0;
        }

        public void GetIntTime()
        {
            float fIntTime = IntTime;
            int ret = Spectrometer.CM_Emission_GetAutoTimeEx(Handle, ref fIntTime, IntLimitTime, AutoIntTimeB, Max,MyAutoTimeCallback);
            if (ret == 1)
            {
                IntTime = fIntTime;
                log.Info($"自动积分时间获取成功: {fIntTime}ms");
                MessageBox.Show("获取成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Warn($"自动积分时间获取失败: {errorMsg}");
                MessageBox.Show($"自动积分获取失败: {errorMsg}");
            }
        }

        public float[] fDarkData = new float[2048];

        public float[] fLightData = new float[2048];


        public int IntLimitTime { get => _IntLimitTime; set { _IntLimitTime = value; OnPropertyChanged(); } }
        private int _IntLimitTime = 6000;

        public float AutoIntTimeB { get => _AutoIntTimeB; set { _AutoIntTimeB = value; OnPropertyChanged(); } }
        private float _AutoIntTimeB = 1;

        public float IntTime { get => _IntTime; set { _IntTime = value; OnPropertyChanged(); } }
        private float _IntTime = 100;

        public int Saturation { get => _Saturation; set { _Saturation = value; OnPropertyChanged(); } }
        private int _Saturation = 99;
        public int Max { get => _Max; set { _Max = value; OnPropertyChanged(); } }
        private int _Max = 50000;

        public double MaxPercent { get => _MaxPercent; set { _MaxPercent = value; OnPropertyChanged(); Max = (int)(_MaxPercent * 655.35); } }
        private double _MaxPercent = 76.3;

        public int Average { get => _Average; set { _Average = value; OnPropertyChanged(); } }
        private int _Average = 1;

        public string WavelengthFile { get => _WavelengthFile; set { _WavelengthFile = value; OnPropertyChanged(); } }
        private string _WavelengthFile = "WavaLength.dat";

        public string CSFile { get => _CSFile; set { _CSFile = value; OnPropertyChanged(); } }
        private string _CSFile;

        public string MaguideFile { get => _MaguideFile; set { _MaguideFile = value; OnPropertyChanged(); } }
        private string _MaguideFile;

        public string MaguideFileOutput { get => _MaguideFileOutput; set { _MaguideFileOutput = value; OnPropertyChanged(); } }
        private string _MaguideFileOutput;



        public AutodarkParam AutodarkParam { get => _AutodarkParam; set { _AutodarkParam = value; OnPropertyChanged(); } }
        private AutodarkParam _AutodarkParam = new AutodarkParam();
        public void EditAutodarkParam()
        {
            var win = new PropertyEditorWindow(AutodarkParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            // Add adaptive auto dark execution button to the property editor window
            if (AutodarkParam.ExecuteAdaptiveAutoDark != null)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Content = "执行自适应校零",
                    Margin = new Thickness(10, 10, 10, 0),
                    Padding = new Thickness(10, 4, 10, 4),
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E67E22")),
                    Foreground = System.Windows.Media.Brushes.White
                };
                btn.Click += (s, e) => AutodarkParam.ExecuteAdaptiveAutoDark?.Invoke();
                if (win.Content is System.Windows.Controls.Panel panel)
                {
                    panel.Children.Add(btn);
                }
                else if (win.Content is System.Windows.UIElement existingContent)
                {
                    var sp = new System.Windows.Controls.StackPanel();
                    sp.Children.Add(existingContent);
                    sp.Children.Add(btn);
                    win.Content = sp;
                }
            }
            win.ShowDialog();
        }

        /// <summary>
        /// 自动校零
        /// </summary>
        public bool EnableAutodark { get => _EnableAutodark; set { _EnableAutodark = value; OnPropertyChanged(); if (value) EnableAdaptiveAutoDark = false;  } }
        private bool _EnableAutodark;

        /// <summary>
        /// 自适应校零
        /// </summary>
        public bool EnableAdaptiveAutoDark { get => _EnableAdaptiveAutoDark; set { _EnableAdaptiveAutoDark = value; OnPropertyChanged();  if (value) EnableAutodark = false;  } }
        private bool _EnableAdaptiveAutoDark;



        /// <summary>
        /// 启动自动积分
        /// </summary>
        public bool EnableAutoIntegration { get => _EnableAutoIntegration; set { _EnableAutoIntegration = value; OnPropertyChanged(); } }
        private bool _EnableAutoIntegration;


        private void LoadMaguideFile()
        {
            int ret = Spectrometer.CM_Emission_LoadMagiudeFile(Handle, MaguideFile);
            if (ret == 1)
            {
                log.Info($"加载幅值文件成功: {MaguideFile}");
                MessageBox.Show("配置幅值文件成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"加载幅值文件失败: {MaguideFile}, {errorMsg}");
                MessageBox.Show($"配置幅值文件失败: {errorMsg}");
            }
        }
        private void LoadWavelengthFile()
        {
            int ret = Spectrometer.CM_Emission_LoadWavaLengthFile(Handle, WavelengthFile);
            if (ret == 1)
            {
                log.Info($"加载波长文件成功: {WavelengthFile}");
                MessageBox.Show("配置波长文件成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"加载波长文件失败: {WavelengthFile}, {errorMsg}");
                MessageBox.Show($"配置波长文件失败: {errorMsg}");
            }
        }


        private void SetMaguideFile()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Filter = "All Files|*.*"; // Optionally set a filter for file types
                dialog.Title = "Save Maguide File";
                dialog.FileName = "Magiude.dat"; // Default file name

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MaguideFile = dialog.FileName;
                }
            }
        }



        private void SetFile(Action<string> setFilePath)
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Filter = "All Files|*.*"; // Optionally set a filter for file types
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    setFilePath(dialog.FileName);
                }
            }
        }

    }
}
