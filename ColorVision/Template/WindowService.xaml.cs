using ColorVision.Extension;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
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

    public static class BaseObjectExtensions
    {
        /// <summary>
        /// 得到指定数据类型的祖先节点。
        /// </summary>
        public static T? GetAncestor<T>(this BaseObject This) where T : BaseObject
        {
            if (This is T t)
                return t;

            if (This.Parent == null)
                return null;

            return This.Parent.GetAncestor<T>();
        }
    }


    public class BaseObject : ViewModelBase
    {
        public  ObservableCollection<BaseObject> VisualChildren { get; set; }
        public BaseObject()
        {
            VisualChildren = new ObservableCollection<BaseObject>();
        }
        public BaseObject Parent
        {
            get { return _Parent; }
            set
            {
                _Parent = value;
                NotifyPropertyChanged();
            }
        }
        private BaseObject _Parent;

        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public virtual void AddChild(BaseObject baseObject)
        {
            if (baseObject == null) return;
            baseObject.Parent = this;
            VisualChildren.SortedAdd(baseObject);
        }

    }

    public class MQTTDevice : BaseObject
    {
        public ResourceParam ResourceParam { get; set; }

        public MQTTDevice() : base()
        {

        }
    }

    public class MQTTService : BaseObject
    {
        public ResourceParam ResourceParam { get; set; }

        public MQTTService() : base()
        {

        }


    }




    /// <summary>
    /// WindowService.xaml 的交互逻辑
    /// </summary>
    public partial class WindowService : Window
    {
        TemplateControl TemplateControl { get; set; }

        public WindowService()
        {
            InitializeComponent();
        }
        public ObservableCollection<MQTTService> MQTTServices { get; set; }

        private SysResourceService resourceService = new SysResourceService();
        private SysDictionaryService dictionaryService = new SysDictionaryService();

        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTServices = new ObservableCollection<MQTTService>();

            TemplateControl = TemplateControl.GetInstance();


            List<SysResourceModel> Services = resourceService.GetAllServices(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            List<SysResourceModel> devices = resourceService.GetAllDevices(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            foreach (var item in dictionaryService.GetAllServiceType())
            {
                MQTTService mQTTServicetype = new MQTTService();
                mQTTServicetype.Name = item.Name ?? string.Empty;
                foreach (var item1 in Services)
                {
                    if (item1.Type == item.Value)
                    {
                        MQTTService mQTTService = new MQTTService();
                        mQTTService.Name = item1.Name ?? string.Empty;


                        foreach (var item2 in devices)
                        {
                            if (item2.Type == item1.Type)
                            {
                                MQTTDevice device = new MQTTDevice();
                                device.Name = item2.Name ?? string.Empty; ;
                                mQTTService.AddChild(device);
                            }
                        }

                        mQTTServicetype.AddChild(mQTTService);
                    }

                }
                MQTTServices.Add(mQTTServicetype);

            }
            TreeView1.ItemsSource = MQTTServices;
        }

    }
}
