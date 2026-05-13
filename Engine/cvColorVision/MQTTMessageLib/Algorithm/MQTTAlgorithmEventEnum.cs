namespace MQTTMessageLib.Algorithm;

public class MQTTAlgorithmEventEnum
{
	public const string Event_POI_GetData = "POI";

	public const string Event_SFR_GetData = "SFR";

	public const string Event_FOV_GetData = "FOV";

	public const string Event_MTF_GetData = "MTF";

	public const string Event_Ghost_GetData = "Ghost";

	public const string Event_LED_Check_GetData = "LedCheck";

	public const string Event_LED_StripDetection = "LEDStripDetection";

	public const string Event_LightArea_GetData = "FocusPoints";

	public const string Event_LightArea2_GetData = "OLED.GetRIAandPT";

	public const string Event_FindLightArea = "FindLightArea";

	public const string Event_MatchTemplate = "MatchTemplate";

	public const string Event_Distortion_GetData = "Distortion";

	public const string Event_Build_POI = "BuildPOI";

	public const string Event_RealPOI_GetData = "Real_POI";

	public const string Event_Compliance_Math = "Compliance_Math";

	public const string Event_Compliance_Judgment = "Compliance.Judgment";

	public const string Event_Compliance_Contrast = "Compliance_Contrast";

	public const string Event_DataLoad_GetData = "DataLoad";

	public const string Event_POIReviseGen_GetData = "POIReviseGen";

	public const string Event_POI_CADMapping = "POI.CADMapping";

	public const string Event_Image_Cropping = "OLED.GetRIAand";

	public const string Event_LED_FindLedPosition = "OLED.FindLedPosition";

	public const string Event_FindLED = "FindLED";

	public const string Event_OLED_FindDotsArrayMem_GetData = "FindDotsArray";

	public const string Event_OLED_FindDotsArrayByCornerPts = "OLED.FindDotsArrayByCornerPts";

	public const string Event_OLED_RebuildPixelsMem_GetData = "OLED.RebuildPixels";

	public const string Event_OLED_RebuildPixelsPosMem = "OLED.RebuildPixelsPos";

	public const string Event_OLED_FindDotsArrayAndRebuildPixelsMem = "OLED.FindDotsArrayAndRebuildPixels";

	public const string Event_OLED_JND_CalVas_GetData = "OLED.JND.CalVas";

	public const string Event_OLED_FindVHLine = "OLED.FindVHLine";

	public const string Event_OLED_FindMura = "OLED.FindMura";

	public const string Event_OLED_FindPixelDefectsForRebuildPicGrading = "OLED.FindPixelDefectsForRebuildPicGrading";

	public const string Event_OLED_FindPixelDefectsForRebuildPicGradingV2 = "OLED.FindPixelDefectsForRebuildPicGradingV2";

	public const string Event_OLED_DetectBrightPixelsForBlackScreen = "OLED.DetectBrightPixelsForBlackScreen";

	public const string Event_OLED_FindPixelDefectsForRebuildPic = "OLED.FindPixelDefectsForRebuildPic";

	public const string Event_OLED_CombineQuaterImages = "OLED.CombineQuaterImages";

	public const string Event_OLED_FindPixelDefectsForQuardImg = "OLED.FindPixelDefectsForQuardImg";

	public const string Event_OLED_AOI_ALL = "OLED.ALL_AOI";

	public const string Event_OLED_ParticlesFindAndFill = "OLED.Particles.FindAndFill";

	public const string Event_KB_GetData = "KB";

	public const string Event_KB_Output = "KB.Output";

	public const string Event_BinocularFusion_GetData = "ARVR.BinocularFusion";

	public const string Event_ARVR_SFR_FindROI = "ARVR.SFR.FindROI";

	public const string Event_ARVR_AAFindPoints = "ARVR.AA.FindPoints";

	public const string Event_BlackMura_Calc = "BlackMura.Caculate";

	public const string Event_DataConvert = "Math.DataConvert";

	public const string Event_PoiAnalysis = "PoiAnalysis";

	public const string Event_FindCross = "FindCross";

	public const string Event_CompoundImg = "CompoundImg";

	public const string Event_Calc_EQE = "CalcEQE";

	public const string Event_CaliAngleShift = "CaliAngleShift";

	public const string Event_ImageROI = "Image.ROI";

	public const string Event_ImageConvert = "Image.Convert";
}
