namespace FlowEngineLib.Node.Global;

public class SPDeviceControlData : DeviceControlData
{
	public CVTemplateParam TemplateParam { get; set; }

	public SPDeviceControlData()
	{
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = null
		};
	}
}
