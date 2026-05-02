using System;
using System.Collections.Generic;
using RawInputMonitor.Core;

namespace RawInputMonitor.Profiles;

public class GenericHidProfile : IDeviceProfile
{

    public bool CanHandle(ushort vendorId, ushort productId)
    {
        // Fallback profile handles everything that hasn't been matched by other profiles
        return true;
    }

    private byte[]? _lastReport;

    public IEnumerable<InputEvent> Decode(byte[] report, int count, DeviceInfo device)
    {
        var events = new List<InputEvent>();
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string deviceId = $"{device.VendorId:X4}:{device.ProductId:X4}";

        bool isFirst = false;
        if (_lastReport == null || _lastReport.Length != report.Length)
        {
            _lastReport = new byte[report.Length];
            isFirst = true;
        }

        for (int i = 0; i < report.Length; i++)
        {
            byte oldByte = _lastReport[i];
            byte newByte = report[i];

            if (isFirst || oldByte != newByte)
            {
                events.Add(new InputEvent
                {
                    DeviceId = deviceId,
                    DeviceName = device.ProductName,
                    SourceType = device.SourceType,
                    Channel = $"Byte[{i:D2}]",
                    Value = newByte,
                    RawValue = newByte,
                    Timestamp = ts
                });

                _lastReport[i] = newByte;
            }
        }

        return events;
    }

    public IEnumerable<InputEvent>? DecodeMouse(Win32.RawInputInterop.RAWMOUSE mouse, DeviceInfo device)
    {
        var events = new List<InputEvent>();
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string deviceId = $"{device.VendorId:X4}:{device.ProductId:X4}";

        // Always emit mouse data exactly as it comes in to ensure relative axes are captured.
        events.Add(new InputEvent { DeviceId = deviceId, DeviceName = device.ProductName, SourceType = device.SourceType, Channel = "MouseX", Value = mouse.lLastX, RawValue = mouse.lLastX, Timestamp = ts });
        events.Add(new InputEvent { DeviceId = deviceId, DeviceName = device.ProductName, SourceType = device.SourceType, Channel = "MouseY", Value = mouse.lLastY, RawValue = mouse.lLastY, Timestamp = ts });
        events.Add(new InputEvent { DeviceId = deviceId, DeviceName = device.ProductName, SourceType = device.SourceType, Channel = "ButtonFlags", Value = mouse.usButtonFlags, RawValue = mouse.usButtonFlags, Timestamp = ts });
        events.Add(new InputEvent { DeviceId = deviceId, DeviceName = device.ProductName, SourceType = device.SourceType, Channel = "ButtonData", Value = mouse.usButtonData, RawValue = mouse.usButtonData, Timestamp = ts });

        return events;
    }


}
