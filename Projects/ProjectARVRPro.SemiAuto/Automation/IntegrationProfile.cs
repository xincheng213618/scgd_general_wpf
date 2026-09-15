using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ProjectARVRPro.SemiAuto.Automation
{
    public sealed class IntegrationProfile
    {
        public string ArvrHost { get; set; } = "127.0.0.1";

        public int ArvrPort { get; set; } = 6666;

        public string PgHost { get; set; } = "192.168.200.200";

        public int PgPort { get; set; } = 40009;

        public int NetworkNumber { get; set; } = 255;

        public string Channel { get; set; } = "01";

        public int PgResponseTimeoutSeconds { get; set; } = 30;

        public int HeartbeatSeconds { get; set; } = 5;

        public bool AutoExecuteMappedPgCommand { get; set; }

        public bool ConfirmArvrAfterPgSuccess { get; set; } = true;

        public List<PgActionMapping> Mappings { get; set; } = new List<PgActionMapping>();

        public PgActionMapping FindMapping(string eventName, string arvrTestType)
        {
            return (Mappings ?? new List<PgActionMapping>()).FirstOrDefault(mapping =>
                mapping != null &&
                mapping.Enabled &&
                string.Equals(mapping.EventName == null ? string.Empty : mapping.EventName.Trim(), eventName, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(mapping.ArvrTestType == null ? string.Empty : mapping.ArvrTestType.Trim(), "*", StringComparison.Ordinal) ||
                 string.Equals(mapping.ArvrTestType == null ? string.Empty : mapping.ArvrTestType.Trim(), arvrTestType ?? string.Empty, StringComparison.OrdinalIgnoreCase)));
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ArvrHost))
                throw new InvalidDataException("ARVR Host 不能为空。 ");
            ValidatePort(ArvrPort, "ARVR Port");
            if (string.IsNullOrWhiteSpace(PgHost))
                throw new InvalidDataException("PG Host 不能为空。 ");
            ValidatePort(PgPort, "PG Port");
            if (NetworkNumber < 0 || NetworkNumber > 255)
                throw new InvalidDataException("Network Number 必须在 0 到 255 之间。 ");
            int channelNumber;
            if (!int.TryParse(Channel, NumberStyles.Integer, CultureInfo.InvariantCulture, out channelNumber) || channelNumber < 1 || channelNumber > 54)
                throw new InvalidDataException("PG Channel 必须在 01 到 54 之间。 ");
            if (PgResponseTimeoutSeconds <= 0)
                throw new InvalidDataException("PG 响应超时必须大于 0 秒。 ");
            if (HeartbeatSeconds < 0)
                throw new InvalidDataException("心跳间隔不能小于 0；设为 0 表示禁用心跳。 ");

            foreach (PgActionMapping mapping in Mappings ?? new List<PgActionMapping>())
            {
                if (mapping == null)
                    continue;
                if (string.IsNullOrWhiteSpace(mapping.EventName))
                    throw new InvalidDataException("PG 映射的 EventName 不能为空。 ");
                if (string.IsNullOrWhiteSpace(mapping.ArvrTestType))
                    throw new InvalidDataException("PG 映射的 ARVRTestType 不能为空；通配请填写 *。 ");
                if (string.IsNullOrWhiteSpace(mapping.CommandTemplate))
                    throw new InvalidDataException("PG 映射的 CommandTemplate 不能为空。 ");
            }
        }

        public string ExpandCommand(PgActionMapping mapping, string arvrTestType, string serialNumber)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            string normalizedChannel = int.Parse(Channel, CultureInfo.InvariantCulture).ToString("00", CultureInfo.InvariantCulture);
            string command = (mapping.CommandTemplate ?? string.Empty)
                .Replace("{channel}", normalizedChannel)
                .Replace("{testType}", arvrTestType ?? string.Empty)
                .Replace("{sn}", serialNumber ?? string.Empty);
            if (command.Any(character => character > 0x7F))
                throw new InvalidDataException("展开后的 PG 指令包含非 ASCII 字符。 ");
            return command;
        }

        private static void ValidatePort(int port, string name)
        {
            if (port <= 0 || port > 65535)
                throw new InvalidDataException(name + " 必须在 1 到 65535 之间。 ");
        }
    }

    public sealed class PgActionMapping
    {
        public bool Enabled { get; set; } = true;

        public string EventName { get; set; } = "SwitchPG";

        public string ArvrTestType { get; set; } = "0";

        public string Name { get; set; } = string.Empty;

        public string CommandTemplate { get; set; } = "PG,{channel},PATTERN,INDEX,1";

        public string SuccessContains { get; set; } = ",END,OK";

        public PgActionMapping Clone()
        {
            return new PgActionMapping
            {
                Enabled = Enabled,
                EventName = EventName,
                ArvrTestType = ArvrTestType,
                Name = Name,
                CommandTemplate = CommandTemplate,
                SuccessContains = SuccessContains
            };
        }
    }
}
