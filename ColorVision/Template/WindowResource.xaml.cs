using ColorVision.SettingUp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace ColorVision.Template
{
    /// <summary>
    /// WindowResource.xaml 的交互逻辑
    /// </summary>
    public partial class WindowResource : Window
    {
        WindowTemplateType TemplateType { get; set; }
        TemplateControl TemplateControl { get; set; }
        public UserControl UserControl { get; set; }
        public ObservableCollection<ListConfig> ListConfigs { get; set; } = new ObservableCollection<ListConfig>();
        public WindowResource()
        {
            InitializeComponent();
        }

        public WindowResource(WindowTemplateType windowTemplateType, UserControl userControl)
        {
            this.TemplateType = windowTemplateType;
            this.TemplateControl = TemplateControl.GetInstance();
            InitializeComponent();

            this.GridProperty.Children.Clear();

            this.UserControl = userControl;
            this.GridProperty.Children.Add(UserControl);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ListConfigs = new ObservableCollection<ListConfig>();
            ListView1.ItemsSource = ListConfigs;
        }

        public string NewCreateFileName(string FileName)
        {
            List<string> Names = new List<string>();
            foreach (var item in ListConfigs)
            {
                Names.Add(item.Name);
            }
            for (int i = 1; i < 9999; i++)
            {
                if (!Names.Contains($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextBox_Name.Text))
            {
                if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                {
                    CreateNewTemplateFromDB();
                }
                else
                {
                    //CreateNewTemplateFromCsv();
                }
                TextBox_Name.Text = NewCreateFileName("default");
            }
            else
            {
                MessageBox.Show("请输入资源名称", Application.Current.MainWindow.Title, MessageBoxButton.OK);
            }
        }

        private void CreateNewTemplate<T>(ObservableCollection<KeyValuePair<string, T>> keyValuePairs, string Name, T t)
        {
            keyValuePairs.Add(new KeyValuePair<string, T>(Name, t));
            ListConfig config = new ListConfig() { ID = ListConfigs.Count + 1, Name = Name, Value = t };
            ListConfigs.Add(config);
            ListView1.SelectedIndex = ListConfigs.Count - 1;
            ListView1.ScrollIntoView(config);
        }

        private void CreateNewTemplateFromDB()
        {
            switch (TemplateType)
            {
                case WindowTemplateType.Services:
                    CameraDeviceParam? param = TemplateControl.AddFServiceParam(TextBox_Name.Text, TextBox_Code.Text);
                    if (param != null) CreateNewTemplate(TemplateControl.ServiceParams, TextBox_Name.Text, param);
                    else MessageBox.Show("数据库创建服务失败");
                    break;
            }
        }

        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ListView1_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void ListView1_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {

        }
    }
}
