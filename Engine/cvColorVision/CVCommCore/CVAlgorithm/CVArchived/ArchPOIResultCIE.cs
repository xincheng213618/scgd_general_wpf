namespace CVCommCore.CVAlgorithm.CVArchived;

public class ArchPOIResultCIE<T>
{
	public ArchPOIPoint Point { get; set; }

	public T Data { get; set; }

	public ArchPOIResultCIE(ArchPOIPoint point, T data)
	{
		Point = point;
		Data = data;
	}
}
