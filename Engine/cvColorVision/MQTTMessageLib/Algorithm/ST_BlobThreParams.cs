namespace MQTTMessageLib.Algorithm;

public struct ST_BlobThreParams
{
	public bool filterByColor;

	public int blobColor;

	public float minThreshold;

	public float thresholdStep;

	public float maxThreshold;

	public bool ifDEBUG;

	public float darkRatio;

	public float contrastRatio;

	public int bgRadius;

	public float minDistBetweenBlobs;

	public bool filterByArea;

	public float minArea;

	public float maxArea;

	public int minRepeatability;

	public bool filterByCircularity;

	public float minCircularity;

	public float maxCircularity;

	public bool filterByConvexity;

	public float minConvexity;

	public float maxConvexity;

	public bool filterByInertia;

	public float minInertiaRatio;

	public float maxInertiaRatio;
}
