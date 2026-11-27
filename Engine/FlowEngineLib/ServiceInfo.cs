namespace FlowEngineLib;

public class ServiceInfo
{
	private string _ServiceCode;

	public string ServiceType { get; }

	public string ServiceCode => _ServiceCode;

	public string Token { get; private set; }

	public ServiceInfo(string serviceType)
		: this(serviceType, string.Empty)
	{
	}

	public ServiceInfo(string serviceType, string serviceCode)
	{
		ServiceType = serviceType;
		_ServiceCode = serviceCode;
	}

	public void SetCode(string serviceCode)
	{
		_ServiceCode = serviceCode;
	}

	public virtual string GetKey() => GetKey(ServiceType, _ServiceCode);

	internal static string GetKey(string serviceType, string serviceCode)
	{
		if (!string.IsNullOrEmpty(serviceCode))
		{
			return $"{serviceType}.{serviceCode}";
		}
		return string.Empty;
	}

	public virtual void Update(ServiceInfo service)
	{
		_ServiceCode = service.ServiceCode;
	}

	public void Update(string serviceCode, string token)
	{
		if (!string.IsNullOrEmpty(serviceCode))
		{
			_ServiceCode = serviceCode;
		}
		Token = token;
	}
}
