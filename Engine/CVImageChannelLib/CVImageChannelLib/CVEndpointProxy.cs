#pragma warning disable CA1051
using System;
using System.IO;

namespace CVImageChannelLib;

public abstract class CVEndpointProxy : IDisposable
{
	protected BinaryWriter writer;

	protected BinaryReader reader;

	public CVEndpointProxy(Stream stream)
	{
		writer = new BinaryWriter(stream);
		reader = new BinaryReader(stream);
	}

	public virtual void Dispose()
	{
		writer.Dispose();
		reader.Dispose();
        GC.SuppressFinalize(this);
    }
}
