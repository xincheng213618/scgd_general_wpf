using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace ProjectARVRPro
{
    public interface IRecipeConfig
    {

    }

    [DisplayName("ARVR上下限判定")]
    public class RecipeConfig : ViewModelBase, IRecipe
    {
        public RecipeConfig()
        {
            Configs = new Dictionary<Type, IRecipeConfig>();

            typeof(RecipeConfig).Assembly.GetTypes()
                .Where(t => typeof(IRecipeConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList()
                .ForEach(t =>
                {
                    if (Activator.CreateInstance(t) is IRecipeConfig instance)
                    {
                        Configs[t] = instance;
                    }
                });
        }
        public Dictionary<Type, IRecipeConfig> Configs { get; set; }

        public T GetRequiredService<T>() where T : IRecipeConfig
        {
            var type = typeof(T);

            if (Configs.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            if (Activator.CreateInstance(type) is IRecipeConfig defaultConfig)
            {
                Configs[type] = defaultConfig;
            }
            // 此处递归调用是为了确保缓存和异常处理逻辑一致
            return GetRequiredService<T>();
        }
    }


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

    /// <summary>
    /// EditRecipeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditRecipeWindow : Window
    {
        RecipeManager RecipeManager { get; set; }
        public EditRecipeWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            RecipeManager = RecipeManager.GetInstance();

            foreach (var item in RecipeManager.RecipeConfig.Configs)
            {
                EditStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(item.Value));
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            RecipeManager.Save();
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RecipeManager.RecipeConfig.Configs)
            {
                object source = Activator.CreateInstance(item.Key);

                var properties = item.Key.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite);
                foreach (var property in properties)
                {
                    var propertyValue = property.GetValue(source);
                    property.SetValue(item.Value, propertyValue);
                }
            }
            RecipeManager.Save();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RecipeManager.Save();
            this.Close();
        }
    }
}
