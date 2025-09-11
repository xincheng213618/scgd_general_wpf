namespace FlowEngineLib.Camera;

public class ChannelData
{
	private float _Temp;

	private int _FWPort;

	public string TypeCode { get; set; }

	public float Temp
	{
		get
		{
			return _Temp;
		}
		set
		{
			_Temp = value;
		}
	}

	public int FWPort
	{
		get
		{
			return _FWPort;
		}
		set
		{
			_FWPort = value;
		}
	}

	public ChannelData(string typeCode, int FWPort, float Temp)
	{
		TypeCode = typeCode;
		_FWPort = FWPort;
		_Temp = Temp;
	}
}
