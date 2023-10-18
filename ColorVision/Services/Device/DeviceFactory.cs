#pragma warning disable CS8603 

using ColorVision.Device.Camera;
using ColorVision.Device.PG;
using ColorVision.Device.Sensor;
using ColorVision.Device.SMU;
using ColorVision.Device.Spectrum;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Services;

namespace ColorVision.Device
{
    public class DeviceFactory
    {

        public static BaseChannel CreateDevice(SysResourceModel device)
        {
            BaseChannel result = null;
            DeviceType deviceType = (DeviceType)device.Type;

            switch (deviceType)
            {
                case DeviceType.Camera:
                    result = new DeviceCamera(device);
                    break;
                case DeviceType.PG:
                    result = new DevicePG(device);
                    break;
                case DeviceType.Spectum:
                    result = new DeviceSpectrum(device);
                    break;
                case DeviceType.SMU:
                    result = new DeviceSMU(device);
                    break;
                case DeviceType.Sensor:
                    result = new DeviceSensor(device);
                    break;
                case DeviceType.FileServer:
                    result = new DeviceSpectrum(device);
                    break;
                case DeviceType.Algorithm:
                    result = new DeviceSpectrum(device);
                    break;
                default:
                    break;
            }

            return result;
        }
    }
}
