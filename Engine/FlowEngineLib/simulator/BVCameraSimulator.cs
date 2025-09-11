namespace FlowEngineLib.simulator;

internal class BVCameraSimulator : BaseSimulator
{
	public BVCameraSimulator()
		: base("相机模拟器", "CAMERA", "DEV01")
	{
		serverRespEventName = "GetData";
	}
}
