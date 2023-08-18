namespace ColorVision.MQTT
{
    public delegate void DeviceStatusChangedHandler(DeviceStatus deviceStatus);

    public enum DeviceStatus
    {
        Close,
        Closing,
        Open,
        Opening,
        UnInit,
        Init,
        UnConnected
    }

}
