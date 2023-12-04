using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace ColorVision.Util.Helper
{

    public static class YamlHelper
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





    public static class JsonHelper
    {

        public static JsonSerializerSettings JsonSerializerSettings { get; set; } = new JsonSerializerSettings { Formatting = Formatting.Indented };
        public static T? ReadConfig<T>(string fileName)
        {
            if (File.Exists(fileName))
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(fileName), JsonSerializerSettings);
            else
            {
                T t = (T)Activator.CreateInstance(typeof(T));
                WriteConfig(fileName, t);
                return t;
            }
        }


        public static void WriteConfig<T>(string fileName, T? t)
        {
            string DirectoryName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(DirectoryName) && !Directory.Exists(DirectoryName))
                Directory.CreateDirectory(DirectoryName);
            string jsonString = JsonConvert.SerializeObject(t, JsonSerializerSettings);
            File.WriteAllText(fileName, jsonString);
        }
    }
}
