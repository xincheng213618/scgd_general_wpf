using Newtonsoft.Json;

namespace MQTTMessageLib;

public class CVBaseDeviceResponse : IDeviceResponse
{
	public const int CODE_SUCCESS = 0;

	public const int CODE_HTTP_SUCCESS = 200;

	public const int CODE_WARN_DB = 1;

	public const int CODE_PENDING = 102;

	public const int CODE_FAILED = -1;

	public const int CODE_HTTP_FAILED = 400;

	public const int CODE_ERR_NEED_OPEN = -2;

	public const int CODE_ERR_TOKEN = -10;

	public const int CODE_ERR_UNAUTHORIZED = -401;

	public const int CODE_ERR_UNINITIALIZED = -500;

	[JsonIgnore]
	private bool _IsSended;

	public int Code { get; set; }

	public string Desc { get; set; }

	public string DeviceResultCode { get; set; }

	public bool IsAlwaysPersistence { get; set; }

	public bool IsUnauthorized()
	{
		return Code == -401;
	}

	public bool IsOk()
	{
		return Code == 0;
	}

	public bool IsPending()
	{
		return Code == 102;
	}

	public bool IsFailed()
	{
		return Code == -1;
	}

	public bool IsSended()
	{
		return _IsSended;
	}

	public void SetSended(bool value)
	{
		_IsSended = value;
	}

	public CVBaseDeviceResponse(int code, string desc)
	{
		Code = code;
		Desc = desc;
		_IsSended = false;
		IsAlwaysPersistence = false;
	}

	public CVBaseDeviceResponse(CVBaseDeviceResponse status)
	{
		Code = status.Code;
		Desc = status.Desc;
		_IsSended = false;
		IsAlwaysPersistence = false;
	}

	public void ToSuccess()
	{
		Code = 0;
		Desc = "ok";
		_IsSended = false;
	}

	public static CVBaseDeviceResponse Success()
	{
		return new CVBaseDeviceResponse(0, "ok");
	}

	public static CVBaseDeviceResponse Failed()
	{
		return new CVBaseDeviceResponse(-1, "Failed");
	}

	public static CVBaseDeviceResponse Failed(string desc)
	{
		return new CVBaseDeviceResponse(-1, desc);
	}

	public static CVBaseDeviceResponse Pending()
	{
		return new CVBaseDeviceResponse(102, "Pending");
	}

	public static CVBaseDeviceResponse Unauthorized()
	{
		return new CVBaseDeviceResponse(-401, "Unauthorized");
	}

	public static CVBaseDeviceResponse Uninitialized()
	{
		return new CVBaseDeviceResponse(-500, "Uninitialized");
	}

	public void ToFailed(string deviceResultCode, string desc)
	{
		Code = -1;
		Desc = desc;
		_IsSended = false;
		DeviceResultCode = deviceResultCode;
	}

	public virtual void ToFailed(string desc)
	{
		ToFailed(null, desc);
	}

	public void ToPending()
	{
		Code = 102;
		Desc = "Pending";
		DeviceResultCode = null;
	}
}
