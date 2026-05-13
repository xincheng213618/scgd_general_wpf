namespace CVCommCore;

public class CVWinPlatform : CVSingleton<CVWinPlatform>
{
	private string platform;

	public string Platform => platform;

	public void Init(string plarform)
	{
		platform = plarform;
	}
}
