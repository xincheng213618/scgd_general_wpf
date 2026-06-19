#pragma warning disable CA1822
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Flow;
using Newtonsoft.Json;
using ProjectKB.Auth;
using System.IO;
using System.Windows;

namespace ProjectKB
{
    public class RecipeManager
    {
        private static RecipeManager? _instance;
        private static readonly object _locker = new();
        public static RecipeManager GetInstance() { lock (_locker) { _instance ??= new RecipeManager(); return _instance; } }

        public static string DirectoryPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Config");
        public static string RecipeDirectoryPath { get; set; } = Path.Combine(DirectoryPath, "ProjectKB", "Recipes");
        public static string RecipeFixPath { get; set; } = Path.Combine(RecipeDirectoryPath, "ProjectKBRecipe.json");
        public static string RecipeDefaultPath { get; set; } = Path.Combine(RecipeDirectoryPath, "ProjectKBRecipeDefault.json");
        public static string LegacyRecipeFixPath { get; set; } = Path.Combine(DirectoryPath, "ProjectKBRecipe.json");

        public Dictionary<string, KBRecipeConfig> RecipeConfigs { get; set; } = new();

        public KBRecipeConfig RecipeConfig { get; set; } = new();

        public KBRecipeConfig DefaultRecipeConfig { get; set; } = new();

        public string CurrentTemplateName { get; private set; } = string.Empty;

        public RelayCommand EditCommand { get; set; }

        public RecipeManager()
        {
            EditCommand = new RelayCommand(a => Edit());
            EnsureRecipeDirectory();

            RecipeConfigs = LoadFromFile(RecipeFixPath) ?? LoadFromFile(LegacyRecipeFixPath) ?? new Dictionary<string, KBRecipeConfig>();
            DefaultRecipeConfig = LoadConfig(RecipeDefaultPath) ?? new KBRecipeConfig();
            SyncTemplateRecipes();
            Save();
        }

        public static void Edit()
        {
            if (!KBAuthManager.GetInstance().RequireAdmin(Application.Current.GetActiveWindow())) return;

            EditRecipeWindow editRecipeWindow = new() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            editRecipeWindow.ShowDialog();
        }

        public void SyncTemplateRecipes(bool removeDeletedRecipes = false)
        {
            HashSet<string> templateNames = GetTemplateNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool removedRecipe = false;

            if (removeDeletedRecipes)
            {
                foreach (string recipeName in RecipeConfigs.Keys.Where(name => !templateNames.Contains(name)).ToList())
                {
                    RecipeConfigs.Remove(recipeName);
                    removedRecipe = true;
                }

                if (removedRecipe && !string.IsNullOrWhiteSpace(CurrentTemplateName) && !templateNames.Contains(CurrentTemplateName))
                {
                    CurrentTemplateName = string.Empty;
                    RecipeConfig = new KBRecipeConfig();
                }
            }

            foreach (string templateName in templateNames)
            {
                if (!RecipeConfigs.ContainsKey(templateName))
                {
                    RecipeConfigs[templateName] = CreateRecipeFromDefault();
                }
            }

            if (!string.IsNullOrWhiteSpace(CurrentTemplateName))
            {
                SetCurrentTemplate(CurrentTemplateName);
            }

            if (removedRecipe)
            {
                Save();
            }
        }

        public IReadOnlyList<RecipeEditorItem> GetRecipeEditorItems()
        {
            SyncTemplateRecipes(removeDeletedRecipes: true);

            HashSet<string> templateNames = GetTemplateNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<string> names = templateNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderByDescending(name => string.Equals(name, CurrentTemplateName, StringComparison.OrdinalIgnoreCase))
                .ThenBy(name => name)
                .ToList();

            return names.Select(name => new RecipeEditorItem(
                    name,
                    EnsureRecipe(name),
                    string.Equals(name, CurrentTemplateName, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public KBRecipeConfig SetCurrentTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return RecipeConfig;
            }

            CurrentTemplateName = templateName;
            RecipeConfig = EnsureRecipe(templateName);
            return RecipeConfig;
        }

        public KBRecipeConfig EnsureRecipe(string templateName)
        {
            if (!RecipeConfigs.TryGetValue(templateName, out KBRecipeConfig? config))
            {
                config = CreateRecipeFromDefault();
                RecipeConfigs[templateName] = config;
            }
            return config;
        }

        public KBRecipeConfig CreateRecipeFromDefault()
        {
            return DefaultRecipeConfig.Clone();
        }

        public void CopyRecipe(KBRecipeConfig source, KBRecipeConfig target)
        {
            target.CopyFrom(source);
        }

        public void ApplyDefaultTo(KBRecipeConfig target)
        {
            target.CopyFrom(DefaultRecipeConfig);
        }

        public void SetDefaultFrom(KBRecipeConfig source)
        {
            DefaultRecipeConfig.CopyFrom(source);
        }

        public static bool HasAnyLimit(KBRecipeConfig? config)
        {
            if (config == null) return false;

            return config.EnableKeyLvLimit
                || config.EnableAvgLvLimit
                || config.EnableUniformityLimit
                || config.EnableKeyLcLimit
                || config.EnableBacklightAutotune;
        }

        public void Save()
        {
            try
            {
                EnsureRecipeDirectory();

                string json = JsonConvert.SerializeObject(RecipeConfigs, Formatting.Indented);
                File.WriteAllText(RecipeFixPath, json);

                string defaultJson = JsonConvert.SerializeObject(DefaultRecipeConfig, Formatting.Indented);
                File.WriteAllText(RecipeDefaultPath, defaultJson);
            }
            catch
            {
            }
        }

        public static Dictionary<string, KBRecipeConfig>? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<Dictionary<string, KBRecipeConfig>>(json);
            }
            catch
            {
                return null;
            }
        }

        private static KBRecipeConfig? LoadConfig(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<KBRecipeConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> GetTemplateNames()
        {
            return TemplateFlow.Params
                .Select(item => item.Key)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void EnsureRecipeDirectory()
        {
            if (!Directory.Exists(RecipeDirectoryPath))
                Directory.CreateDirectory(RecipeDirectoryPath);
        }
    }
}
