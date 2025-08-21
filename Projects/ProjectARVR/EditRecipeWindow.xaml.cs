using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.IO;
using System.Windows;

namespace ProjectARVR
{

    public class RecipeManager
    {
        private static RecipeManager _instance;
        private static readonly object _locker = new();
        public static RecipeManager GetInstance() { lock (_locker) { _instance ??= new RecipeManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string ObjectiveTestResultFixPath { get; set; } = DirectoryPath + "ProjectARVRLite.json";
        public Dictionary<string, ARVRRecipeConfig> RecipeConfigs { get; set; }

        public ARVRRecipeConfig RecipeConfig { get; set; } = new ARVRRecipeConfig();

        public RecipeManager()
        {
            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (LoadFromFile(ObjectiveTestResultFixPath) is Dictionary<string, ARVRRecipeConfig> fix)
            {
                RecipeConfigs = fix;
            }
            else
            {
                RecipeConfigs = new Dictionary<string, ARVRRecipeConfig>();
                Save();
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);

                string json = JsonConvert.SerializeObject(RecipeConfigs, Formatting.Indented);
                File.WriteAllText(ObjectiveTestResultFixPath, json);
            }
            catch
            {
                // Optionally log or rethrow
            }
        }

        public static Dictionary<string, ARVRRecipeConfig>? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<Dictionary<string, ARVRRecipeConfig>>(json);
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
            EditStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(RecipeManager.RecipeConfig));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            RecipeManager.Save();
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var ObjectiveTestResultFix  = new ARVRRecipeConfig();
            RecipeManager.RecipeConfig.CopyFrom(ObjectiveTestResultFix);
            RecipeManager.Save();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RecipeManager.Save();
            this.Close();
        }
    }
}
