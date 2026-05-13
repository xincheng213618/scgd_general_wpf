using CVCommCore;

namespace MQTTMessageLib.Tool;

public class CVResultConverter
{
	public static CVResultType Convert(AlgorithmResultType src)
	{
		CVResultType result = (CVResultType)src;
		switch (src)
		{
		case AlgorithmResultType.POI_XYZ_File:
			result = CVResultType.Algorithm_POI_XYZ_File;
			break;
		case AlgorithmResultType.OLED_FindDotsArrayMem:
			result = CVResultType.Algorithm_FindDotsArrayMem;
			break;
		case AlgorithmResultType.OLED_FindDotsArrayMem_File:
		case AlgorithmResultType.OLED_FindDotsArrayOutFile:
		case AlgorithmResultType.BuildPOI_File:
			result = CVResultType.Algorithm_POI_LED_File;
			break;
		case AlgorithmResultType.POI_Y_File:
			result = CVResultType.Algorithm_POI_Y_File;
			break;
		case AlgorithmResultType.POI_XYZ:
			result = CVResultType.Algorithm_POI_XYZ;
			break;
		case AlgorithmResultType.POI_Y:
			result = CVResultType.Algorithm_POI_Y;
			break;
		case AlgorithmResultType.POI_Y_V2:
			result = CVResultType.Algorithm_POI_Y_V2;
			break;
		case AlgorithmResultType.FOV:
			result = CVResultType.Algorithm_FOV;
			break;
		case AlgorithmResultType.SFR:
			result = CVResultType.Algorithm_SFR;
			break;
		case AlgorithmResultType.MTF:
			result = CVResultType.Algorithm_MTF;
			break;
		case AlgorithmResultType.JND_CalVas:
			result = CVResultType.Algorithm_POI_JND;
			break;
		case AlgorithmResultType.Ghost:
			result = CVResultType.Algorithm_Ghost;
			break;
		case AlgorithmResultType.LedCheck:
			result = CVResultType.Algorithm_LedCheck;
			break;
		case AlgorithmResultType.LightArea:
			result = CVResultType.Algorithm_LightArea;
			break;
		case AlgorithmResultType.Distortion:
			result = CVResultType.Algorithm_Distortion;
			break;
		case AlgorithmResultType.BuildPOI:
			result = CVResultType.Algorithm_BuildPOI;
			break;
		case AlgorithmResultType.Compliance_Math_CIE_XYZ:
			result = CVResultType.Algorithm_Compliance_STD_CIE_XYZ;
			break;
		case AlgorithmResultType.Compliance_Math_CIE_Y:
			result = CVResultType.Algorithm_Compliance_STD_CIE_Y;
			break;
		case AlgorithmResultType.Compliance_Contrast_CIE_XYZ:
			result = CVResultType.Algorithm_Compliance_STD_CIE_XYZ;
			break;
		case AlgorithmResultType.Compliance_Contrast_CIE_Y:
			result = CVResultType.Algorithm_Compliance_STD_CIE_Y;
			break;
		case AlgorithmResultType.LEDStripDetection:
			result = CVResultType.Algorithm_LEDStripDetection;
			break;
		case AlgorithmResultType.Compliance_Math_JND:
			result = CVResultType.Algorithm_Compliance_STD_JND;
			break;
		case AlgorithmResultType.KB:
			result = CVResultType.Algorithm_KB;
			break;
		case AlgorithmResultType.KB_Raw:
			result = CVResultType.Algorithm_KB_Raw;
			break;
		case AlgorithmResultType.KB_Output_Lv:
			result = CVResultType.Algorithm_KB_Output_Lv;
			break;
		case AlgorithmResultType.KB_Output_CIE:
			result = CVResultType.Algorithm_KB_Output_CIE;
			break;
		case AlgorithmResultType.ARVR_BinocularFusion:
			result = CVResultType.Algorithm_ARVR_BF;
			break;
		}
		return result;
	}
}
