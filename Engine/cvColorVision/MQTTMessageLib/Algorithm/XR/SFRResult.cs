namespace MQTTMessageLib.Algorithm.XR;

public struct SFRResult
{
	public string Name { get; set; }

	public CRECT ROI { get; set; }

	public float[] pdfrequency { get; set; }

	public float[] pdomainSamplingData { get; set; }

	public SFRResult(string name, CRECT roi, float[] pdfrequency, float[] pdomainSamplingData)
	{
		Name = name;
		ROI = roi;
		this.pdfrequency = pdfrequency;
		this.pdomainSamplingData = pdomainSamplingData;
	}
}
