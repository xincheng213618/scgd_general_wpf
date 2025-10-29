using ColorVision.Common;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.IO;
using System.Windows;

namespace ProjectARVRLite
{

    public class RecipeManager
    {
        private static RecipeManager _instance;
        private static readonly object _locker = new();
        public static RecipeManager GetInstance() { lock (_locker) { _instance ??= new RecipeManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = ColorVisionPaths.ConfigDirectory;

        public static string RecipeFixPath { get; set; } = Path.Combine(DirectoryPath, "ProjectARVRLiteRecipe.json");
        public Dictionary<string, ARVRRecipeConfig> RecipeConfigs { get; set; }
        public RelayCommand EditCommand { get; set; }

        public ARVRRecipeConfig RecipeConfig { get; set; } = new ARVRRecipeConfig();

        public RecipeManager()
        {
            EditCommand = new RelayCommand(a => Edit());

            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (LoadFromFile(RecipeFixPath) is Dictionary<string, ARVRRecipeConfig> fix)
            {
                RecipeConfigs = fix;
            }
            else
            {
                RecipeConfigs = new Dictionary<string, ARVRRecipeConfig>();
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
