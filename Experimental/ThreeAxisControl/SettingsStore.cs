using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace ThreeAxisControl
{
    internal sealed class SettingsStore
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(ControlSettings));
        private readonly string path;

        public SettingsStore(string path = null)
        {
            this.path = Path.GetFullPath(path ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreeAxisControl", "settings.xml"));
        }

        public ControlSettings Load()
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                    return (ControlSettings)Serializer.Deserialize(reader) ?? new ControlSettings();
            }
            catch (FileNotFoundException)
            {
                return new ControlSettings();
            }
            catch (DirectoryNotFoundException)
            {
                return new ControlSettings();
            }
        }

        public void Save(ControlSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, $"settings-{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    Serializer.Serialize(stream, settings);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
    }

    // XmlSerializer requires a public type. Null means a field was not saved.
    public sealed class ControlSettings
    {
        public string XyPort { get; set; }
        public string XyBaud { get; set; }
        public string ZLocalIp { get; set; }
        public string ZControllerIp { get; set; }
        public string ZUdp { get; set; }
        public string ZChannel { get; set; }
        public string XStep { get; set; }
        public string YStep { get; set; }
        public string ZStep { get; set; }
        public string XSpeed { get; set; }
        public string YSpeed { get; set; }
        public string ZSpeed { get; set; }
        public string XStartSpeed { get; set; }
        public string YStartSpeed { get; set; }
        public string ZStartSpeed { get; set; }
        public string XAcceleration { get; set; }
        public string YAcceleration { get; set; }
        public string ZAcceleration { get; set; }
        public string XMinPosition { get; set; }
        public string XMaxPosition { get; set; }
        public string YMinPosition { get; set; }
        public string YMaxPosition { get; set; }
        public string ZMinPosition { get; set; }
        public string ZMaxPosition { get; set; }
        public bool XyAutoConnect { get; set; }
        public bool ZAutoConnect { get; set; }
    }
}
