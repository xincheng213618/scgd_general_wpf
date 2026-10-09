using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.SMU.Configs;

public enum SMUSerialBaudRate
{
    [Description("300")] Baud300 = 300,
    [Description("600")] Baud600 = 600,
    [Description("1200")] Baud1200 = 1200,
    [Description("2400")] Baud2400 = 2400,
    [Description("4800")] Baud4800 = 4800,
    [Description("9600")] Baud9600 = 9600,
    [Description("19200")] Baud19200 = 19200,
    [Description("38400")] Baud38400 = 38400,
    [Description("57600")] Baud57600 = 57600,
    [Description("115200")] Baud115200 = 115200
}
