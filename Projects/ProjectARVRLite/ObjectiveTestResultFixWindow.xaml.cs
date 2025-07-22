using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ProjectARVRLite
{

    public class ObjectiveTestResultFixManager
    {
        private static ObjectiveTestResultFixManager _instance;
        private static readonly object _locker = new();
        public static ObjectiveTestResultFixManager GetInstance() { lock (_locker) { _instance ??= new ObjectiveTestResultFixManager(); return _instance; } }
        public static string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public static string ObjectiveTestResultFixPath { get; set; } = DirectoryPath + "ObjectiveTestResultFix.json";

        public ObjectiveTestResultFix ObjectiveTestResultFix { get; set; }

        public ObjectiveTestResultFixManager()
        {
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
    /// ObjectiveTestResultFixWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ObjectiveTestResultFixWindow : Window
    {
        ObjectiveTestResultFixManager ObjectiveTestResultFixManager { get; set; }
        public ObjectiveTestResultFixWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ObjectiveTestResultFixManager = ObjectiveTestResultFixManager.GetInstance();
            EditStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(ObjectiveTestResultFixManager.ObjectiveTestResultFix));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ObjectiveTestResultFixManager.Save();
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var ObjectiveTestResultFix  = new ObjectiveTestResultFix();
            ObjectiveTestResultFixManager.ObjectiveTestResultFix.CopyFrom(ObjectiveTestResultFix);
            ObjectiveTestResultFixManager.Save();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            ObjectiveTestResultFixManager.Save();
            this.Close();
        }
    }
}
