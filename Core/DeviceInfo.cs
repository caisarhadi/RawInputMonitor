using System;
using RawInputMonitor.Profiles;
using RawInputMonitor.Win32;

namespace RawInputMonitor.Core;

public class DeviceInfo
{
    public string DevicePath { get; set; } = string.Empty;
    public IntPtr DeviceHandle { get; set; }
    public ushort VendorId { get; set; }
    public ushort ProductId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SourceType { get; set; } = "HID";
    public IntPtr PreparsedData { get; set; }
    public HidPInterop.HIDP_CAPS Capabilities { get; set; }
    public IDeviceProfile? Profile { get; set; }
    public bool IsConnected { get; set; } = true;
}
