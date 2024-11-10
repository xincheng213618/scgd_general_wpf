#pragma warning disable CA1051

using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace CVImageChannelLib;

public class MMFEndpointProxy<T> : CVEndpointProxy where T : ISerializer<T>, new()
{
	protected MemoryMappedFile memoryMappedFile;

	protected MemoryMappedViewStream memoryMappedViewStream;

	public string Name { get; protected set; }

	public MMFEndpointProxy(MemoryMappedFile memoryMappedFile)
		: this(memoryMappedFile.CreateViewStream())
	{
		this.memoryMappedFile = memoryMappedFile;
	}

	public MMFEndpointProxy(MemoryMappedViewStream stream)
		: base(stream)
	{
		memoryMappedViewStream = stream;
	}

	public Stream GetStream()
	{
		return memoryMappedViewStream;
	}

	public void Publish(T val)
	{
		memoryMappedViewStream.Position = 0L;
		val.Serialize(writer);
	}

	public T Subscribe()
	{
		memoryMappedViewStream.Position = 0L;
		T result = new T();
		result.Deserialize(reader);
		return result;
	}

	public override void Dispose()
	{
		memoryMappedViewStream.Dispose();
		memoryMappedFile.Dispose();
		GC.SuppressFinalize(this);
		base.Dispose();
	}
}
