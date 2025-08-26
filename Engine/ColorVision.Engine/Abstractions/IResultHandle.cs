#pragma warning disable CA1707,CA1711
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using System.Collections.Generic;

namespace ColorVision.Engine.Abstractions
{
    public enum ViewResultAlgType
    {
        None = -1,
        POI = 0,
        POI_XYZ = 1,
        POI_Y = 2,
        FOV = 3,
        SFR = 4,
        MTF = 5,
        Ghost = 6,
        LedCheck = 7,
        LightArea = 8,
        Distortion = 9,
        BuildPOI = 10,
        Compliance_Math = 11,
        Compliance_Math_CIE_XYZ = 12,
        Compliance_Math_CIE_Y = 13,
        Compliance_Contrast = 14,
        Compliance_Contrast_CIE_XYZ = 15,
        Compliance_Contrast_CIE_Y = 16,
        LEDStripDetection = 17,
        RealPOI = 18,
        DataLoad = 19,
        POI_Y_V2 = 20,
        OLED_FindDotsArrayMem = 21,
        OLED_FindDotsArrayMem_File = 22,
        POI_XYZ_File = 23,
        POI_Y_File = 24,
        POI_XYZ_V2 = 25,
        OLED_FindDotsArrayOutFile = 26,
        OLED_JND_CalVas = 27,
        POI_CAD_Mapping = 28,
        Image_Cropping = 29,
        Calibration = 30,
        POIReviseGen = 31,
        OLED_RebuildPixelsMem = 32,
        BuildPOI_File = 33,
        Compliance_Math_JND = 34,
        POI_CIE_File = 35,
        FindLightArea = 36,
        OLED_FindDotsArrayByCornerPts = 37,
        OLED_FindDotsArrayByCornerPts_File = 38,
        AOI = 39,
        ThirdPartyAlgorithms_File = 40,
        ThirdPartyAlgorithms_RealParam = 41,
        OLED_FindLedPosition = 42,
        OLED_FindLedPosition_File = 43,
        KB = 50,
        KB_Raw = 51,
        KB_Output = 52,
        Compliance_Math_CIE_Y_Ex = 53,
        ARVR_BinocularFusion = 54,
        KB_Output_Lv = 55,
        KB_Output_CIE = 56,
        ARVR_SFR_FindROI = 60,
        ARVR_AAFindPoints = 61,
        PoiAnalysis = 62,
        FindCross = 63,
        Math_DataConvert = 70,
        BlackMura_Calc = 80
    }
    public interface IResultHandle
    {
        public bool CanHandle1(ViewResultAlg result);

        void Handle(AlgorithmView view, ViewResultAlg result);
        void SideSave(ViewResultAlg result, string selectedPath);
    }

    public abstract class IResultHandleBase : IResultHandle
    {
        public abstract List<ViewResultAlgType> CanHandle { get; }
        public virtual bool CanHandle1(ViewResultAlg result)
        {
            if (CanHandle.Contains(result.ResultType))
            {
                return true;
            }
            return false;
        }

        public abstract void Handle(AlgorithmView view, ViewResultAlg result);

        public virtual void Load(AlgorithmView view, ViewResultAlg result)
        {

        }
        public virtual void SideSave(ViewResultAlg result, string selectedPath)
        {

        }
    }

}
