using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using Spectrum.Configs;
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

        public float SetWL1 { get => _SetWL1; set { _SetWL1 = value; OnPropertyChanged(); } }
        private float _SetWL1 = 380;
        public float SetWL2 { get => _SetWL2; set { _SetWL2 = value; OnPropertyChanged(); } }
        private float _SetWL2 = 780;

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
                log.Info($"Group switch: CM_Emission_LoadWavaLengthFile {WavelengthFile}, ret={r1}");
                int r2 = Spectrometer.CM_Emission_LoadMagiudeFile(Handle, MaguideFile);
                log.Info($"Group switch: CM_Emission_LoadMagiudeFile {MaguideFile}, ret={r2}");
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
                log.Info($"ND position changed to '{ndPositionName}', auto-switching to calibration group '{group.GroupName}'");
                ActiveCalibrationGroupName = group.GroupName;
            }
            else
            {
                log.Info($"ND position changed to '{ndPositionName}', no matching calibration group found");
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
                log.Info($"FilterWheel position changed to {position}, auto-switching to calibration group '{group.GroupName}'");
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
                log.Info($"FilterWheel position changed to {position}, no matching calibration group found");
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
        public RelayCommand ConnectCommand { get; set; }

        [JsonIgnore]
        public RelayCommand DisconnectCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditIntTimeConfigCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditGetDataConfigCommand { get; set; }

        [JsonIgnore]
        public RelayCommand EditAutodarkParamCommand { get; set; }


        public SpectrometerManager()
        {
            SetCSFileCommand = new RelayCommand(a => SetFile(path => CSFile = path));
            GetIntTimeCommand = new RelayCommand(a => GetIntTime());
            GetDarkDataCommand = new RelayCommand(a => GetDarkData());
            GetLightDataCommand = new RelayCommand(a => GetLightData());
            GenerateAmplitudeCommand = new RelayCommand(a => GenerateAmplitude());

            ConnectCommand = new RelayCommand(a => Connect());
            DisconnectCommand = new RelayCommand(a => Disconnect());
            MaguideFile = "Magiude.dat";

            LoadWavelengthFileCommand = new RelayCommand(a => LoadWavelengthFile());
            SetWavelengthFileCommand = new RelayCommand(a => SetFile(path => WavelengthFile = path));

            SetMaguideFileCommand = new RelayCommand(a => SetMaguideFile());
            LoadMaguideFileCommand = new RelayCommand(a => LoadMaguideFile());
            SetMaguideOutputFileCommand = new RelayCommand(a => SetMaguideOutputFile());
            EditIntTimeConfigCommand = new RelayCommand(a => EditIntTimeConfig());

            EditGetDataConfigCommand = new RelayCommand(a => EditGetDataConfig());
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
                log.Info("NDConnnet failed");
                IsNDConnected = false;
            }
            else
            {
                log.Info("NDConnnet");
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
                log.Info("Cannot remove the last calibration group");
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
                // 使用强类型反序列化（内部基��反射），直接将 JSON 映射到对象
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
                saveFileDialog.FileName = "Magiude.dat"; // 默认文件名
                saveFileDialog.Filter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*";
                saveFileDialog.Title = "选择保存文件路径";

                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MaguideFileOutput = saveFileDialog.FileName;

                }
            }
        }

        public void EditIntTimeConfig()
        {
            new PropertyEditorWindow(IntTimeConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public IntTimeConfig IntTimeConfig { get => _IntTimeConfig; set { _IntTimeConfig = value; OnPropertyChanged(); } }
        private IntTimeConfig _IntTimeConfig = new IntTimeConfig();

        public void EditGetDataConfig()
        {
            new PropertyEditorWindow(GetDataConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public GetDataConfig GetDataConfig { get => _GetDataConfig; set { _GetDataConfig = value; OnPropertyChanged(); } }
        private GetDataConfig _GetDataConfig = new GetDataConfig();

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
            log.Info("Callback: " + text);
            return 0;
        }

        public void Connect()
        {
            Handle = Spectrometer.CM_CreateEmission((int)Config.SpectrometerType, MyCallback);
            int ncom = 0;
            if (Config.IsComPort)
            {
                 ncom = int.Parse(Config.SzComName.Replace("COM", ""));

            }
            int iR = Spectrometer.CM_Emission_Init(Handle, ncom, Config.BaudRate);
            if (iR == 1)
            {
                MessageBox.Show("连接成功");
            }
            else
            {
                MessageBox.Show("连接失败");
            }
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

        public void GenerateAmplitude()  
        {
            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fLightData);
            if (ret == 1)
            {
            }
            else
            {
                MessageBox.Show("获取LightData失败");
                return;
            }
            log.Info($"IntTime{IntTime}CSFile: {CSFile},WavelengthFile{WavelengthFile},MaguideFileOutput{MaguideFileOutput}");
            int ret1 = Spectrometer.CM_Emission_CreateMagiude(IntTime, fDarkData, fLightData, CSFile, WavelengthFile, MaguideFileOutput);
            if (ret1 ==1)
            {
                MessageBox.Show("生成成功");
            }
            else
            {
                MessageBox.Show("生成失败");
            }
        }

        public void GetLightData()
        {
            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fLightData);
            if (ret == 1)
            {
                MessageBox.Show("获取成功");
            }
            else
            {
                MessageBox.Show("获取失败");
            }
        }

        public void GetDarkData()
        {
            int ret = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fDarkData);
            if (ret == 1)
            {
                MessageBox.Show("校零成功");
            }
            else
            {
                MessageBox.Show("校零失败");
            }
        }

        public int MyAutoTimeCallback(int time,double spectum)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                log.Info($"当前自动积分时间: {time},spectum:{spectum}");
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
                MessageBox.Show("获取成功");
            }
            else
            {
                MessageBox.Show("自动积分获取失败");
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

        public string MaguideFileOutput { get => _MaguideFile; set { _MaguideFile = value; OnPropertyChanged(); } }
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
            log.Info($"CM_Emission_LoadMagiudeFile 配置幅值文{MaguideFile}:{ret}");
            if (ret == 1)
            {
                MessageBox.Show("配置幅值文件成功");
            }
            else
            {
                MessageBox.Show("配置幅值文件失败");
            }
        }
        private void LoadWavelengthFile()
        {
            int ret = Spectrometer.CM_Emission_LoadWavaLengthFile(Handle, WavelengthFile);
            log.Info($"CM_Emission_LoadWavaLengthFile 配置波长文{WavelengthFile}:{ret}");
            if (ret == 1)
            {
                MessageBox.Show("配置波长文件成功");
            }
            else
            {
                MessageBox.Show("配置波长文件失败");
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
