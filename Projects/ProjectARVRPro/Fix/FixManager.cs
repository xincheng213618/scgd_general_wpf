using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using System.IO;
using System.Windows;

namespace ProjectARVRPro
{
    public class FixManager
    {
        private static FixManager _instance;
        private static readonly object _locker = new();
        public static FixManager GetInstance() { lock (_locker) { _instance ??= new FixManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string FixFilePath { get; set; } = DirectoryPath + "ProjectARVRProFixConfig.json";
        public RelayCommand EditCommand { get; set; }

        public FixConfig FixConfig { get; set; }

        public FixManager()
        {
            EditCommand = new RelayCommand(a => Edit());

            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (LoadFromFile(FixFilePath) is FixConfig fix)
            {
                FixConfig = fix;
            }
            else
            {
                FixConfig = new FixConfig();
                Save();
            }

        }
        public static void Edit()
        {
            EditFixWindow EditFixWindow = new EditFixWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            EditFixWindow.ShowDialog();
        }
        public void Save()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);

                string json = JsonConvert.SerializeObject(FixConfig, Formatting.Indented);
                File.WriteAllText(FixFilePath, json);
            }
            catch
            {
                // Optionally log or rethrow
            }
        }

        public static FixConfig? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<FixConfig>(json);
            }
            catch
            {
                return null;
            }
        }

    }
}
