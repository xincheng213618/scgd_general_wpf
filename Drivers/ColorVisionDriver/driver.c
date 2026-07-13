#include "public.h"

DRIVER_INITIALIZE DriverEntry;
DRIVER_UNLOAD ColorVisionDriverUnload;
DRIVER_DISPATCH ColorVisionDriverCreateClose;
DRIVER_DISPATCH ColorVisionDriverDeviceControl;
DRIVER_DISPATCH ColorVisionDriverUnsupported;

static NTSTATUS ColorVisionDriverCreateDevice(PDRIVER_OBJECT DriverObject)
{
    UNICODE_STRING deviceName;
    UNICODE_STRING symbolicLinkName;
    PDEVICE_OBJECT deviceObject = NULL;

    RtlInitUnicodeString(&deviceName, CVDRV_NT_DEVICE_NAME);
    NTSTATUS status = IoCreateDevice(DriverObject, 0, &deviceName, FILE_DEVICE_COLORVISION, 0, FALSE, &deviceObject);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    deviceObject->Flags |= DO_BUFFERED_IO;
    RtlInitUnicodeString(&symbolicLinkName, CVDRV_DOS_DEVICE_NAME);
    status = IoCreateSymbolicLink(&symbolicLinkName, &deviceName);
    if (!NT_SUCCESS(status))
    {
        IoDeleteDevice(deviceObject);
        return status;
    }

    deviceObject->Flags &= ~DO_DEVICE_INITIALIZING;
    return STATUS_SUCCESS;
}

NTSTATUS DriverEntry(PDRIVER_OBJECT DriverObject, PUNICODE_STRING RegistryPath)
{
    UNREFERENCED_PARAMETER(RegistryPath);

    for (ULONG i = 0; i <= IRP_MJ_MAXIMUM_FUNCTION; i++)
    {
        DriverObject->MajorFunction[i] = ColorVisionDriverUnsupported;
    }

    DriverObject->MajorFunction[IRP_MJ_CREATE] = ColorVisionDriverCreateClose;
    DriverObject->MajorFunction[IRP_MJ_CLOSE] = ColorVisionDriverCreateClose;
    DriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL] = ColorVisionDriverDeviceControl;
    DriverObject->DriverUnload = ColorVisionDriverUnload;

    return ColorVisionDriverCreateDevice(DriverObject);
}

VOID ColorVisionDriverUnload(PDRIVER_OBJECT DriverObject)
{
    UNICODE_STRING symbolicLinkName;

    RtlInitUnicodeString(&symbolicLinkName, CVDRV_DOS_DEVICE_NAME);
    IoDeleteSymbolicLink(&symbolicLinkName);

    if (DriverObject->DeviceObject != NULL)
    {
        IoDeleteDevice(DriverObject->DeviceObject);
    }
}

NTSTATUS ColorVisionDriverCreateClose(PDEVICE_OBJECT DeviceObject, PIRP Irp)
{
    UNREFERENCED_PARAMETER(DeviceObject);

    Irp->IoStatus.Status = STATUS_SUCCESS;
    Irp->IoStatus.Information = 0;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);
    return STATUS_SUCCESS;
}

NTSTATUS ColorVisionDriverUnsupported(PDEVICE_OBJECT DeviceObject, PIRP Irp)
{
    UNREFERENCED_PARAMETER(DeviceObject);

    Irp->IoStatus.Status = STATUS_INVALID_DEVICE_REQUEST;
    Irp->IoStatus.Information = 0;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);
    return STATUS_INVALID_DEVICE_REQUEST;
}

NTSTATUS ColorVisionDriverDeviceControl(PDEVICE_OBJECT DeviceObject, PIRP Irp)
{
    UNREFERENCED_PARAMETER(DeviceObject);

    PIO_STACK_LOCATION stack = IoGetCurrentIrpStackLocation(Irp);
    ULONG outputLength = stack->Parameters.DeviceIoControl.OutputBufferLength;
    ULONG controlCode = stack->Parameters.DeviceIoControl.IoControlCode;
    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;
    ULONG_PTR information = 0;

    switch (controlCode)
    {
    case IOCTL_CVDRV_PING:
        if (outputLength >= sizeof(CVDRV_PING_RESPONSE))
        {
            PCVDRV_PING_RESPONSE response = (PCVDRV_PING_RESPONSE)Irp->AssociatedIrp.SystemBuffer;
            response->Signature = CVDRV_SIGNATURE;
            response->AbiVersion = CVDRV_ABI_VERSION;
            status = STATUS_SUCCESS;
            information = sizeof(CVDRV_PING_RESPONSE);
        }
        else
        {
            status = STATUS_BUFFER_TOO_SMALL;
        }
        break;

    case IOCTL_CVDRV_GET_VERSION:
        if (outputLength >= sizeof(CVDRV_VERSION_INFO))
        {
            PCVDRV_VERSION_INFO version = (PCVDRV_VERSION_INFO)Irp->AssociatedIrp.SystemBuffer;
            version->AbiVersion = CVDRV_ABI_VERSION;
            version->Major = 0;
            version->Minor = 1;
            version->Patch = 0;
            version->Build = 0;
            status = STATUS_SUCCESS;
            information = sizeof(CVDRV_VERSION_INFO);
        }
        else
        {
            status = STATUS_BUFFER_TOO_SMALL;
        }
        break;
    }

    Irp->IoStatus.Status = status;
    Irp->IoStatus.Information = information;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);
    return status;
}
