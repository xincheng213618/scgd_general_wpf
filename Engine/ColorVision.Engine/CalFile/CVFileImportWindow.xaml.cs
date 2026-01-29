using ColorVision.Engine.CalFile;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

namespace ColorVision.Engine.CalFile
{
    public partial class CVFileImportWindow : Window
    {
        // 用于 DataGrid 显示的简单类
        public class InfoItem
        {
            public string PropertyName { get; set; } // 对应 "属性"
            public string Value { get; set; }        // 对应 "值"
        }

        public CVFileImportWindow(CvCameraInfoModel cameraInfo)
        {
            InitializeComponent();
            LoadDataFromObject(cameraInfo);
        }

        private void LoadDataFromObject(CvCameraInfoModel info)
        {
            if (info == null) return;

            var list = new List<InfoItem>();

            // 使用反射遍历实体类的所有属性
            PropertyInfo[] properties = info.GetType().GetProperties();

            foreach (var prop in properties)
            {
                // 获取属性值
                var val = prop.GetValue(info);

                list.Add(new InfoItem
                {
                    PropertyName = prop.Name, // 属性名 (例如 CameraModel)
                    Value = val != null ? val.ToString() : "null"
                });
            }

            InfoGrid.ItemsSource = list;
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