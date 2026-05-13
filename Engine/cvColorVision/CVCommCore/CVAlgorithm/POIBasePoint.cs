using System;

namespace CVCommCore.CVAlgorithm;

public class POIBasePoint : POIPointPosition
{
	public float Width { get; set; }

	public float Height { get; set; }

	public int WidthAsInt => Convert.ToInt32(Width);

	public int HeightAsInt => Convert.ToInt32(Height);

	public POIBasePoint()
	{
	}

	public POIBasePoint(POIBasePoint item)
		: this(item.PixelX, item.PixelY, item.Width, item.Height)
	{
	}

	public POIBasePoint(float pixelX, float pixelY, float width, float height)
		: base(pixelX, pixelY)
	{
		Width = width;
		Height = height;
	}
}
