#pragma warning disable CS8603,CS0649,CS8604,CS8601
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Core;
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

        public string FilePath { get { if (string.IsNullOrWhiteSpace(propertyName)) return GetValue(_FilePath); else return GetValue(_FilePath, propertyName); } set { if (string.IsNullOrWhiteSpace(propertyName)) { SetProperty(ref _FilePath, value); } else { SetProperty(ref _FilePath, value, propertyName); NotifyPropertyChanged(); } } }
        private string _FilePath;

        public bool IsSelected { get { if (string.IsNullOrWhiteSpace(propertyName + "IsSelected")) return GetValue(_IsSelected); else return GetValue(_IsSelected, propertyName + "IsSelected"); } set { if (string.IsNullOrWhiteSpace(propertyName + "IsSelected")) SetProperty(ref _IsSelected, value); else SetProperty(ref _IsSelected, value, propertyName + "IsSelected"); NotifyPropertyChanged(); } }
        private bool _IsSelected;

        public override int Id { get { if (string.IsNullOrWhiteSpace(propertyName + "Id")) return GetValue(_Id); else return GetValue(_Id, propertyName + "Id"); } set { if (string.IsNullOrWhiteSpace(propertyName + "Id")) SetProperty(ref _Id, value); else SetProperty(ref _Id, value, propertyName + "Id"); NotifyPropertyChanged(); } }
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
        }
        public CalibrationBase DarkNoise { get; set; }
        public CalibrationBase DefectPoint { get; set; }
        public CalibrationBase DSNU { get; set; }
        public CalibrationBase Uniformity { get; set; }
        public CalibrationBase Distortion { get; set; }
        public CalibrationBase ColorShift { get; set; }

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


    public class TemplateCalibrationParam : ITemplate<CalibrationParam>
    {
        public TemplateCalibrationParam(ICalibrationService<ServiceObjectBase> device)
        {
            if (device.CalibrationParams.Count > 0)
            {
                CalibrationControl = new CalibrationControl(device, device.CalibrationParams[0].Value);
            }
            else
            {
                CalibrationControl = new CalibrationControl(device);
            }

            Title = "校正参数设置";
            Device = device;
            IsUserControl = true;
            Code = "calibration";
            TemplateParams = Device.CalibrationParams;
        }

        public CalibrationControl CalibrationControl { get; set; }
        public override UserControl GetUserControl() => CalibrationControl;

        public override bool ExitsTemplateName(string templateName)
        {
            ModMasterDao modMasterDao = new ModMasterDao("calibration");
            List<ModMasterModel> smus = modMasterDao.GetAll(UserConfig.Instance.TenantId);
           return smus.Any(a => a.Name?.Equals(templateName, StringComparison.OrdinalIgnoreCase) ?? false);
        }
        public override void SetUserControlDataContext(int index)
        {
            if (index < 0 || index >= TemplateParams.Count) return;
            CalibrationControl.Initializedsss(Device, TemplateParams[index].Value);
        }

        public ICalibrationService<ServiceObjectBase> Device { get; set; }


        public override void Load()
        {
            CalibrationParam.LoadResourceParams(TemplateParams, Device.SysResourceModel.Id);
        }
        public override void Create(string templateName)
        {
            CalibrationParam? param = AddParamMode(Code, templateName, Device.SysResourceModel.Id);
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

            // Create a dictionary for efficient lookup of existing items
            var existingParams = ResourceParams.ToDictionary(rp => rp.Id, rp => rp);
            ModMasterDao masterFlowDao = new("calibration");
            List<ModMasterModel> smus = masterFlowDao.GetResourceAll(UserConfig.Instance.TenantId, resourceId);

            foreach (var dbModel in smus)
            {
                List<ModDetailModel> smuDetails = ModDetailDao.Instance.GetAllByPid(dbModel.Id);
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
