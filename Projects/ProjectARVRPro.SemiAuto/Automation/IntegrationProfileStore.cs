using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ProjectARVRPro.SemiAuto.Automation
{
    public static class IntegrationProfileStore
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static IntegrationProfile Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("配置文件路径不能为空。", nameof(filePath));
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            IntegrationProfile profile = Serializer.Deserialize<IntegrationProfile>(json);
            if (profile == null)
                throw new InvalidDataException("配置文件内容为空。 ");
            if (profile.Mappings == null)
                profile.Mappings = new List<PgActionMapping>();
            profile.Validate();
            return profile;
        }

        public static void Save(string filePath, IntegrationProfile profile)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("配置文件路径不能为空。", nameof(filePath));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            profile.Validate();

            string fullPath = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, Serializer.Serialize(profile), new UTF8Encoding(false));
        }
    }
}
