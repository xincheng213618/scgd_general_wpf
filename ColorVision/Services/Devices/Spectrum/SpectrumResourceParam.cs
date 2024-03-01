using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Services.Devices.Spectrum
{
    public class SpectrumResourceParam:ParamBase
    {
        public static void Load(ObservableCollection<TemplateModel<SpectrumResourceParam>> CalibrationParamModes, int resourceId, string ModeType)
        {
            ModDetailDao detailDao = new ModDetailDao();
            CalibrationParamModes.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                ModMasterDao masterFlowDao = new ModMasterDao(ModeType);
                List<ModMasterModel> smus = masterFlowDao.GetResourceAll(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId, resourceId);
                foreach (var dbModel in smus)
                {
                    List<ModDetailModel> smuDetails = detailDao.GetAllByPid(dbModel.Id);
                    foreach (var dbDetail in smuDetails)
                    {
                        dbDetail.ValueA = dbDetail?.ValueA?.Replace("\\r", "\r");
                    }
                    CalibrationParamModes.Add(new TemplateModel<SpectrumResourceParam>(dbModel.Name ?? "default", new SpectrumResourceParam(dbModel, smuDetails)));
                }
            }
        }

        public SpectrumResourceParam() { }

        public SpectrumResourceParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
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
