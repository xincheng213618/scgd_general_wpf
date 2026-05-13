using System;

namespace CVCommCore.CVAlgorithm;

public class POIPointOnly : POIBasePoint
{
	public int? Id { get; set; }

	public int Pid { get; set; }

	public string Name { get; set; }

	public POIPointTypes PointType { get; set; }

	public double Radius => base.Width / 2f;

	public PointInt Center
	{
		get
		{
			if (PointType == POIPointTypes.LTRect)
			{
				return new PointInt(Convert.ToInt32(base.PixelX + base.Width / 2f), Convert.ToInt32(base.PixelY + base.Height / 2f));
			}
			return new PointInt(base.PixelXAsInt, base.PixelYAsInt);
		}
	}

	public PointInt LeftTop
	{
		get
		{
			if (PointType == POIPointTypes.LTRect)
			{
				return new PointInt(base.PixelXAsInt, base.PixelYAsInt);
			}
			return new PointInt(Convert.ToInt32(base.PixelX - base.Width / 2f), Convert.ToInt32(base.PixelY - base.Height / 2f));
		}
	}

	public PointInt RightTop
	{
		get
		{
			if (PointType == POIPointTypes.LTRect)
			{
				return new PointInt(Convert.ToInt32(base.PixelX + base.Width), Convert.ToInt32(base.PixelY));
			}
			return new PointInt(Convert.ToInt32(base.PixelX + base.Width / 2f), Convert.ToInt32(base.PixelY - base.Height / 2f));
		}
	}

	public PointInt RightBottom
	{
		get
		{
			if (PointType == POIPointTypes.LTRect)
			{
				return new PointInt(Convert.ToInt32(base.PixelX + base.Width), Convert.ToInt32(base.PixelY + base.Height));
			}
			return new PointInt(Convert.ToInt32(base.PixelX + base.Width / 2f), Convert.ToInt32(base.PixelY + base.Height / 2f));
		}
	}

	public PointInt LeftBottom
	{
		get
		{
			if (PointType == POIPointTypes.LTRect)
			{
				return new PointInt(Convert.ToInt32(base.PixelX), Convert.ToInt32(base.PixelY + base.Height));
			}
			return new PointInt(Convert.ToInt32(base.PixelX - base.Width / 2f), Convert.ToInt32(base.PixelY + base.Height / 2f));
		}
	}

	public int Width_Radius
	{
		get
		{
			if (PointType == POIPointTypes.Circle)
			{
				return Convert.ToInt32(base.Width / 2f);
			}
			return base.WidthAsInt;
		}
	}

	public POIPointOnly()
	{
	}

	public POIPointOnly(int? id, int pid, string name, POIPointTypes pointType, float pixelX, float pixelY, float width, float height)
		: base(pixelX, pixelY, width, height)
	{
		Id = id;
		Pid = pid;
		Name = name;
		PointType = pointType;
	}

	public POIPointOnly(int? id, string name, POIPointTypes pointType, POIPointPosition pos, float width, float height)
		: this(id, -1, name, pointType, pos.PixelX, pos.PixelY, width, height)
	{
	}

	public void GetRectPoint(ref PointInt leftTop, ref PointInt rightBottom, int maxX, int maxY)
	{
		int num = (int)Math.Round((double)base.Width / 2.0, MidpointRounding.AwayFromZero);
		int num2 = (int)Math.Round((double)base.Height / 2.0, MidpointRounding.AwayFromZero);
		float num3 = Math.Min(Math.Max(base.PixelX - (float)num, 0f), maxX);
		float num4 = Math.Min(Math.Max(base.PixelY - (float)num2, 0f), maxY);
		float num5 = Math.Min(Math.Max(num3 + base.Width, 0f), maxX);
		float num6 = Math.Min(Math.Max(num4 + base.Height, 0f), maxY);
		if (num5 <= num3)
		{
			num5 = num3 + 1f;
		}
		if (num6 <= num4)
		{
			num6 = num4 + 1f;
		}
		leftTop.X = Convert.ToInt32(num3);
		leftTop.Y = Convert.ToInt32(num4);
		rightBottom.X = Convert.ToInt32(num5);
		rightBottom.Y = Convert.ToInt32(num6);
	}

	public bool IsValid()
	{
		bool flag = PointType == POIPointTypes.Circle || PointType == POIPointTypes.Rect;
		if (flag)
		{
			flag = base.Width > 0f && base.Height > 0f;
		}
		return flag;
	}

	public bool IsRect()
	{
		if (PointType != POIPointTypes.Rect)
		{
			return PointType == POIPointTypes.LTRect;
		}
		return true;
	}
}
