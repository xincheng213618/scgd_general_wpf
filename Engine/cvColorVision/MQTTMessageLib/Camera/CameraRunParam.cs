using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.Camera;

public abstract class CameraRunParam : ParamDicBase
{
	private bool _EnableFocus;

	private int _Focus;

	private int _Aperture;

	private uint _AvgCount;

	private float _Gain;

	public bool EnableFocus
	{
		get
		{
			return GetValue(_EnableFocus, "EnableFocus");
		}
		set
		{
			SetProperty(ref _EnableFocus, value, "EnableFocus");
		}
	}

	public int Focus
	{
		get
		{
			return GetValue(_Focus, "Focus");
		}
		set
		{
			SetProperty(ref _Focus, value, "Focus");
		}
	}

	public int Aperture
	{
		get
		{
			return GetValue(_Aperture, "Aperture");
		}
		set
		{
			SetProperty(ref _Aperture, value, "Aperture");
		}
	}

	public uint AvgCount
	{
		get
		{
			return GetValue(_AvgCount, "AvgCount");
		}
		set
		{
			SetProperty(ref _AvgCount, value, "AvgCount");
		}
	}

	public float Gain
	{
		get
		{
			return GetValue(_Gain, "Gain");
		}
		set
		{
			SetProperty(ref _Gain, value, "Gain");
		}
	}

	public abstract float[] ExportExpTime();
}
