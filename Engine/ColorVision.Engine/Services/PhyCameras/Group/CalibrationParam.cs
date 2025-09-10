#pragma warning disable CS8603,CS0649,CS8604,CS8601
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Rbac;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.PhyCameras.Group
{
    public class CalibrationBase : ModelBase
    {
        public RelayCommand SelectFileCommand { get; set; }

        public CalibrationBase(List<ModDetailModel> detail, string propertyName = "") : base(detail)
        {
            SelectFileCommand = new RelayCommand((s) =>
            {
                using var dialog = new System.Windows.Forms.OpenFileDialog();
                dialog.Filter = "DAT|*.dat||";
                dialog.RestoreDirectory = true;
                dialog.FilterIndex = 1;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FilePath = dialog.FileName;
                }
            });
            this.propertyName = propertyName;
        }
        private string propertyName = string.Empty;

        public string FilePath { get { if (string.IsNullOrWhiteSpace(propertyName)) return GetValue(_FilePath); else return GetValue(_FilePath, propertyName); } set { if (string.IsNullOrWhiteSpace(propertyName)) { SetProperty(ref _FilePath, value); } else { SetProperty(ref _FilePath, value, propertyName); OnPropertyChanged(); } } }
        private string _FilePath = string.Empty;

        public bool IsSelected { get { if (string.IsNullOrWhiteSpace(propertyName + "IsSelected")) return GetValue(_IsSelected); else return GetValue(_IsSelected, propertyName + "IsSelected"); } set { if (string.IsNullOrWhiteSpace(propertyName + "IsSelected")) SetProperty(ref _IsSelected, value); else SetProperty(ref _IsSelected, value, propertyName + "IsSelected"); OnPropertyChanged(); } }
        private bool _IsSelected;

        public override int Id { get { if (string.IsNullOrWhiteSpace(propertyName + "Id")) return GetValue(_Id); else return GetValue(_Id, propertyName + "Id"); } set { if (string.IsNullOrWhiteSpace(propertyName + "Id")) SetProperty(ref _Id, value); else SetProperty(ref _Id, value, propertyName + "Id"); OnPropertyChanged(); } }
        private int _Id;
    }



    public class CalibrationNormal
    {

        public CalibrationNormal(List<ModDetailModel> detail, string Type)
        {

            DarkNoise = new CalibrationBase(detail, nameof(DarkNoise) + Type);
            DefectPoint = new CalibrationBase(detail, nameof(DefectPoint) + Type);
            DSNU = new CalibrationBase(detail, nameof(DSNU) + Type);
            Uniformity = new CalibrationBase(detail, nameof(Uniformity) + Type);
            Distortion = new CalibrationBase(detail, nameof(Distortion) + Type);
            ColorShift = new CalibrationBase(detail, nameof(ColorShift) + Type);
            ColorDiff = new CalibrationBase(detail, nameof(ColorDiff) + Type);
            LineArity = new CalibrationBase(detail, nameof(LineArity) + Type);
        }
        public CalibrationBase DarkNoise { get; set; }
        public CalibrationBase DefectPoint { get; set; }
        public CalibrationBase DSNU { get; set; }
        public CalibrationBase Uniformity { get; set; }
        public CalibrationBase Distortion { get; set; }
        public CalibrationBase ColorShift { get; set; }
        public CalibrationBase ColorDiff { get; set; }
        public CalibrationBase LineArity { get; set; }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> keyValuePairs = new();
            if (DarkNoise.IsSelected)
                keyValuePairs.Add(nameof(DarkNoise), DarkNoise.FilePath);
            if (DefectPoint.IsSelected)
                keyValuePairs.Add(nameof(DefectPoint), DefectPoint.FilePath);
            if (DSNU.IsSelected)
                keyValuePairs.Add(nameof(DSNU), DSNU.FilePath);
            if (Uniformity.IsSelected)
                keyValuePairs.Add(nameof(Uniformity), Uniformity.FilePath);
            if (Distortion.IsSelected)
                keyValuePairs.Add(nameof(Distortion), Distortion.FilePath);
            if (ColorShift.IsSelected)
                keyValuePairs.Add(nameof(ColorShift), ColorShift.FilePath);
            if (ColorDiff.IsSelected)
                keyValuePairs.Add(nameof(ColorDiff), ColorDiff.FilePath);
            if (LineArity.IsSelected)
                keyValuePairs.Add(nameof(LineArity), LineArity.FilePath);        
            return keyValuePairs;
        }
    }

    public class CalibrationColor
    {
        public CalibrationColor(List<ModDetailModel> detail)
        {
            Luminance = new CalibrationBase(detail, nameof(Luminance));
            LumOneColor = new CalibrationBase(detail, nameof(LumOneColor));
            LumFourColor = new CalibrationBase(detail, nameof(LumFourColor));
            LumMultiColor = new CalibrationBase(detail, nameof(LumMultiColor));

            Luminance.PropertyChanged += (s, e) =>
            {
                if (Luminance.IsSelected)
                {
                    LumOneColor.IsSelected = false;
                    LumFourColor.IsSelected = false;
                    LumMultiColor.IsSelected = false;
                }
            };
            LumOneColor.PropertyChanged += (s, e) =>
            {
                if (LumOneColor.IsSelected)
                {
                    Luminance.IsSelected = false;
                    LumFourColor.IsSelected = false;
                    LumMultiColor.IsSelected = false;
                }
            };
            LumFourColor.PropertyChanged += (s, e) =>
            {
                if (LumFourColor.IsSelected)
                {
                    Luminance.IsSelected = false;
                    LumOneColor.IsSelected = false;
                    LumMultiColor.IsSelected = false;
                }
            };
            LumMultiColor.PropertyChanged += (s, e) =>
            {
                if (LumMultiColor.IsSelected)
                {
                    Luminance.IsSelected = false;
                    LumFourColor.IsSelected = false;
                    LumOneColor.IsSelected = false;
                }
            };
        }
        public CalibrationType CalibrationType
        {
            get
            {
                if (Luminance.IsSelected)
                    return CalibrationType.Luminance;
                else if (LumOneColor.IsSelected)
                    return CalibrationType.LumOneColor;
                else if (LumFourColor.IsSelected)
                    return CalibrationType.LumFourColor;
                else if (LumMultiColor.IsSelected)
                    return CalibrationType.LumMultiColor;
                else
                    return CalibrationType.Empty_Num;
            }
        }
        public CalibrationBase Luminance { get; set; }
        public CalibrationBase LumOneColor { get; set; }

        public CalibrationBase LumFourColor { get; set; }

        public CalibrationBase LumMultiColor { get; set; }
    }


    public class MysqlCalibrationParam : IMysqlCommand
    {
        public string GetMysqlCommandName() => "校正恢复";

        public string GetRecover()
        {
            string sql = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (206, 'Luminance', 206, '亮度', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (207, 'LuminanceIsSelected', 207, 'LuminanceIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:48', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (208, 'LumOneColor', 208, '单色', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (209, 'LumOneColorIsSelected', 209, 'LumOneColorIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:46', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (210, 'LumFourColor', 210, '四色', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (211, 'LumFourColorIsSelected', 211, 'LumFourColorIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:45', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (212, 'LumMultiColor', 212, '多色', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (213, 'LumMultiColorIsSelected', 213, 'LumMultiColorIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:44', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (214, 'Uniformity', 214, '均匀场', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (215, 'UniformityIsSelected', 215, 'UniformityIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:42', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (216, 'Distortion', 216, '畸变', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (217, 'DistortionIsSelected', 217, 'DistortionIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:41', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (218, 'ColorShift', 218, '色偏', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (219, 'ColorShiftIsSelected', 219, 'ColorShiftIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:39', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (220, 'DarkNoise', 220, 'DarkNoise', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (221, 'DarkNoiseIsSelected', 221, 'DarkNoiseIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:38', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (222, 'DefectPoint', 222, 'DefectPoint', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (223, 'DefectPointIsSelected', 223, 'DefectPointIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:37', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (224, 'DSNU', 224, 'DSNU', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (225, 'DSNUIsSelected', 225, 'DSNUIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:34', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (227, 'LuminanceId', 227, '亮度', 1 , NULL, '-1', 2, '2024-01-31 17:57:18', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (228, 'LumOneColorId', 228, '单色', 1 , NULL, '-1', 2, '2024-01-31 17:57:18', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (229, 'LumFourColorId', 229, '四色', 1 , NULL, '-1', 2, '2024-01-31 17:57:17', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (230, 'LumMultiColorId', 230, '多色', 1 , NULL, '-1', 2, '2024-01-31 17:57:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (231, 'UniformityId', 231, '均匀场', 1 , NULL, '-1', 2, '2024-01-31 17:57:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (232, 'DistortionId', 232, '畸变', 1 , NULL, '-1', 2, '2024-01-31 17:57:15', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (233, 'ColorShiftId', 233, '色偏', 1 , NULL, '-1', 2, '2024-01-31 17:57:15', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (234, 'DarkNoiseId', 234, 'DarkNoise', 1 , NULL, '-1', 2, '2024-01-31 17:57:14', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (235, 'DefectPointId', 235, 'DefectPoint', 1 , NULL, '-1', 2, '2024-01-31 17:57:13', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (236, 'DSNUId', 236, 'DSNU', 1 , NULL, '-1', 2, '2024-01-31 17:59:14', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (237, 'CalibrationMode', 237, 'CalibrationMode', 3 , NULL, '', 2, '2024-03-12 17:30:14', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (238, 'ColorDiff', 238, 'ColorDiff', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (239, 'ColorDiffId', 239, 'ColorDiff', 1 , NULL, '-1', 2, '2024-01-31 17:57:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (240, 'ColorDiffIsSelected', 240, 'ColorDiffIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:48', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (241, 'LineArity', 241, 'LineArity', 3 , NULL, NULL, 2, '2023-12-08 15:12:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (242, 'LineArityId', 242, 'LineArity', 1 , NULL, '-1', 2, '2024-01-31 17:57:16', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (243, 'LineArityIsSelected', 243, 'LineArityIsSelected', 2 , NULL, NULL, 2, '2023-12-08 15:22:48', 1 , 0, NULL);";
            return sql;
        }
    }

    public class TemplateCalibrationParam : ITemplate<CalibrationParam>
    {
        public TemplateCalibrationParam(PhyCamera device)
        {
            if (device.CalibrationParams.Count > 0)
            {
                CalibrationControl = new CalibrationControl(device, device.CalibrationParams[0].Value);
            }
            else
            {
                CalibrationControl = new CalibrationControl(device);
            }
            Name = $"camera,calibration,{device.Code}";
            TemplateDicId = 2;
            Title = "校正参数设置";
            Device = device;
            IsUserControl = true;
            Code = "calibration";
            TemplateParams = Device.CalibrationParams;
        }

        public CalibrationControl CalibrationControl { get; set; }
        public override UserControl GetUserControl() => CalibrationControl;

        public override void SetUserControlDataContext(int index)
        {
            if (index < 0 || index >= TemplateParams.Count) return;
            CalibrationControl.Initializedsss(TemplateParams[index].Value);
        }

        public PhyCamera Device { get; set; }

        public override IMysqlCommand? GetMysqlCommand() => new MysqlCalibrationParam();

        public override void Load()
        {
            CalibrationParam.LoadResourceParams(TemplateParams, Device.SysResourceModel.Id);
        }
        public override void Create(string templateName)
        {
            CalibrationParam? param = AddParamMode(templateName, Device.SysResourceModel.Id);
            if (param != null)
            {
                var a = new TemplateModel<CalibrationParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(CalibrationParam)}模板失败", "ColorVision");
            }
        }
    }



    public class CalibrationParam : ParamModBase
    {
        public static void LoadResourceParams<T>(ObservableCollection<TemplateModel<T>> ResourceParams, int resourceId) where T : ParamModBase, new()
        {
            if (!MySqlSetting.IsConnect)
                return;
            var existingParams = ResourceParams.ToDictionary(rp => rp.Id, rp => rp);

            List<ModMasterModel> smus = MySqlControl.GetInstance().DB.Queryable<ModMasterModel>().Where(x=>x.Pid ==2).Where(x => x.ResourceId == resourceId).Where(x=>x.TenantId == RbacManagerConfig.Instance.TenantId).Where(x => x.IsDelete == false).ToList();
            foreach (var dbModel in smus)
            {

                List<ModDetailModel> smuDetails = MySqlControl.GetInstance().DB.Queryable<ModDetailModel>() .Where(x => x.Pid == dbModel.Id).ToList();
                foreach (var dbDetail in smuDetails)
                {
                    dbDetail.ValueA = dbDetail?.ValueA?.Replace("\\r", "\r");
                }

                var newParam = (T)Activator.CreateInstance(typeof(T), new object[] { dbModel, smuDetails });

                if (existingParams.TryGetValue(dbModel.Id, out var existingModel))
                {
                    // Update the existing model
                    existingModel.Value = newParam;
                    existingModel.Key = dbModel.Name ?? "default";
                }
                else
                {
                    // Add new model
                    ResourceParams.Add(new TemplateModel<T>(dbModel.Name ?? "default", newParam));
                }
            }
        }


        public string CalibrationMode { get { return GetValue(_CalibrationMode); } set { SetProperty(ref _CalibrationMode, value); } }
        private string _CalibrationMode;

        public CalibrationNormal Normal { get; set; }
        public CalibrationColor Color { get; set; }

        public DeviceCamera DeviceCamera { get; set; }

        public CalibrationParam()
        {
            Id = -1;
            Normal = new CalibrationNormal(new List<ModDetailModel>(), "");
            Color = new CalibrationColor(new List<ModDetailModel>());
        }

        public CalibrationParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
            Normal = new CalibrationNormal(modDetails, "");
            Color = new CalibrationColor(modDetails);
        }
    }


}
