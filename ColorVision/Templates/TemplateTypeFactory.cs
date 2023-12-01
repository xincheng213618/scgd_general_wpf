#pragma warning disable CS8604
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.Templates.Algorithm;
using System.Collections.Generic;

namespace ColorVision.Templates
{
    public class TemplateTypeFactory 
    { 
        public static TemplateType GetWindowTemplateType(string code)
        {
            return code switch
            {
                ModMasterType.Aoi => TemplateType.AoiParam,
                ModMasterType.PG => TemplateType.PGParam,
                ModMasterType.SMU => TemplateType.SMUParam,
                ModMasterType.MTF => TemplateType.MTFParam,
                ModMasterType.SFR => TemplateType.SFRParam,
                ModMasterType.FOV => TemplateType.FOVParam,
                ModMasterType.Ghost => TemplateType.GhostParam,
                ModMasterType.Distortion => TemplateType.DistortionParam,
                ModMasterType.LedCheck => TemplateType.LedCheckParam,
                ModMasterType.FocusPoints => TemplateType.FocusPointsParam,
                ModMasterType.Calibration => TemplateType.Calibration,
                _ => TemplateType.AoiParam,
            };
        }

        public static string GetModeTemplateType(TemplateType windowTemplateType)
        {
            return windowTemplateType switch
            {
                TemplateType.AoiParam => ModMasterType.Aoi,
                TemplateType.PGParam => ModMasterType.PG,
                TemplateType.SMUParam => ModMasterType.SMU,
                TemplateType.MTFParam => ModMasterType.MTF,
                TemplateType.SFRParam => ModMasterType.SFR,
                TemplateType.FOVParam => ModMasterType.FOV,
                TemplateType.GhostParam => ModMasterType.Ghost,
                TemplateType.DistortionParam => ModMasterType.Distortion,
                TemplateType.LedCheckParam => ModMasterType.LedCheck,
                TemplateType.FocusPointsParam => ModMasterType.FocusPoints,
                TemplateType.Calibration => ModMasterType.Calibration,
                _ => string.Empty,
            };
        }

        public static ParamBase CreateParam(TemplateType windowTemplateType)
        {
            return windowTemplateType switch
            {
                TemplateType.AoiParam => new AOIParam(),
                TemplateType.Calibration => new CalibrationParam(),
                TemplateType.PGParam => new PGParam(),
                TemplateType.LedResult => new LedReusltParam(),
                TemplateType.SMUParam => new SMUParam(),
                TemplateType.PoiParam => new PoiParam(),
                TemplateType.FlowParam => new FlowParam(),
                TemplateType.MeasureParam => new MeasureParam(),
                TemplateType.MTFParam => new MTFParam(),
                TemplateType.SFRParam => new SFRParam(),
                TemplateType.FOVParam => new FOVParam(),
                TemplateType.GhostParam => new GhostParam(),
                TemplateType.DistortionParam => new DistortionParam(),
                TemplateType.LedCheckParam => new LedCheckParam(),
                TemplateType.FocusPointsParam => new FocusPointsParam(),
                _ => new ParamBase(),
            };
        }
        public static ParamBase CreateModeParam(TemplateType windowTemplateType, ModMasterModel  modMasterModel, List<ModDetailModel>  modDetailModels)
        {
            return windowTemplateType switch
            {
                TemplateType.AoiParam => new AOIParam(modMasterModel, modDetailModels),
                TemplateType.Calibration => new CalibrationParam(modMasterModel, modDetailModels),
                TemplateType.PGParam => new PGParam(modMasterModel, modDetailModels),
                TemplateType.LedResult => new LedReusltParam(modMasterModel, modDetailModels),
                TemplateType.SMUParam => new SMUParam(modMasterModel, modDetailModels),
                TemplateType.FlowParam => new FlowParam(modMasterModel, modDetailModels),
                TemplateType.MTFParam => new MTFParam(modMasterModel, modDetailModels),
                TemplateType.SFRParam => new SFRParam(modMasterModel, modDetailModels),
                TemplateType.FOVParam => new FOVParam(modMasterModel, modDetailModels),
                TemplateType.GhostParam => new GhostParam(modMasterModel, modDetailModels),
                TemplateType.DistortionParam => new DistortionParam(modMasterModel, modDetailModels),
                TemplateType.LedCheckParam => new LedCheckParam(modMasterModel, modDetailModels),
                TemplateType.FocusPointsParam => new FocusPointsParam(modMasterModel, modDetailModels),
                _ => new ParamBase(),
            };
        }
    }
}
