using System;
using System.Collections.Generic;
using RawInputMonitor.Core;

namespace RawInputMonitor.Profiles;

/// <summary>
/// Device profile for 3Dconnexion SpaceMouse devices.
/// Decodes multi-report HID data: Report ID 1 (Translation), Report ID 2 (Rotation), Report ID 3 (Buttons).
/// Each axis is a signed 16-bit little-endian integer.
/// Supports both legacy Logitech VID (046D) and modern 3Dconnexion VID (256F).
/// </summary>
public class SpaceMouseProfile : IDeviceProfile
{
    public string FriendlyName => "3Dconnexion SpaceMouse";

    // Known 3Dconnexion PIDs for targeted matching
    private static readonly HashSet<ushort> _knownPids = new()
    {
        0xC626, // SpaceNavigator
        0xC627, // SpaceExplorer
        0xC628, // SpaceNavigator for Notebooks
        0xC629, // SpacePilot Pro
        0xC62A, // Legacy 3Dconnexion
        0xC62B, // SpaceMouse Pro
        0xC631, // SpaceMouse Pro Wireless (USB)
        0xC632, // SpaceMouse Pro Wireless Receiver
        0xC635, // SpaceMouse Compact
        0xC652, // SpaceMouse Pro V2 / Enterprise
    };

    public bool CanHandle(ushort vendorId, ushort productId)
    {
        // Modern 3Dconnexion VID — always match
        if (vendorId == 0x256F) return true;

        // Legacy Logitech VID — only match known SpaceMouse PIDs to avoid
        // false positives on Logitech keyboards/mice sharing the same VID
        if (vendorId == 0x046D) return _knownPids.Contains(productId);

        return false;
    }

    // Dead zone threshold — axis values within this range are treated as noise/rest position
    private const short DeadZone = 5;

    // Track previous button state for edge detection
    private uint _lastButtonMask = 0;

    public IEnumerable<InputEvent> Decode(byte[] report, int count, DeviceInfo device)
    {
        var events = new List<InputEvent>();
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string deviceId = $"{device.VendorId:X4}:{device.ProductId:X4}";

        if (report.Length < 1) return events;

        byte reportId = report[0];

        switch (reportId)
        {
            case 1:
                // OpenUnrealSpaceMouse FSingleReportTransRotHidReader:
                // C652 Universal Receiver sends Report ID 1 with 13 bytes (FTransRotReport)
                // Layout: [ReportID=1][TX_lo][TX_hi][TY_lo][TY_hi][TZ_lo][TZ_hi][RX_lo][RX_hi][RY_lo][RY_hi][RZ_lo][RZ_hi]
                if (report.Length >= 13)
                {
                    short tx = (short)(report[1] | (report[2] << 8));
                    short ty = (short)(report[3] | (report[4] << 8));
                    short tz = (short)(report[5] | (report[6] << 8));
                    short rx = (short)(report[7] | (report[8] << 8));
                    short ry = (short)(report[9] | (report[10] << 8));
                    short rz = (short)(report[11] | (report[12] << 8));

                    if (Math.Abs(tx) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "TX", tx, tx, ts, "HID_AXIS"));
                    if (Math.Abs(ty) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "TY", ty, ty, ts, "HID_AXIS"));
                    if (Math.Abs(tz) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "TZ", tz, tz, ts, "HID_AXIS"));
                    if (Math.Abs(rx) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "RX", rx, rx, ts, "HID_AXIS"));
                    if (Math.Abs(ry) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "RY", ry, ry, ts, "HID_AXIS"));
                    if (Math.Abs(rz) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "RZ", rz, rz, ts, "HID_AXIS"));
                }
                // OpenUnrealSpaceMouse FSeparateReportTransRotHidReader:
                // Legacy devices send 7-byte Report ID 1 = Translation only
                else if (report.Length >= 7)
                {
                    short tx = (short)(report[1] | (report[2] << 8));
                    short ty = (short)(report[3] | (report[4] << 8));
                    short tz = (short)(report[5] | (report[6] << 8));

                    if (Math.Abs(tx) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "TX", tx, tx, ts, "HID_AXIS"));
                    if (Math.Abs(ty) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "TY", ty, ty, ts, "HID_AXIS"));
                    if (Math.Abs(tz) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "TZ", tz, tz, ts, "HID_AXIS"));
                }
                break;

            case 2: // FSeparateReportTransRotHidReader: Legacy 7-byte Report ID 2 = Rotation
                if (report.Length >= 7)
                {
                    short rx = (short)(report[1] | (report[2] << 8));
                    short ry = (short)(report[3] | (report[4] << 8));
                    short rz = (short)(report[5] | (report[6] << 8));

                    if (Math.Abs(rx) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "RX", rx, rx, ts, "HID_AXIS"));
                    if (Math.Abs(ry) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "RY", ry, ry, ts, "HID_AXIS"));
                    if (Math.Abs(rz) > DeadZone) events.Add(CreateEvent(deviceId, device.ProductName, "RZ", rz, rz, ts, "HID_AXIS"));
                }
                break;



            case 3: // Buttons (variable length bitmask depending on device model)
                if (report.Length >= 2)
                {
                    // Build button mask from available bytes (up to 4 bytes = 32 buttons)
                    uint buttonMask = 0;
                    int buttonBytes = Math.Min(report.Length - 1, 4);
                    for (int i = 0; i < buttonBytes; i++)
                    {
                        buttonMask |= (uint)(report[1 + i] << (i * 8));
                    }

                    // Emit events only for changed buttons (edge detection)
                    uint changed = buttonMask ^ _lastButtonMask;
                    for (int bit = 0; bit < (buttonBytes * 8); bit++)
                    {
                        if ((changed & (1u << bit)) != 0)
                        {
                            int state = (int)((buttonMask >> bit) & 1);
                            events.Add(CreateEvent(deviceId, device.ProductName, $"Button_{bit + 1}", state, state, ts, "HID_BUTTON"));
                        }
                    }

                    _lastButtonMask = buttonMask;
                }
                break;

            default:
                // Unknown report ID — emit raw for discovery/mapping
                events.Add(new InputEvent
                {
                    DeviceId = deviceId,
                    DeviceName = device.ProductName,
                    SourceType = "HID",
                    Channel = $"ReportID_{reportId}",
                    Value = report.Length,
                    RawValue = reportId,
                    Timestamp = ts
                });
                break;
        }

        return events;
    }

    private static InputEvent CreateEvent(string deviceId, string deviceName, string channel, double value, double rawValue, long ts, string sourceType)
    {
        return new InputEvent
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            SourceType = sourceType,
            Channel = channel,
            Value = value,
            RawValue = rawValue,
            Timestamp = ts
        };
    }
}
