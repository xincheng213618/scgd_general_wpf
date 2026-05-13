namespace MQTTMessageLib.Algorithm.LED;

public class DeviceParamLedCheck : ParamDicBase
{
	private int _CheckChannel;

	private int _Isguding;

	private int _Gudingrid;

	private int _Lunkuomianji;

	private int _PointNum;

	private float _Hegexishu;

	private int _Erzhihuapiancha;

	private int _BinaryCorret;

	private int _Boundry;

	private bool _IsuseLocalRdPoint;

	private int _Picwid;

	private int _Pichig;

	private double[] _LengthCheck;

	private double[] _LengthRange;

	private float[] _LocalRdMark;

	public int CheckChannel
	{
		get
		{
			return GetValue(_CheckChannel, "CheckChannel");
		}
		set
		{
			SetProperty(ref _CheckChannel, value, "CheckChannel");
		}
	}

	public int Isguding
	{
		get
		{
			return GetValue(_Isguding, "Isguding");
		}
		set
		{
			SetProperty(ref _Isguding, value, "Isguding");
		}
	}

	public int Gudingrid
	{
		get
		{
			return GetValue(_Gudingrid, "Gudingrid");
		}
		set
		{
			SetProperty(ref _Gudingrid, value, "Gudingrid");
		}
	}

	public int Lunkuomianji
	{
		get
		{
			return GetValue(_Lunkuomianji, "Lunkuomianji");
		}
		set
		{
			SetProperty(ref _Lunkuomianji, value, "Lunkuomianji");
		}
	}

	public int PointNum
	{
		get
		{
			return GetValue(_PointNum, "PointNum");
		}
		set
		{
			SetProperty(ref _PointNum, value, "PointNum");
		}
	}

	public float Hegexishu
	{
		get
		{
			return GetValue(_Hegexishu, "Hegexishu");
		}
		set
		{
			SetProperty(ref _Hegexishu, value, "Hegexishu");
		}
	}

	public int Erzhihuapiancha
	{
		get
		{
			return GetValue(_Erzhihuapiancha, "Erzhihuapiancha");
		}
		set
		{
			SetProperty(ref _Erzhihuapiancha, value, "Erzhihuapiancha");
		}
	}

	public int BinaryCorret
	{
		get
		{
			return GetValue(_BinaryCorret, "BinaryCorret");
		}
		set
		{
			SetProperty(ref _BinaryCorret, value, "BinaryCorret");
		}
	}

	public int Boundry
	{
		get
		{
			return GetValue(_Boundry, "Boundry");
		}
		set
		{
			SetProperty(ref _Boundry, value, "Boundry");
		}
	}

	public bool IsuseLocalRdPoint
	{
		get
		{
			return GetValue(_IsuseLocalRdPoint, "IsuseLocalRdPoint");
		}
		set
		{
			SetProperty(ref _IsuseLocalRdPoint, value, "IsuseLocalRdPoint");
		}
	}

	public int Picwid
	{
		get
		{
			return GetValue(_Picwid, "Picwid");
		}
		set
		{
			SetProperty(ref _Picwid, value, "Picwid");
		}
	}

	public int Pichig
	{
		get
		{
			return GetValue(_Pichig, "Pichig");
		}
		set
		{
			SetProperty(ref _Pichig, value, "Pichig");
		}
	}

	public double[] LengthCheck
	{
		get
		{
			return GetValue(_LengthCheck, "LengthCheck");
		}
		set
		{
			SetProperty(ref _LengthCheck, value, "LengthCheck");
		}
	}

	public double[] LengthRange
	{
		get
		{
			return GetValue(_LengthRange, "LengthRange");
		}
		set
		{
			SetProperty(ref _LengthRange, value, "LengthRange");
		}
	}

	public float[] LocalRdMark
	{
		get
		{
			return GetValue(_LocalRdMark, "LocalRdMark");
		}
		set
		{
			SetProperty(ref _LocalRdMark, value, "LocalRdMark");
		}
	}
}
