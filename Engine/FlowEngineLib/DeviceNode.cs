using FlowEngineLib.Base;

namespace FlowEngineLib;

public class DeviceNode
{
	private string _DeviceCode;

	private CVBaseServerNode OwnerNode;

	public string DeviceID { get; }

	public string DeviceType
	{
		get
		{
			string result = string.Empty;
			if (Service != null)
			{
				result = Service.ServiceType;
			}
			return result;
		}
	}

	public string DeviceCode => _DeviceCode;

	public ServiceInfo Service { get; }

	public bool IsAnonymous { get; private set; }

	public DeviceNode(string id, string type)
		: this(id, string.Empty, new ServiceInfo(type))
	{
	}

	public DeviceNode(string id, string code, ServiceInfo service)
	{
		DeviceID = id;
		_DeviceCode = code;
		Service = service;
		IsAnonymous = string.IsNullOrEmpty(GetKey());
	}

	public DeviceNode(CVBaseServerNode node)
		: this(node.NodeID, node.DeviceCode, new ServiceInfo(node.NodeType, node.NodeName))
	{
		OwnerNode = node;
	}

	public void SetCode(string deviceCode, string serviceCode)
	{
		_DeviceCode = deviceCode;
		if (Service != null)
		{
			Service.SetCode(serviceCode);
		}
	}

	public virtual string GetKey()
	{
		string text = string.Empty;
		if (Service != null)
		{
			text = Service.GetKey();
		}
		if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(DeviceCode))
		{
			return $"{text}.{DeviceCode}";
		}
		return string.Empty;
	}

	public void Update(DeviceNode deviceNode)
	{
		_DeviceCode = deviceNode.DeviceCode;
		Service.Update(deviceNode.Service);
	}

	public void Update(MQTTDeviceInfo deviceNode)
	{
		if (IsAnonymous)
		{
			_DeviceCode = deviceNode.DeviceCode;
			Service.Update(deviceNode.Service.ServiceCode, deviceNode.Service.Token);
		}
		else
		{
			Service.Update(string.Empty, deviceNode.Service.Token);
		}
		UpdateOwnerNode();
	}

	public void UpdateOwnerNode()
	{
		if (OwnerNode != null)
		{
			if (IsAnonymous)
			{
				OwnerNode.DeviceCode = _DeviceCode;
				OwnerNode.NodeName = Service.ServiceCode;
			}
			OwnerNode.Token = Service.Token;
		}
	}
}
