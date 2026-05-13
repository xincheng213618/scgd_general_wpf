using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceParamDistortion : ParamDicBase
{
	private DisCornerType _type;

	private DisSlopeType _sType = DisSlopeType.lb_Variance;

	private DisLayoutType _lType = DisLayoutType.SlopeOUT;

	private DistortionType _dType = DistortionType.TVDistV;

	private int _cx;

	private int _cy;

	private bool _filterByColor;

	private int _blobColor;

	private float _minThreshold;

	private float _thresholdStep;

	private float _maxThreshold;

	private bool _ifDEBUG;

	private float _darkRatio;

	private float _contrastRatio;

	private int _bgRadius;

	private float _minDistBetweenBlobs;

	private bool _filterByArea;

	private float _minArea;

	private float _maxArea;

	private int _minRepeatability;

	private bool _filterByCircularity;

	private float _minCircularity;

	private float _maxCircularity;

	private bool _filterByConvexity;

	private float _minConvexity;

	private float _maxConvexity;

	private bool _filterByInertia;

	private float _minInertiaRatio;

	private float _maxInertiaRatio;

	public DisCornerType type
	{
		get
		{
			return GetValue(_type, "type");
		}
		set
		{
			SetProperty(ref _type, value, "type");
		}
	}

	public DisSlopeType sType
	{
		get
		{
			return GetValue(_sType, "sType");
		}
		set
		{
			SetProperty(ref _sType, value, "sType");
		}
	}

	public DisLayoutType lType
	{
		get
		{
			return GetValue(_lType, "lType");
		}
		set
		{
			SetProperty(ref _lType, value, "lType");
		}
	}

	public DistortionType dType
	{
		get
		{
			return GetValue(_dType, "dType");
		}
		set
		{
			SetProperty(ref _dType, value, "dType");
		}
	}

	public int Width
	{
		get
		{
			return GetValue(_cx, "Width");
		}
		set
		{
			SetProperty(ref _cx, value, "Width");
		}
	}

	public int Height
	{
		get
		{
			return GetValue(_cy, "Height");
		}
		set
		{
			SetProperty(ref _cy, value, "Height");
		}
	}

	public bool filterByColor
	{
		get
		{
			return GetValue(_filterByColor, "filterByColor");
		}
		set
		{
			SetProperty(ref _filterByColor, value, "filterByColor");
		}
	}

	public int blobColor
	{
		get
		{
			return GetValue(_blobColor, "blobColor");
		}
		set
		{
			SetProperty(ref _blobColor, value, "blobColor");
		}
	}

	public float minThreshold
	{
		get
		{
			return GetValue(_minThreshold, "minThreshold");
		}
		set
		{
			SetProperty(ref _minThreshold, value, "minThreshold");
		}
	}

	public float thresholdStep
	{
		get
		{
			return GetValue(_thresholdStep, "thresholdStep");
		}
		set
		{
			SetProperty(ref _thresholdStep, value, "thresholdStep");
		}
	}

	public float maxThreshold
	{
		get
		{
			return GetValue(_maxThreshold, "maxThreshold");
		}
		set
		{
			SetProperty(ref _maxThreshold, value, "maxThreshold");
		}
	}

	public bool ifDEBUG
	{
		get
		{
			return GetValue(_ifDEBUG, "ifDEBUG");
		}
		set
		{
			SetProperty(ref _ifDEBUG, value, "ifDEBUG");
		}
	}

	public float darkRatio
	{
		get
		{
			return GetValue(_darkRatio, "darkRatio");
		}
		set
		{
			SetProperty(ref _darkRatio, value, "darkRatio");
		}
	}

	public float contrastRatio
	{
		get
		{
			return GetValue(_contrastRatio, "contrastRatio");
		}
		set
		{
			SetProperty(ref _contrastRatio, value, "contrastRatio");
		}
	}

	public int bgRadius
	{
		get
		{
			return GetValue(_bgRadius, "bgRadius");
		}
		set
		{
			SetProperty(ref _bgRadius, value, "bgRadius");
		}
	}

	public float minDistBetweenBlobs
	{
		get
		{
			return GetValue(_minDistBetweenBlobs, "minDistBetweenBlobs");
		}
		set
		{
			SetProperty(ref _minDistBetweenBlobs, value, "minDistBetweenBlobs");
		}
	}

	public bool filterByArea
	{
		get
		{
			return GetValue(_filterByArea, "filterByArea");
		}
		set
		{
			SetProperty(ref _filterByArea, value, "filterByArea");
		}
	}

	public float minArea
	{
		get
		{
			return GetValue(_minArea, "minArea");
		}
		set
		{
			SetProperty(ref _minArea, value, "minArea");
		}
	}

	public float maxArea
	{
		get
		{
			return GetValue(_maxArea, "maxArea");
		}
		set
		{
			SetProperty(ref _maxArea, value, "maxArea");
		}
	}

	public int minRepeatability
	{
		get
		{
			return GetValue(_minRepeatability, "minRepeatability");
		}
		set
		{
			SetProperty(ref _minRepeatability, value, "minRepeatability");
		}
	}

	public bool filterByCircularity
	{
		get
		{
			return GetValue(_filterByCircularity, "filterByCircularity");
		}
		set
		{
			SetProperty(ref _filterByCircularity, value, "filterByCircularity");
		}
	}

	public float minCircularity
	{
		get
		{
			return GetValue(_minCircularity, "minCircularity");
		}
		set
		{
			SetProperty(ref _minCircularity, value, "minCircularity");
		}
	}

	public float maxCircularity
	{
		get
		{
			return GetValue(_maxCircularity, "maxCircularity");
		}
		set
		{
			SetProperty(ref _maxCircularity, value, "maxCircularity");
		}
	}

	public bool filterByConvexity
	{
		get
		{
			return GetValue(_filterByConvexity, "filterByConvexity");
		}
		set
		{
			SetProperty(ref _filterByConvexity, value, "filterByConvexity");
		}
	}

	public float minConvexity
	{
		get
		{
			return GetValue(_minConvexity, "minConvexity");
		}
		set
		{
			SetProperty(ref _minConvexity, value, "minConvexity");
		}
	}

	public float maxConvexity
	{
		get
		{
			return GetValue(_maxConvexity, "maxConvexity");
		}
		set
		{
			SetProperty(ref _maxConvexity, value, "maxConvexity");
		}
	}

	public bool filterByInertia
	{
		get
		{
			return GetValue(_filterByInertia, "filterByInertia");
		}
		set
		{
			SetProperty(ref _filterByInertia, value, "filterByInertia");
		}
	}

	public float minInertiaRatio
	{
		get
		{
			return GetValue(_minInertiaRatio, "minInertiaRatio");
		}
		set
		{
			SetProperty(ref _minInertiaRatio, value, "minInertiaRatio");
		}
	}

	public float maxInertiaRatio
	{
		get
		{
			return GetValue(_maxInertiaRatio, "maxInertiaRatio");
		}
		set
		{
			SetProperty(ref _maxInertiaRatio, value, "maxInertiaRatio");
		}
	}
}
