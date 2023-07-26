using ColorVision.MySql.DAO;
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

    public class ServiceSetting
    {
        public string ServiceName { get; set; }
        public string UserTopic { get; set; }
        public string ServiceTopic { get; set; }
        public int ServiceType { get; set; }
    }


    public class ResourceTypeConfig
    {
        public string Name { get; set; }
        public int Type { get; set; }
        public int Value { get; set; }

        public ResourceTypeConfig(string name,int val) {
            this.Name = name;
            this.Value = val;
        }

        public ResourceTypeConfig(string name, int val, int type)
        {
            this.Name = name;
            this.Value = val;
            this.Type = type;
        }
    }
    /// <summary>
    /// WindowResource.xaml 的交互逻辑
    /// </summary>
    public partial class WindowResource : Window
    {
        WindowTemplateType TemplateType { get; set; }
        TemplateControl TemplateControl { get; set; }
        public UserControl UserControl { get; set; }
        public ObservableCollection<ListConfig> ListConfigs { get; set; } = new ObservableCollection<ListConfig>();
        public ObservableCollection<ResourceTypeConfig> ResourceTypes { get; set; } = new ObservableCollection<ResourceTypeConfig>();
        public WindowResource()
        {
            InitializeComponent();
        }

        public WindowResource(WindowTemplateType windowTemplateType, UserControl userControl)
        {
            this.TemplateType = windowTemplateType;
            this.TemplateControl = TemplateControl.GetInstance();
            InitializeComponent();
            this.UserControl = userControl;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ListConfigs = new ObservableCollection<ListConfig>();
            ListView1.ItemsSource = ListConfigs;

            switch (TemplateType)
            {
                case WindowTemplateType.Devices:
                    List<SysResourceModel> res = TemplateControl.LoadAllServices();
                    res.ForEach(item => { ResourceTypes.Add(new ResourceTypeConfig(item.Name ?? string.Empty, item.Id,item.Type)); });
                    break;
                case WindowTemplateType.Services:
                    List<SysDictionaryModel> svrs = TemplateControl.LoadServiceType();
                    svrs.ForEach(item => { ResourceTypes.Add(new ResourceTypeConfig(item.Name ?? string.Empty, item.Value)); });
                    break;
                default:
                    break;
            }

            TextBox_Type.ItemsSource = ResourceTypes;
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
                CreateNewTemplateFromDB();
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
                case WindowTemplateType.Devices:
                    ResourceParam? paramDev = TemplateControl.AddDeviceParam(TextBox_Name.Text, TextBox_Code.Text, ((ResourceTypeConfig)TextBox_Type.SelectedItem).Type, ((ResourceTypeConfig)TextBox_Type.SelectedItem).Value);
                    if (paramDev != null) CreateNewTemplate(TemplateControl.DeviceParams, TextBox_Name.Text, paramDev);
                    else MessageBox.Show("数据库创建设备失败");
                    break;
                case WindowTemplateType.Services:
                    ResourceParam? param = TemplateControl.AddServiceParam(TextBox_Name.Text, TextBox_Code.Text, ((ResourceTypeConfig)TextBox_Type.SelectedItem).Value);
                    if (param != null) CreateNewTemplate(TemplateControl.ServiceParams, TextBox_Name.Text, param);
                    else MessageBox.Show("数据库创建服务失败");
                    break;
            }
        }

        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show($"是否删除资源{ListView1.SelectedIndex + 1},删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    switch (TemplateType)
                    {
                        case WindowTemplateType.Devices:
                            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                                TemplateControl.ResourceDeleteById(TemplateControl.DeviceParams[ListView1.SelectedIndex].Value.ID);
                            TemplateControl.DeviceParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                        case WindowTemplateType.Services:
                            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
                                TemplateControl.ResourceDeleteById(TemplateControl.ServiceParams[ListView1.SelectedIndex].Value.ID);
                            TemplateControl.ServiceParams.RemoveAt(ListView1.SelectedIndex);
                            break;
                    }
                    ListConfigs.RemoveAt(ListView1.SelectedIndex);
                    ListView1.SelectedIndex = ListConfigs.Count - 1;
                }
            }
            else
            {
                MessageBox.Show("请先选择" + TemplateGrid.Header);
            }
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
