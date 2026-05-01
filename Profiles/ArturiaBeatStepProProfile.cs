using System;
using System.Collections.Generic;
using RawInputMonitor.Core;
using RawInputMonitor.Win32;

namespace RawInputMonitor.Profiles;

public class ArturiaBeatStepProProfile : IDeviceProfile
{
    public string FriendlyName => "Arturia BeatStep Pro";

    // Arturia VID is 1C75. PID varies but 0289 is common. We match VID only.
    public bool CanHandle(ushort vendorId, ushort productId)
    {
        return vendorId == 0x1C75;
    }

    // --- HID Implementation (Fallback / Discovery) ---
    public IEnumerable<InputEvent> Decode(byte[] report, int length, DeviceInfo device)
    {
        // If the BeatStep Pro exposes a proprietary HID endpoint, it will be captured here.
        // We log the raw bytes to the console so the user can discover undocumented mappings.
        Console.WriteLine($"[Arturia HID Discovery] Raw Report: {BitConverter.ToString(report, 0, length)}");
        return Array.Empty<InputEvent>();
    }

    public IEnumerable<InputEvent> DecodeMouse(RawInputInterop.RAWMOUSE mouse, DeviceInfo device)
    {
        return Array.Empty<InputEvent>();
    }

}
