using System.IO;

namespace CVImageChannelLib;

public interface ISerializer<T> where T : ISerializer<T>
{
	void Serialize(BinaryWriter writer);

	void Deserialize(BinaryReader reader);
}
