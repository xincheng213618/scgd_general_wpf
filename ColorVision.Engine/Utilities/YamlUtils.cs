using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ColorVision.Engine.Utilities
{
    public static class YamlUtils
    {
        public static T? ReadYaml<T>(string fileName)
        {
            if (File.Exists(fileName))
            {
                var deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
                return deserializer.Deserialize<T>(File.ReadAllText(fileName));
            }
            else
            {
                T t = (T)Activator.CreateInstance(typeof(T));
                return t;
            }
        }

        public static void SaveYaml<T>(string fileName, T t)
        {
            string DirectoryName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(DirectoryName) && !Directory.Exists(DirectoryName))
                Directory.CreateDirectory(DirectoryName);


            var serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
            var yamlString = serializer.Serialize(t);

            File.WriteAllText(fileName, yamlString);
        }
    }

}
