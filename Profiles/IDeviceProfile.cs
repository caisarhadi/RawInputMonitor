using System;
using System.Collections.Generic;
using RawInputMonitor.Core;

namespace RawInputMonitor.Profiles;

public interface IDeviceProfile
{
    bool CanHandle(ushort vendorId, ushort productId);
    
    // For standard HID reports
    IEnumerable<InputEvent> Decode(byte[] report, int count, DeviceInfo device);
    
    // For Mouse-class reports (like Slimblade)
    IEnumerable<InputEvent>? DecodeMouse(RawInputMonitor.Win32.RawInputInterop.RAWMOUSE mouse, DeviceInfo device) => null;


}
