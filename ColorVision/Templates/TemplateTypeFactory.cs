using ColorVision.MySql.Service;

namespace ColorVision.Templates
{
    public enum TemplateType
    {
        FlowParam,
        MeasureParam,
        Calibration,
        LedResult,
        AoiParam,
        PGParam,
        SMUParam,
        PoiParam,
        MTFParam,
        SFRParam,
        FOVParam,
        GhostParam,
        DistortionParam,
        LedCheckParam,
        FocusPointsParam
    }

    public class TemplateTypeFactory 
    { 
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
