﻿using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates.POI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Templates
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
            ID = item.Id;
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
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? TypeName { get; set; }
    }
    /// <summary>
    /// MeasureParamControl.xaml 的交互逻辑
    /// </summary>
    public partial class MeasureParamControl : UserControl
    {
        private TemplateControl TemplateControl = TemplateControl.GetInstance();
        public MeasureParamControl()
        {
            InitializeComponent();
        }
        private MeasureMasterDao measureMaster = new MeasureMasterDao();
        private MeasureDetailDao measureDetail = new MeasureDetailDao();
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
        private ModMasterDao masterModDao = new ModMasterDao();

        private void ModTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(Mod_Type.SelectedItem != null)
            {
                ModMasterConfigs.Clear();
                MParamConfig config = (MParamConfig)Mod_Type.SelectedItem;
                if (config.Type != null && config.Type.Equals("POI", StringComparison.Ordinal))
                {
                    TemplateControl.LoadPoiParam();
                    foreach (var item in TemplateControl.PoiParams)
                    {
                        ModMasterConfigs.Add(new MParamConfig(item.Value));
                    }
                }
                else
                {
                    List<ModMasterModel> mods = masterModDao.GetAllByPid(config.ID);
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
                if (config.Type!=null&&config.Type.Equals("POI", StringComparison.Ordinal))
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
                    measureDetail.Save(detailModel);
                    Reload();
                }
            }
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if(ListView1.SelectedItem!=null)
            {
                MParamConfig config = (MParamConfig)ListView1.SelectedItem;
                measureDetail.DeleteById(config.ID);

                Reload();
            }
        }

        private void Reload()
        {
            List<MeasureDetailModel> des = measureDetail.GetAllByPid(MasterID);
            Reload(des);
        }

        public void Reload(List<MeasureDetailModel> des)
        {
            this.ListConfigs.Clear();
            foreach (MeasureDetailModel model in des)
            {
                this.ListConfigs.Add(new MParamConfig(model));
            }
        }
    }
}
