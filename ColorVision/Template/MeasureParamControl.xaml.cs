using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using NPOI.XWPF.UserModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Template
{
    public class MParamConfig : ViewModelBase
    {
        public MParamConfig(MeasureDetailModel model)
        {
            ID = model.Id;
            Name = model.Name;
            TypeName = model.PName;
            Type = model.PCode;
        }

        public MParamConfig(SysModMasterModel model)
        {
            ID = model.Id;
            Name = model.Name;
            Type = model.Code;
        }

        public MParamConfig(PoiParam item)
        {
            ID = item.ID;
            Name = item.PoiName;
            Type = "POI";
        }

        public MParamConfig(ModMasterModel item)
        {
            ID = item.Id;
            Name = item.Name;
            Type = item.Pcode;
        }

        public MParamConfig(int id, string name, string type)
        {
            ID = id;
            Name = name;
            Type = type;
        }

        public int ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string TypeName { get; set; }
    }
    /// <summary>
    /// MeasureParamControl.xaml 的交互逻辑
    /// </summary>
    public partial class MeasureParamControl : UserControl
    {
        private TemplateControl templateControl;
        public MeasureParamControl()
        {
            InitializeComponent();
            templateControl = TemplateControl.GetInstance();
        }

        public int MasterID { get; set; }
        public ObservableCollection<MParamConfig> ListConfigs { get; set; } = new ObservableCollection<MParamConfig>();
        public ObservableCollection<MParamConfig> ModTypeConfigs { get; set; } = new ObservableCollection<MParamConfig>();
        public ObservableCollection<MParamConfig> ModMasterConfigs { get; set; } = new ObservableCollection<MParamConfig>();
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ListView1.ItemsSource = ListConfigs;
            Mod_Type.ItemsSource = ModTypeConfigs;
            Mod_Master.ItemsSource = ModMasterConfigs;
        }

        private void Mod_Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(Mod_Type.SelectedItem != null)
            {
                ModMasterConfigs.Clear();
                MParamConfig config = (MParamConfig)Mod_Type.SelectedItem;
                if (config.Type.Equals("POI"))
                {
                    templateControl.LoadPoiParam();
                    foreach (var item in templateControl.PoiParams)
                    {
                        ModMasterConfigs.Add(new MParamConfig(item.Value));
                    }
                }
                else
                {
                    List<ModMasterModel> mods = templateControl.LoadModMasterByPid(config.ID);
                    foreach (var item in mods)
                    {
                        ModMasterConfigs.Add(new MParamConfig(item));
                    }
                }
            }
        }
        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            if (Mod_Type.SelectedItem != null)
            {
                MeasureDetailModel detailModel = new MeasureDetailModel();
                MParamConfig config = (MParamConfig)Mod_Type.SelectedItem;
                if (config.Type.Equals("POI"))
                {
                    detailModel.TType = 0;
                }
                else
                {
                    detailModel.TType = 1;
                }
                detailModel.Pid = MasterID;
                if (Mod_Master.SelectedItem != null)
                {
                    MParamConfig mod = (MParamConfig)Mod_Master.SelectedItem;
                    detailModel.TID = mod.ID;

                    templateControl.Save(detailModel);

                    reload();
                }
            }
        }

        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if(ListView1.SelectedItem!=null)
            {
                MParamConfig config = (MParamConfig)ListView1.SelectedItem;
                templateControl.ModMDetailDeleteById(config.ID);

                reload();
            }
        }

        private void reload()
        {
            List<MeasureDetailModel> des = templateControl.LoadMeasureDetail(MasterID);
            reload(des);
        }

        public void reload(List<MeasureDetailModel> des)
        {
            this.ListConfigs.Clear();
            foreach (MeasureDetailModel model in des)
            {
                this.ListConfigs.Add(new MParamConfig(model));
            }
        }
    }
}
