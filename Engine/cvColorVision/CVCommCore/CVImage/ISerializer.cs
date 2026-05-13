using System.IO;

namespace CVCommCore.CVImage;

public interface ISerializer<T> where T : ISerializer<T>
{
	void Serialize(BinaryWriter writer);

	void Deserialize(BinaryReader reader);
}
