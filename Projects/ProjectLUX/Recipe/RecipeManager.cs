using ColorVision.Common.MVVM;
using log4net;
using Newtonsoft.Json;
using System.IO;
using System.Windows;

namespace ProjectLUX
{
    public class RecipeManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RecipeManager));

        private static RecipeManager _instance;
        private static readonly object _locker = new();
        public static RecipeManager GetInstance() { lock (_locker) { _instance ??= new RecipeManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string RecipeFixPath { get; set; } = DirectoryPath + "ARVRRecipe.json";

        public RelayCommand EditCommand { get; set; }

        public RecipeConfig RecipeConfig { get; set; }


        public RecipeManager()
        {
            EditCommand = new RelayCommand(a => Edit());

            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (LoadFromFile(RecipeFixPath) is RecipeConfig fix)
            {
                RecipeConfig = fix;
                var lists = typeof(RecipeConfig).Assembly.GetTypes().Where(t => typeof(IRecipeConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).ToList();
                if (RecipeConfig.Configs.Count != lists.Count)
                {
                    RecipeConfig.Configs.Clear();
                    lists.ForEach(t => {
                        if (Activator.CreateInstance(t) is IRecipeConfig instance)
                        {
                            RecipeConfig.Configs[t] = instance;
                        }
                    });
                }
            }
            else
            {
                RecipeConfig = new RecipeConfig();
                typeof(RecipeConfig).Assembly.GetTypes().Where(t => typeof(IRecipeConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).ToList().ForEach(t => {
                    if (Activator.CreateInstance(t) is IRecipeConfig instance)
                    {
                        RecipeConfig.Configs[t] = instance;
                    }
                });
                Save();
            }
        }
        public static void Edit()
        {
            EditRecipeWindow EditRecipeWindow = new EditRecipeWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            EditRecipeWindow.ShowDialog();
        }
        public void Save()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);

                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    Formatting = Formatting.Indented
                };
                string json = JsonConvert.SerializeObject(RecipeConfig, settings);
                File.WriteAllText(RecipeFixPath, json);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        public static RecipeConfig? LoadFromFile(string filePath)
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
                return JsonConvert.DeserializeObject<RecipeConfig>(json, settings);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return null;
            }
        }
    }
}
