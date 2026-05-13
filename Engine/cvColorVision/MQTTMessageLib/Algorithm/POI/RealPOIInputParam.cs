namespace MQTTMessageLib.Algorithm.POI;

public struct RealPOIInputParam
{
	public int CIE_MasterId { get; set; }

	public int POI_MasterId { get; set; }

	public bool IsSubPixel { get; set; }

	public bool IsCCTWave { get; set; }

	public CVTemplateParam FilterTemplate { get; set; }

	public CVTemplateParam ReviseTemplate { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }
}
