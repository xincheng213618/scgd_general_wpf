using System.Collections.Generic;
using System.Linq;

namespace CVCommCore.CVAlgorithm;

public class POIPoint : POIPointOnly
{
	public POIReviseItem ReviseItem { get; set; }

	public POIPoint()
	{
		ReviseItem = POIReviseItem.Disable();
	}

	public POIPoint(int? id, string name, POIPointTypes pointType, POIPointPosition item, float Width, float Height)
		: this()
	{
		base.Id = id;
		base.Pid = -1;
		base.Name = name;
		base.PointType = pointType;
		base.PixelX = item.PixelX;
		base.PixelY = item.PixelY;
		base.Width = Width;
		base.Height = Height;
	}

	public POIPoint(int? id, string name, POIPointTypes pointType, POIBasePoint item)
		: this()
	{
		base.Id = id;
		base.Pid = -1;
		base.Name = name;
		base.PointType = pointType;
		base.PixelX = item.PixelX;
		base.PixelY = item.PixelY;
		base.Width = item.Width;
		base.Height = item.Height;
	}

	public POIPoint(int? id, int pid, string name, POIPointTypes pointType, float pixelX, float pixelY, float width, float height)
	{
		ReviseItem = POIReviseItem.Disable();
		base.Id = id;
		base.Pid = pid;
		base.Name = name;
		base.PointType = pointType;
		base.PixelX = pixelX;
		base.PixelY = pixelY;
		base.Width = width;
		base.Height = height;
	}

	public POIPoint(POIPointOnly item)
		: this()
	{
		base.Id = item.Id;
		base.Pid = item.Pid;
		base.Name = item.Name;
		base.PointType = item.PointType;
		base.PixelX = item.PixelX;
		base.PixelY = item.PixelY;
		base.Width = item.Width;
		base.Height = item.Height;
	}

	private void setRevise(IEnumerable<POIFileReviseItem> filteredNumbers)
	{
		if (filteredNumbers != null && filteredNumbers.Count() > 0)
		{
			POIFileReviseItem src = filteredNumbers.ElementAt(0);
			ReviseItem = new POIReviseItem(src);
		}
		else
		{
			ReviseItem = POIReviseItem.Disable();
		}
	}

	public void FilterRevise(List<POIFileReviseItem> fixData)
	{
		if (string.IsNullOrEmpty(base.Name))
		{
			setRevise(fixData.Where((POIFileReviseItem f) => f.Id == base.Id));
		}
		else
		{
			setRevise(fixData.Where((POIFileReviseItem f) => f.Name == base.Name));
		}
	}
}
