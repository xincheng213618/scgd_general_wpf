namespace MQTTMessageLib.Algorithm.POI;

public struct POIInputParam
{
	public CVTemplateParam FilterTemplate { get; set; }

	public CVTemplateParam ReviseTemplate { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public bool IsSubPixel { get; set; }

	public bool IsCCTWave { get; set; }
}
