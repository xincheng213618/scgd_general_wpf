namespace ColorVision.MQTT
{
    public delegate void DeviceStatusChangedHandler(DeviceStatus deviceStatus);

    public enum DeviceStatus
    {
        Closed = 0,
        Closing = 1,
        Opened = 2,
        Opening = 3,
        Busy = 4,
        Free = 5,
        UnInit,
        Init,
        UnConnected
    }

}
