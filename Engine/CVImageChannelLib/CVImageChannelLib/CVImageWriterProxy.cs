using System;
using System.Collections.Generic;

namespace CVImageChannelLib;

public abstract class CVImageWriterProxy : IDisposable
{
	public abstract void Publish(CVImagePacket packet);

	public abstract bool Setup(Dictionary<string, object> cfg);

	public virtual void Dispose()
	{
        GC.SuppressFinalize(this);
    }
}
