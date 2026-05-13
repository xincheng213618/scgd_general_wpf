using System;

namespace MQTTMessageLib;

public class NodeToken
{
	public string AccessToken { get; set; }

	public string RefreshToken { get; set; }

	public long Timestamp { get; set; }

	public int Expires { get; set; }

	public NodeToken(int expires)
	{
		AccessToken = Guid.NewGuid().ToString();
		RefreshToken = Guid.NewGuid().ToString();
		Timestamp = DateTime.Now.Ticks;
		Expires = expires;
	}

	public bool IsExpired()
	{
		return false;
	}

	public void Refresh()
	{
		AccessToken = Guid.NewGuid().ToString();
		Timestamp = DateTime.Now.Ticks;
	}

	public void Refresh(int expires)
	{
		Expires = expires;
		Refresh();
	}
}
