#pragma warning disable CA1859
using log4net;
using Newtonsoft.Json;
using System.IO;

namespace ProjectARVRPro
{
    public class RecipeManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RecipeManager));

        private static RecipeManager _instance;
        private static readonly object _locker = new();
        public static RecipeManager GetInstance() { lock (_locker) { _instance ??= new RecipeManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string RecipeFixPath { get; set; } = DirectoryPath + "ARVRRecipe.json";

        public RecipeConfig RecipeConfig { get; set; }


        public RecipeManager()
        {
            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            RecipeConfig = LoadFromFile(RecipeFixPath) ?? new RecipeConfig();
            bool changed = EnsureConfigInstances(RecipeConfig);

            if (changed || !File.Exists(RecipeFixPath))
            {
                Save();
            }
        }

        private static bool EnsureConfigInstances(RecipeConfig recipeConfig)
        {
            bool changed = false;
            foreach (var type in GetConfigTypes())
            {
                if (!recipeConfig.Configs.TryGetValue(type, out var service) || service == null)
                {
                    if (Activator.CreateInstance(type) is IRecipeConfig instance)
                    {
                        recipeConfig.Configs[type] = instance;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private static IReadOnlyList<Type> GetConfigTypes()
        {
            return typeof(RecipeConfig).Assembly.GetTypes()
                .Where(t => typeof(IRecipeConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();
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
