using System;

namespace CVCommCore.CVAlgorithm;

public class POIPointPosition
{
	public float PixelX { get; set; }

	public float PixelY { get; set; }

	public int PixelXAsInt => Convert.ToInt32(PixelX);

	public int PixelYAsInt => Convert.ToInt32(PixelY);

	public POIPointPosition()
	{
	}

	public POIPointPosition(POIPointPosition item)
		: this(item.PixelX, item.PixelY)
	{
	}

	public POIPointPosition(double pixelX, double pixelY)
		: this(Convert.ToSingle(pixelX), Convert.ToSingle(pixelY))
	{
	}

	public POIPointPosition(float pixelX, float pixelY)
	{
		PixelX = pixelX;
		PixelY = pixelY;
	}
}
