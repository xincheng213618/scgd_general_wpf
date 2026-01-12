using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ProjectARVRLite
{
    /// <summary>
    /// Configuration for a test type, allowing users to enable/disable specific test types
    /// </summary>
    public class TestTypeConfig : ViewModelBase
    {
        public ARVR1TestType TestType { get => _TestType; set { _TestType = value; OnPropertyChanged(); } }
        private ARVR1TestType _TestType;

        [DisplayName("启用")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled = true;

        [DisplayName("名称")]
        [JsonIgnore]
        public string Name => TestType.ToString();

        [DisplayName("描述")]
        [JsonIgnore]
        public string Description => GetDescription(TestType);

        private static string GetDescription(ARVR1TestType testType)
        {
            return testType switch
            {
                ARVR1TestType.None => "无",
                ARVR1TestType.W51 => "白画面绿图W51",
                ARVR1TestType.White => "白画面255",
                ARVR1TestType.W25 => "白画面25",
                ARVR1TestType.Chessboard => "棋盘格",
                ARVR1TestType.MTFHV => "MTF水平垂直",
                ARVR1TestType.Distortion => "畸变",
                ARVR1TestType.Ghost => "鬼影",
                ARVR1TestType.OpticCenter => "光轴偏角",
                ARVR1TestType.DotMatrix => "屏幕定位",
                ARVR1TestType.WscreeenDefectDetection => "白画面瑕疵检测",
                ARVR1TestType.BKscreeenDefectDetection => "黑画面瑕疵检测",
                _ => testType.ToString()
            };
        }
    }

    /// <summary>
    /// Manager for test type configurations
    /// </summary>
    public class TestTypeConfigManager : ViewModelBase
    {
        private static TestTypeConfigManager _instance;
        private static readonly object _locker = new();
        public static TestTypeConfigManager GetInstance() { lock (_locker) { _instance ??= new TestTypeConfigManager(); return _instance; } }

        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        public static string ConfigFilePath { get; set; } = DirectoryPath + "ProjectARVRLiteTestTypeConfig.json";

        public ObservableCollection<TestTypeConfig> TestTypeConfigs { get; } = new ObservableCollection<TestTypeConfig>();

        public RelayCommand EditCommand { get; set; }

        private TestTypeConfigManager()
        {
            EditCommand = new RelayCommand(a => Edit());
            LoadConfig();
        }

        private void LoadConfig()
        {
            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (LoadFromFile(ConfigFilePath) is List<TestTypeConfig> configs)
            {
                TestTypeConfigs.Clear();
                foreach (var config in configs)
                {
                    TestTypeConfigs.Add(config);
                }
            }
            else
            {
                InitializeDefaultConfigs();
                Save();
            }
        }

        private void InitializeDefaultConfigs()
        {
            // Initialize with all test types except None
            foreach (ARVR1TestType testType in Enum.GetValues(typeof(ARVR1TestType)))
            {
                if (testType != ARVR1TestType.None)
                {
                    TestTypeConfigs.Add(new TestTypeConfig { TestType = testType, IsEnabled = true });
                }
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);

                var configList = TestTypeConfigs.ToList();
                string json = JsonConvert.SerializeObject(configList, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch
            {
                // Optionally log or rethrow
            }
        }

        public static List<TestTypeConfig>? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<List<TestTypeConfig>>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void Edit()
        {
            EditTestTypeConfigWindow window = new EditTestTypeConfigWindow() 
            { 
                Owner = System.Windows.Application.Current.GetActiveWindow(), 
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner 
            };
            window.ShowDialog();
        }

        /// <summary>
        /// Gets the next enabled test type after the current one
        /// </summary>
        /// <param name="currentType">Current test type</param>
        /// <returns>Next enabled test type, or None if no more enabled types</returns>
        public ARVR1TestType GetNextEnabledTestType(ARVR1TestType currentType)
        {
            int currentIndex = -1;
            
            // Find current index
            for (int i = 0; i < TestTypeConfigs.Count; i++)
            {
                if (TestTypeConfigs[i].TestType == currentType)
                {
                    currentIndex = i;
                    break;
                }
            }

            // Look for next enabled type
            for (int i = currentIndex + 1; i < TestTypeConfigs.Count; i++)
            {
                if (TestTypeConfigs[i].IsEnabled)
                {
                    return TestTypeConfigs[i].TestType;
                }
            }

            return ARVR1TestType.None;
        }

        /// <summary>
        /// Gets the first enabled test type
        /// </summary>
        /// <returns>First enabled test type, or None if none are enabled</returns>
        public ARVR1TestType GetFirstEnabledTestType()
        {
            foreach (var config in TestTypeConfigs)
            {
                if (config.IsEnabled)
                {
                    return config.TestType;
                }
            }
            return ARVR1TestType.None;
        }

        /// <summary>
        /// Checks if there are any enabled test types after the current one
        /// </summary>
        /// <param name="currentType">Current test type</param>
        /// <returns>True if there are more enabled types, false otherwise</returns>
        public bool HasMoreEnabledTestTypes(ARVR1TestType currentType)
        {
            return GetNextEnabledTestType(currentType) != ARVR1TestType.None;
        }
    }
}
