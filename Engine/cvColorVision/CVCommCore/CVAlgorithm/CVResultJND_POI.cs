namespace CVCommCore.CVAlgorithm;

public class CVResultJND_POI
{
	public POIPointOnly POI { get; set; }

	public CVResultJND Jnd { get; set; }

	public CVResultJND_POI()
	{
	}

	public CVResultJND_POI(POIPointOnly roi, double v_jnd, double h_jnd)
	{
		POI = roi;
		Jnd = new CVResultJND
		{
			v_jnd = v_jnd,
			h_jnd = h_jnd
		};
	}

	public CVResultJND_POI(POIPointOnly roi, CVResultJND jnd)
	{
		POI = roi;
		Jnd = jnd;
	}
}
