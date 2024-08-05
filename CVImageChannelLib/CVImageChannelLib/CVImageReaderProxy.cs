using System;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace CVImageChannelLib;

public abstract class CVImageReaderProxy : IDisposable
{
	public abstract WriteableBitmap Subscribe();

	public virtual void Dispose()
	{
	}
}
