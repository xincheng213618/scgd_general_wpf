using ColorVision.Common.MVVM;
using ColorVision.Common;
using log4net;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using System.IO;
using System.Windows;

namespace ProjectARVRPro
{
    public class FixManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FixManager));

        private static FixManager _instance;
        private static readonly object _locker = new();
        public static FixManager GetInstance() { lock (_locker) { _instance ??= new FixManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = ColorVisionPaths.ConfigDirectory;

        public static string FixFilePath { get; set; } = Path.Combine(DirectoryPath, "ProjectARVRProFixConfig.json");
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
                var lists = typeof(RecipeConfig).Assembly.GetTypes().Where(t => typeof(IFixConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).ToList();
                if (FixConfig.Configs.Count != lists.Count)
                {
                    FixConfig.Configs.Clear();
                    lists.ForEach(t => {
                        if (Activator.CreateInstance(t) is IFixConfig instance)
                        {
                            FixConfig.Configs[t] = instance;
                        }
                    });
                }
            }
            else
            {
                FixConfig = new FixConfig();
                typeof(FixConfig).Assembly.GetTypes().Where(t => typeof(IFixConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).ToList() .ForEach(t => {
                    if (Activator.CreateInstance(t) is IFixConfig instance)
                    {
                        FixConfig.Configs[t] = instance;
                    }
                });
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
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                };
                string json = JsonConvert.SerializeObject(FixConfig, settings);
                File.WriteAllText(FixFilePath, json);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        public static FixConfig? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;

                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                };
                return JsonConvert.DeserializeObject<FixConfig>(json, settings);
            }
            catch(Exception ex)
            {
                log.Error(ex);
                return null;
            }
        }

    }
}
