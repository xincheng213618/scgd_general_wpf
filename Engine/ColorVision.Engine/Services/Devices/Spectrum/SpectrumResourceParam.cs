using ColorVision.Engine.MySql;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public class TemplateSpectrumResourceParam : ITemplate<SpectrumResourceParam>
    {
        public TemplateSpectrumResourceParam()
        {
            IsUserControl = true;
            TemplateDicId = 17; // SpectrumResourceParam
            Code = "SpectrumResource";
        }

        public SpectrumResourceControl SpectrumResourceControl { get; set; }
        public override UserControl GetUserControl() => SpectrumResourceControl;
        public override void SetUserControlDataContext(int index)
        {
            if (index < 0 || index >= TemplateParams.Count) return;
            SpectrumResourceControl.Initializedsss(Device, TemplateParams[index].Value);
        }

        public DeviceSpectrum Device { get; set; }


        public override void Load()
        {
            base.Load();
            SpectrumResourceParam.Load(TemplateParams, Device.SysResourceModel.Id);
        }

        public override void Create(string templateName)
        {
            SpectrumResourceParam? param = AddParamMode(templateName, Device.SysResourceModel.Id);
            if (param != null)
            {
                var a = new TemplateModel<SpectrumResourceParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(SpectrumResourceParam)}模板失败", "ColorVision");
            }
        }
    }

    public class SpectrumResourceParam:ParamModBase
    {
        public static void Load(ObservableCollection<TemplateModel<SpectrumResourceParam>> CalibrationParamModes, int resourceId)
        {
            CalibrationParamModes.Clear();
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<ModMasterModel> smus = MySqlControl.GetInstance().DB.Queryable<ModMasterModel>().Where(x => x.Pid == 7).Where(x => x.ResourceId == resourceId).Where(x => x.TenantId == UserConfig.Instance.TenantId).Where(x => x.IsDelete == false).ToList();
                foreach (var dbModel in smus)
                {
                    List<ModDetailModel> smuDetails = MySqlControl.GetInstance().DB.Queryable<ModDetailModel>().Where(it => it.Pid == dbModel.Id).ToList();
                    foreach (var dbDetail in smuDetails)
                    {
                        dbDetail.ValueA = dbDetail?.ValueA?.Replace("\\r", "\r");
                    }
                    CalibrationParamModes.Add(new TemplateModel<SpectrumResourceParam>(dbModel.Name ?? "default", new SpectrumResourceParam(dbModel, smuDetails)));
                }
            }
        }

        public SpectrumResourceParam() { }

        public SpectrumResourceParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        public string? ResourceMode { get { return GetValue(_ResourceMode); } set { SetProperty(ref _ResourceMode, value); } }
        private string? _ResourceMode;


        public string? ResourceName { get => GetValue(_ResourceName); set { SetProperty(ref _ResourceName, value); } }

        private string? _ResourceName;

        public int ResourceId { get => GetValue(_ResourceId); set { SetProperty(ref _ResourceId, value); } }
        private int _ResourceId =-1;

        public bool IsSelected { get => GetValue(_IsSelected); set { SetProperty(ref _IsSelected, value); } }
        private bool _IsSelected;

    }
}
