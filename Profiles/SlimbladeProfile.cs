using System;
using System.Collections.Generic;
using RawInputMonitor.Core;
using RawInputMonitor.Win32;

namespace RawInputMonitor.Profiles;

public class SlimbladeProfile : IDeviceProfile
{
    public string FriendlyName => "Kensington Slimblade Pro";

    public bool CanHandle(ushort vendorId, ushort productId)
    {
        return vendorId == 0x047D; // We catch all Kensington devices for now, or you can refine the PID later
    }

    public IEnumerable<InputEvent> Decode(byte[] report, int count, DeviceInfo device)
    {
        return new List<InputEvent>(); // Handled via DecodeMouse instead
    }

    public IEnumerable<InputEvent>? DecodeMouse(RawInputInterop.RAWMOUSE mouse, DeviceInfo device)
    {
        var events = new List<InputEvent>();
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string deviceId = $"{device.VendorId:X4}:{device.ProductId:X4}";

        // 1. X / Y Movement (Trackball)
        if (mouse.lLastX != 0)
        {
            events.Add(CreateEvent(deviceId, device.ProductName, "X", mouse.lLastX, ts));
        }
        if (mouse.lLastY != 0)
        {
            events.Add(CreateEvent(deviceId, device.ProductName, "Y", mouse.lLastY, ts));
        }

        // 2. Scroll (Trackball Twist)
        if ((mouse.usButtonFlags & RawInputInterop.RI_MOUSE_WHEEL) != 0)
        {
            // Normalize WHEEL_DELTA (120) to 1 or -1 to match other relative axes
            double scrollDelta = (double)mouse.usButtonData / 120.0;
            events.Add(CreateEvent(deviceId, device.ProductName, "Twist_Scroll", scrollDelta, ts));
        }

        // 3. Physical Buttons
        CheckButton(mouse.usButtonFlags, RawInputInterop.RI_MOUSE_LEFT_BUTTON_DOWN, RawInputInterop.RI_MOUSE_LEFT_BUTTON_UP, "Button_BottomLeft", events, deviceId, device.ProductName, ts);
        CheckButton(mouse.usButtonFlags, RawInputInterop.RI_MOUSE_RIGHT_BUTTON_DOWN, RawInputInterop.RI_MOUSE_RIGHT_BUTTON_UP, "Button_BottomRight", events, deviceId, device.ProductName, ts);
        CheckButton(mouse.usButtonFlags, RawInputInterop.RI_MOUSE_MIDDLE_BUTTON_DOWN, RawInputInterop.RI_MOUSE_MIDDLE_BUTTON_UP, "Button_TopLeft", events, deviceId, device.ProductName, ts);
        CheckButton(mouse.usButtonFlags, RawInputInterop.RI_MOUSE_BUTTON_4_DOWN, RawInputInterop.RI_MOUSE_BUTTON_4_UP, "Button_TopRight", events, deviceId, device.ProductName, ts);
        CheckButton(mouse.usButtonFlags, RawInputInterop.RI_MOUSE_BUTTON_5_DOWN, RawInputInterop.RI_MOUSE_BUTTON_5_UP, "Button_Extra", events, deviceId, device.ProductName, ts);

        return events;
    }

    private void CheckButton(ushort flags, ushort downFlag, ushort upFlag, string channel, List<InputEvent> events, string deviceId, string deviceName, long ts)
    {
        if ((flags & downFlag) != 0)
            events.Add(CreateEvent(deviceId, deviceName, channel, 1, ts, "HID_BUTTON"));
        else if ((flags & upFlag) != 0)
            events.Add(CreateEvent(deviceId, deviceName, channel, 0, ts, "HID_BUTTON"));
    }

    private InputEvent CreateEvent(string deviceId, string deviceName, string channel, double value, long ts, string sourceType = "HID_AXIS")
    {
        return new InputEvent
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            SourceType = sourceType,
            Channel = channel,
            Value = value,
            RawValue = value,
            Timestamp = ts
        };
    }
}
