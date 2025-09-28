using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
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

        public static string ObjectiveTestResultFixPath { get; set; } = DirectoryPath + "ObjectiveTestResultFix.json";
        public RelayCommand EditCommand { get; set; }

        public ObjectiveTestResultFix ObjectiveTestResultFix { get; set; }

        public FixManager()
        {
            EditCommand = new RelayCommand(a => Edit());

            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (LoadFromFile(ObjectiveTestResultFixPath) is ObjectiveTestResultFix fix)
            {
                ObjectiveTestResultFix = fix;
            }
            else
            {
                ObjectiveTestResultFix = new ObjectiveTestResultFix();
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

                string json = JsonConvert.SerializeObject(ObjectiveTestResultFix, Formatting.Indented);
                File.WriteAllText(ObjectiveTestResultFixPath, json);
            }
            catch
            {
                // Optionally log or rethrow
            }
        }

        public static ObjectiveTestResultFix? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<ObjectiveTestResultFix>(json);
            }
            catch
            {
                return null;
            }
        }

    }


    /// <summary>
    /// EditFixWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditFixWindow : Window
    {
        FixManager FixManager { get; set; }
        public EditFixWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            FixManager = FixManager.GetInstance();
            EditStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(FixManager.ObjectiveTestResultFix));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            FixManager.Save();
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var ObjectiveTestResultFix  = new ObjectiveTestResultFix();
            FixManager.ObjectiveTestResultFix.CopyFrom(ObjectiveTestResultFix);
            FixManager.Save();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            FixManager.Save();
            this.Close();
        }
    }
}
