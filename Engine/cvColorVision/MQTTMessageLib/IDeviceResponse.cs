namespace MQTTMessageLib;

public interface IDeviceResponse
{
	bool IsAlwaysPersistence { get; set; }

	int Code { get; }

	string Desc { get; }

	bool IsOk();

	bool IsFailed();

	bool IsPending();

	bool IsUnauthorized();

	bool IsSended();

	void SetSended(bool value);

	void ToPending();

	void ToFailed(string desc);
}
