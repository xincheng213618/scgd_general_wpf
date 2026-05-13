using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.XR;

public struct DistortionResult(PointFloat[] finalPoints, PointDouble point, double maxErrorRatio, double t, DistortionType distortionType, DisLayoutType layoutType, DisSlopeType slopeType, DisCornerType cornerType)
{
	public PointFloat[] finalPoints = finalPoints;

	public PointDouble point = point;

	public double maxErrorRatio = maxErrorRatio;

	public double t = t;

	public DistortionType distortionType = distortionType;

	public DisLayoutType layoutType = layoutType;

	public DisSlopeType slopeType = slopeType;

	public DisCornerType cornerType = cornerType;
}
