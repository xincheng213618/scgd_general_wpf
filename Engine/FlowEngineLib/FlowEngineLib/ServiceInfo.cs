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

	public virtual string GetKey()
	{
		if (!string.IsNullOrEmpty(_ServiceCode))
		{
			return $"{ServiceType}.{_ServiceCode}";
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
