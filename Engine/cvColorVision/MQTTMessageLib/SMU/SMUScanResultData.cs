namespace MQTTMessageLib.SMU;

public class SMUScanResultData
{
	public double[] VList { get; set; }

	public double[] IList { get; set; }

	public double[] ScanList { get; set; }

	public SMUScanResultData(double[] scan, double[] v, double[] i)
	{
		VList = v;
		IList = i;
		ScanList = scan;
	}
}
