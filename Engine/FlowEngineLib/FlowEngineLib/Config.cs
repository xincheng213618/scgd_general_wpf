using System.IO;
using log4net.Config;

namespace FlowEngineLib;

public static class Config
{
	public static void init()
	{
		init("log4net.config");
	}

	public static void init(string fileName)
	{
		XmlConfigurator.ConfigureAndWatch(new FileInfo(fileName));
	}
}
