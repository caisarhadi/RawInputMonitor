using System;
using System.Collections.Generic;
using RawInputMonitor.Core;

namespace RawInputMonitor.Profiles;

public class GenericHidProfile : IDeviceProfile
{
    public string FriendlyName => "Generic HID Device";

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

        if (_lastReport == null || _lastReport.Length != report.Length)
        {
            _lastReport = new byte[report.Length];
        }

        for (int i = 0; i < report.Length; i++)
        {
            byte oldByte = _lastReport[i];
            byte newByte = report[i];

            if (oldByte != newByte)
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
}
