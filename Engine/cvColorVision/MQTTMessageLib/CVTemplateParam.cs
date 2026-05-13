using Newtonsoft.Json;

namespace MQTTMessageLib;

public class CVTemplateParam
{
	public int ID { get; set; }

	public string Name { get; set; }

	[JsonIgnore]
	public bool IsValid
	{
		get
		{
			if (ID <= 0)
			{
				return !string.IsNullOrWhiteSpace(Name);
			}
			return true;
		}
	}

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
