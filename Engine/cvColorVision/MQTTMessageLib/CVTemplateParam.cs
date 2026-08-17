namespace MQTTMessageLib;

public class CVTemplateParam
{
	public int ID { get; set; }

	public string Name { get; set; }

	public CVTemplateParam()
		: this(string.Empty)
	{
	}

	public CVTemplateParam(string name)
	{
		ID = -1;
		Name = name;
	}
}
