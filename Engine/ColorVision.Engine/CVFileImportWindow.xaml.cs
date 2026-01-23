using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

namespace ColorVision.Engine
{
    /// <summary>
    /// CVFileImportWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CVFileImportWindow : Window
    {
        public class InfoItem
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }

        public CVFileImportWindow(string jsonContent)
        {
            InitializeComponent();
            LoadJson(jsonContent);
        }

        private void LoadJson(string json)
        {
            try
            {
                var jObject = JObject.Parse(json);
                var list = new List<InfoItem>();

                foreach (var property in jObject.Properties())
                {
                    list.Add(new InfoItem
                    {
                        Key = property.Name,
                        Value = property.Value.ToString()
                    });
                }

                InfoGrid.ItemsSource = list;
            }
            catch
            {
                MessageBox.Show("配置内容解析失败", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
