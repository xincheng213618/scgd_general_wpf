#pragma warning disable CS8604
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
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
    }
}
