using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.POI;

public class DeviceParamPOIRevise : ParamDicBase
{
	private float _M;

	private float _N;

	private float _P;

	private GenReviseType _GenCalibrationType;

	public float M
	{
		get
		{
			return GetValue(_M, "M");
		}
		set
		{
			SetProperty(ref _M, value, "M");
		}
	}

	public float N
	{
		get
		{
			return GetValue(_N, "N");
		}
		set
		{
			SetProperty(ref _N, value, "N");
		}
	}

	public float P
	{
		get
		{
			return GetValue(_P, "P");
		}
		set
		{
			SetProperty(ref _P, value, "P");
		}
	}

	public GenReviseType GenCalibrationType
	{
		get
		{
			return GetValue(_GenCalibrationType, "GenCalibrationType");
		}
		set
		{
			SetProperty(ref _GenCalibrationType, value, "GenCalibrationType");
		}
	}

	public POIReviseItem ToReviseItem()
	{
		return new POIReviseItem
		{
			ReviseType = GenCalibrationType,
			m = M,
			n = N,
			p = P
		};
	}
}
