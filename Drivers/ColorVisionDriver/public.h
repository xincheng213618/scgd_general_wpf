#pragma once

#ifdef _KERNEL_MODE
#include <ntddk.h>
#else
#include <winioctl.h>
#endif

#define CVDRV_NT_DEVICE_NAME L"\\Device\\ColorVisionDriver"
#define CVDRV_DOS_DEVICE_NAME L"\\DosDevices\\ColorVisionDriver"
#define CVDRV_USER_DEVICE_NAME L"\\\\.\\ColorVisionDriver"

#define FILE_DEVICE_COLORVISION 0x8000
#define CVDRV_IOCTL_INDEX 0x800

#define IOCTL_CVDRV_PING CTL_CODE(FILE_DEVICE_COLORVISION, CVDRV_IOCTL_INDEX + 1, METHOD_BUFFERED, FILE_READ_DATA)
#define IOCTL_CVDRV_GET_VERSION CTL_CODE(FILE_DEVICE_COLORVISION, CVDRV_IOCTL_INDEX + 2, METHOD_BUFFERED, FILE_READ_DATA)

#define CVDRV_ABI_VERSION 1
#define CVDRV_SIGNATURE 0x43564452u

typedef struct _CVDRV_PING_RESPONSE
{
    unsigned long Signature;
    unsigned long AbiVersion;
} CVDRV_PING_RESPONSE, *PCVDRV_PING_RESPONSE;

typedef struct _CVDRV_VERSION_INFO
{
    unsigned long AbiVersion;
    unsigned long Major;
    unsigned long Minor;
    unsigned long Patch;
    unsigned long Build;
} CVDRV_VERSION_INFO, *PCVDRV_VERSION_INFO;
