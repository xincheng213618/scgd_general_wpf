using System.Collections.Generic;

namespace FlowEngineLib;

public interface FlowEngineAPI
{
	bool IsReady { get; }

	bool IsRunning { get; }

	void LoadFromFile(string strFileName);

	void LoadFromFile(string strFileName, List<MQTTServiceInfo> services);

	void LoadFromBase64(string base64Data, bool waitReady = false);

	string GetStartNodeName();

	void StartNode(string serialNumber, List<MQTTServiceInfo> services);

	void StartNode(string name, string serialNumber, List<MQTTServiceInfo> services);

	void StopNode(string serialNumber);

	void StopNode(string name, string serialNumber);
}
