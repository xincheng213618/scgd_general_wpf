using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.IO;
using System.Windows;

namespace ProjectARVRPro
{
    public class RecipeManager
    {
        private static RecipeManager _instance;
        private static readonly object _locker = new();
        public static RecipeManager GetInstance() { lock (_locker) { _instance ??= new RecipeManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string RecipeFixPath { get; set; } = DirectoryPath + "ARVRRecipe.json";
        public Dictionary<string, RecipeConfig> RecipeConfigs { get; set; }
        public RelayCommand EditCommand { get; set; }

        public RecipeConfig RecipeConfig { get; set; } = new RecipeConfig();


        public RecipeManager()
        {
            EditCommand = new RelayCommand(a => Edit());

            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (LoadFromFile(RecipeFixPath) is Dictionary<string, RecipeConfig> fix)
            {
                RecipeConfigs = fix;
            }
            else
            {
                RecipeConfigs = new Dictionary<string, RecipeConfig>();
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

                string json = JsonConvert.SerializeObject(RecipeConfigs, Formatting.Indented);
                File.WriteAllText(RecipeFixPath, json);
            }
            catch
            {
                // Optionally log or rethrow
            }
        }

        public static Dictionary<string, RecipeConfig>? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<Dictionary<string, RecipeConfig>>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
