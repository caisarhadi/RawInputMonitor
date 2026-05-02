using System;
using System.Collections.Generic;
using RawInputMonitor.Core;

namespace RawInputMonitor.Profiles;

public class TangentWaveProfile : IDeviceProfile
{

    public bool CanHandle(ushort vendorId, ushort productId)
    {
        return vendorId == 0x04D8 && productId == 0xFDCF;
    }

    private byte[]? _lastReport;
    private static readonly HashSet<int> _seenBytes = new();

    private static readonly Dictionary<int, string> _axisMap = new()
    {
        { 20, "Trackball_1_X" },
        { 21, "Trackball_1_Y" },
        { 22, "Trackball_2_X" },
        { 23, "Trackball_2_Y" },
        { 24, "Trackball_3_X" },
        { 25, "Trackball_3_Y" },
        { 16, "JogWheel_1" },
        { 17, "JogWheel_2" },
        { 18, "JogWheel_3" },
        { 19, "JogWheel_4" },
        { 7, "RotaryKnob_1" },
        { 8, "RotaryKnob_2" },
        { 9, "RotaryKnob_3" },
        { 10, "RotaryKnob_4" },
        { 11, "RotaryKnob_5" },
        { 12, "RotaryKnob_6" },
        { 13, "RotaryKnob_7" },
        { 14, "RotaryKnob_8" },
        { 15, "RotaryKnob_9" }
    };

    private static readonly HashSet<string> _seenBits = new();
    private static readonly Dictionary<string, string> _buttonMap = new()
    {
        { "Bit[02:1]", "Button_Alt" },
        { "Bit[02:2]", "Button_TB1_Left" },
        { "Bit[02:3]", "Button_TB1_Center" },
        { "Bit[02:4]", "Button_TB1_Right" },
        { "Bit[02:5]", "Button_TB1_ResetBall" },
        { "Bit[02:6]", "Button_TB1_ResetWheel" },
        { "Bit[02:7]", "Button_TB2_Left" },
        { "Bit[03:0]", "Button_TB2_Center" },
        { "Bit[03:1]", "Button_TB2_Right" },
        { "Bit[03:2]", "Button_TB2_ResetBall" },
        { "Bit[03:3]", "Button_TB2_ResetWheel" },
        { "Bit[03:4]", "Button_TB3_Left" },
        { "Bit[03:5]", "Button_TB3_Center" },
        { "Bit[03:6]", "Button_TB3_Right" },
        { "Bit[03:7]", "Button_TB3_ResetBall" },
        { "Bit[04:0]", "Button_TB3_ResetWheel" },
        { "Bit[05:4]", "Button_Trans_FastRewind" },
        { "Bit[05:5]", "Button_Trans_FastForward" },
        { "Bit[05:6]", "Button_Trans_PlayReverse" },
        { "Bit[05:7]", "Button_Trans_Stop" },
        { "Bit[06:0]", "Button_Trans_PlayForward" },
        { "Bit[05:1]", "Button_Fn1" },
        { "Bit[05:2]", "Button_Fn2" },
        { "Bit[05:3]", "Button_Fn3" },
        { "Bit[04:6]", "Button_Fn4" },
        { "Bit[04:7]", "Button_Fn5" },
        { "Bit[05:0]", "Button_Fn6" },
        { "Bit[04:3]", "Button_Fn7" },
        { "Bit[04:4]", "Button_Fn8" },
        { "Bit[04:5]", "Button_Fn9" },
        { "Bit[04:1]", "Button_Up" },
        { "Bit[04:2]", "Button_Down" }
    };

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
                if (_seenBytes.Add(i))
                {
                    System.IO.File.AppendAllText("mapping_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] DETECTED: Byte[{i:D2}]\n");
                }

                if (_axisMap.TryGetValue(i, out string? axisName))
                {
                    sbyte delta = (sbyte)newByte;
                    events.Add(new InputEvent
                    {
                        DeviceId = deviceId,
                        DeviceName = device.ProductName,
                        SourceType = "HID_AXIS",
                        Channel = axisName,
                        Value = delta,
                        RawValue = newByte,
                        Timestamp = ts
                    });
                }
                else
                {
                    // Emit the full byte change
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

                    // Emit individual bit changes
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int oldBit = (oldByte >> bit) & 1;
                        int newBit = (newByte >> bit) & 1;
                        
                        if (oldBit != newBit)
                        {
                            string rawBitChannel = $"Bit[{i:D2}:{bit}]";

                            if (oldBit == 0 && newBit == 1)
                            {
                                if (_seenBits.Add(rawBitChannel))
                                {
                                    System.IO.File.AppendAllText("mapping_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] DETECTED: {rawBitChannel}\n");
                                }
                            }

                            if (_buttonMap.TryGetValue(rawBitChannel, out string? buttonName))
                            {
                                events.Add(new InputEvent
                                {
                                    DeviceId = deviceId,
                                    DeviceName = device.ProductName,
                                    SourceType = "HID_BUTTON",
                                    Channel = buttonName,
                                    Value = newBit,
                                    RawValue = newBit,
                                    Timestamp = ts
                                });
                            }
                            else
                            {
                                events.Add(new InputEvent
                                {
                                    DeviceId = deviceId,
                                    DeviceName = device.ProductName,
                                    SourceType = "HID_BIT", 
                                    Channel = rawBitChannel,
                                    Value = newBit,
                                    RawValue = newBit,
                                    Timestamp = ts
                                });
                            }
                        }
                    }
                }

                _lastReport[i] = newByte;
            }
        }

        return events;
    }
}
