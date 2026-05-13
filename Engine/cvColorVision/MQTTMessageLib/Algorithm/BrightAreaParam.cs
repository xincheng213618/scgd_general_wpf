namespace MQTTMessageLib.Algorithm;

public struct BrightAreaParam
{
	public bool bBinarize { get; set; }

	public int nBinarizeThresh { get; set; }

	public bool bBlur { get; set; }

	public int nblur_size { get; set; }

	public bool bRoi { get; set; }

	public int nleft { get; set; }

	public int nright { get; set; }

	public int ntop { get; set; }

	public int nbottom { get; set; }

	public bool bErode { get; set; }

	public int nerode_size { get; set; }

	public bool bDilate { get; set; }

	public int ndilate_size { get; set; }

	public bool bFilterRect { get; set; }

	public int Widht { get; set; }

	public int Height { get; set; }

	public bool bFilterArea { get; set; }

	public int nMax_area { get; set; }

	public int nMin_area { get; set; }
}
