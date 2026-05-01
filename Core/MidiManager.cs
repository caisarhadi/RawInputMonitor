using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RawInputMonitor.Win32;

namespace RawInputMonitor.Core;

/// <summary>
/// Manages MIDI input devices using direct winmm.dll P/Invoke.
/// Each physical MIDI port gets its own DeviceInfo so Port 0 (main MIDI)
/// and Port 1 (MCU/HUI) show as separate devices on the dashboard.
/// All incoming MIDI messages are logged to console for interactive mapping.
/// </summary>
public class MidiManager : IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly List<OpenMidiDevice> _openDevices = new();

    // Must be stored as a field to prevent GC from collecting the delegate
    private readonly MidiInterop.MidiInProc _callbackDelegate;

    // SysEx buffer for MIM_LONGDATA
    private const int SYSEX_BUFFER_SIZE = 256;

    // Output ports and state for Scene LED logic
    private IntPtr _outPort1Handle = IntPtr.Zero;
    private int _activeStepButton = -1;
    private readonly int[] _mcuStepNotes = new int[] { 8, 16, 9, 17, 10, 18, 11, 19, 12, 20, 13, 21, 14, 22, 15, 23 };

    public MidiManager(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
        _callbackDelegate = MidiCallback;
    }

    public void Start()
    {
        Console.WriteLine("[MidiManager] Scanning for MIDI input devices (winmm.dll)...");

        uint numDevices = MidiInterop.midiInGetNumDevs();
        Console.WriteLine($"[MidiManager] winmm reports {numDevices} MIDI input device(s).");

        if (numDevices == 0)
        {
            Console.WriteLine("[MidiManager] No MIDI input devices found.");
            return;
        }

        for (uint i = 0; i < numDevices; i++)
        {
            var caps = new MidiInterop.MIDIINCAPS();
            uint result = MidiInterop.midiInGetDevCaps(i, ref caps, (uint)Marshal.SizeOf(typeof(MidiInterop.MIDIINCAPS)));

            if (result != MidiInterop.MMSYSERR_NOERROR)
            {
                Console.WriteLine($"[MidiManager]   Device {i}: Failed to get caps (error {result})");
                continue;
            }

            // Give each port a distinct name so they show separately on the dashboard
            string portLabel = caps.szPname;
            string sourceLabel = i == 0 ? "MIDI_Port1" : $"MIDI_Port{i + 1}";

            Console.WriteLine($"[MidiManager]   Device {i}: \"{portLabel}\" → registered as [{sourceLabel}]");

            // Each port needs a unique ProductId so the dashboard renders separate panels.
            // We use the winmm device index + 1 as a synthetic PID.
            var deviceInfo = new DeviceInfo
            {
                DevicePath = $"MIDI_IN_{i}_{portLabel}",
                VendorId = 0x1C75, // Arturia USB VID
                ProductId = (ushort)(0x0287 + i), // Unique per port
                Manufacturer = "Arturia",
                ProductName = $"{portLabel} [{sourceLabel}]",
                SourceType = "MIDI",
            };
            _deviceManager.RegisterExternalDevice(deviceInfo);

            // Open the device
            IntPtr hMidiIn;
            result = MidiInterop.midiInOpen(out hMidiIn, i, _callbackDelegate, IntPtr.Zero, MidiInterop.CALLBACK_FUNCTION);
            if (result != MidiInterop.MMSYSERR_NOERROR)
            {
                Console.WriteLine($"[MidiManager]   FAILED to open device {i} (error {result})");
                continue;
            }

            // Prepare a SysEx buffer for long messages
            var sysexDev = new OpenMidiDevice
            {
                Handle = hMidiIn,
                DeviceId = i,
                Name = portLabel,
                PortLabel = sourceLabel,
                Info = deviceInfo
            };
            PrepareSysExBuffer(sysexDev);

            result = MidiInterop.midiInStart(hMidiIn);
            if (result != MidiInterop.MMSYSERR_NOERROR)
            {
                Console.WriteLine($"[MidiManager]   FAILED to start device {i} (error {result})");
                MidiInterop.midiInClose(hMidiIn);
                continue;
            }

            Console.WriteLine($"[MidiManager]   LISTENING on device {i}: \"{portLabel}\" [{sourceLabel}]");
            _openDevices.Add(sysexDev);
        }

        Console.WriteLine($"[MidiManager] {_openDevices.Count} MIDI device(s) opened and listening.");

        // --- Output Ports Setup ---
        uint numOuts = MidiInterop.midiOutGetNumDevs();
        for (uint i = 0; i < numOuts; i++)
        {
            var caps = new MidiInterop.MIDIOUTCAPS();
            if (MidiInterop.midiOutGetDevCaps(i, ref caps, (uint)Marshal.SizeOf(typeof(MidiInterop.MIDIOUTCAPS))) == MidiInterop.MMSYSERR_NOERROR)
            {
                if (caps.szPname.Contains("Arturia", StringComparison.OrdinalIgnoreCase) && 
                    caps.szPname.Contains("BeatStep", StringComparison.OrdinalIgnoreCase))
                {
                    if (caps.szPname.Contains("MIDIOUT2") || caps.szPname.Contains("MIDIIN2")) // Second port
                    {
                        MidiInterop.midiOutOpen(out _outPort1Handle, i, IntPtr.Zero, IntPtr.Zero, 0);
                        Console.WriteLine($"[MidiManager] Opened Output Port 1 (MCU Mode LED): {caps.szPname}");
                    }
                }
            }
        }
    }

    private void PrepareSysExBuffer(OpenMidiDevice dev)
    {
        dev.SysExBuffer = Marshal.AllocHGlobal(SYSEX_BUFFER_SIZE);
        dev.MidiHeader = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MidiInterop.MIDIHDR)));

        var header = new MidiInterop.MIDIHDR
        {
            lpData = dev.SysExBuffer,
            dwBufferLength = SYSEX_BUFFER_SIZE,
            dwBytesRecorded = 0,
            dwFlags = 0
        };
        Marshal.StructureToPtr(header, dev.MidiHeader, false);

        uint result = MidiInterop.midiInPrepareHeader(dev.Handle, dev.MidiHeader, (uint)Marshal.SizeOf(typeof(MidiInterop.MIDIHDR)));
        if (result == MidiInterop.MMSYSERR_NOERROR)
        {
            MidiInterop.midiInAddBuffer(dev.Handle, dev.MidiHeader, (uint)Marshal.SizeOf(typeof(MidiInterop.MIDIHDR)));
        }
    }

    /// <summary>
    /// winmm callback — called from a system thread whenever MIDI data arrives.
    /// </summary>
    private void MidiCallback(IntPtr hMidiIn, int wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
        // Find which device this callback belongs to
        OpenMidiDevice? device = null;
        foreach (var dev in _openDevices)
        {
            if (dev.Handle == hMidiIn)
            {
                device = dev;
                break;
            }
        }
        if (device == null) return;

        if (wMsg == MidiInterop.MIM_DATA)
        {
            HandleShortMessage(device, dwParam1);
        }
        else if (wMsg == MidiInterop.MIM_LONGDATA)
        {
            HandleLongMessage(device);
        }
    }

    private void HandleShortMessage(OpenMidiDevice device, IntPtr dwParam1)
    {
        uint packed = (uint)dwParam1.ToInt64();
        byte status = (byte)(packed & 0xFF);
        byte data1 = (byte)((packed >> 8) & 0xFF);
        byte data2 = (byte)((packed >> 16) & 0xFF);

        byte msgType = (byte)(status & 0xF0);
        byte channel = (byte)(status & 0x0F);

        string port = device.PortLabel;
        string channelName;
        double value;
        double rawValue;

        // System Realtime messages (0xF0-0xFF) — no channel nibble
        if (status >= 0xF0)
        {
            switch (status)
            {
                case 0xF8: // MIDI Clock — suppress, way too frequent
                    return;
                case 0xFA:
                    Console.WriteLine($"  [{port}] >> Transport START");
                    channelName = $"{port}_Transport_Start";
                    value = 1; rawValue = status;
                    break;
                case 0xFB:
                    Console.WriteLine($"  [{port}] >> Transport CONTINUE");
                    channelName = $"{port}_Transport_Continue";
                    value = 1; rawValue = status;
                    break;
                case 0xFC:
                    Console.WriteLine($"  [{port}] >> Transport STOP");
                    channelName = $"{port}_Transport_Stop";
                    value = 1; rawValue = status;
                    break;
                case 0xFE: // Active Sensing — ignore
                    return;
                default:
                    Console.WriteLine($"  [{port}] >> SysRT 0x{status:X2}  D1=0x{data1:X2}  D2=0x{data2:X2}");
                    return;
            }
        }
        else
        {
            switch (msgType)
            {
                case 0x90: // Note On
                    string noteState = data2 > 0 ? "ON " : "OFF";
                    Console.WriteLine($"  [{port}] >> Note {noteState}  Note={data1,-3}  Vel={data2,-3}  Ch={channel + 1}");
                    channelName = $"{port}_Note_{data1}_Ch{channel + 1}";
                    value = data2 > 0 ? 1 : 0;
                    rawValue = data2;

                    // MCU mode step buttons: Port 2, Notes 8-23
                    if (port == "MIDI_Port2")
                    {
                        int stepIdx = Array.IndexOf(_mcuStepNotes, data1);
                        if (stepIdx != -1)
                        {
                            if (data2 > 0)
                            {
                                _activeStepButton = stepIdx;
                            }
                            RefreshLeds();
                        }
                    }
                    break;
                case 0x80: // Note Off
                    Console.WriteLine($"  [{port}] >> Note OFF  Note={data1,-3}  Vel={data2,-3}  Ch={channel + 1}");
                    channelName = $"{port}_Note_{data1}_Ch{channel + 1}";
                    value = 0;
                    rawValue = data2;
                    break;
                case 0xB0: // Control Change
                    Console.WriteLine($"  [{port}] >> CC        CC#={data1,-3}  Val={data2,-3}  Ch={channel + 1}");
                    channelName = $"{port}_CC_{data1}_Ch{channel + 1}";
                    value = data2;
                    rawValue = data2;

                    // CC mode step buttons: Port 1, CC 102-117
                    if (port == "MIDI_Port1" && data1 >= 102 && data1 <= 117)
                    {
                        if (data2 == 127)
                        {
                            _activeStepButton = data1 - 102;
                        }
                        // RefreshLeds() omitted: Hardware ignores incoming MIDI for LEDs in CC mode.
                    }
                    break;
                case 0xE0: // Pitch Bend
                    int pb = (data2 << 7) | data1;
                    Console.WriteLine($"  [{port}] >> PitchBend  Val={pb,-5}  Ch={channel + 1}");
                    channelName = $"{port}_PitchBend_Ch{channel + 1}";
                    value = pb; rawValue = pb;
                    break;
                case 0xC0: // Program Change
                    Console.WriteLine($"  [{port}] >> ProgChange Prog={data1,-3}  Ch={channel + 1}");
                    channelName = $"{port}_ProgramChange_Ch{channel + 1}";
                    value = data1; rawValue = data1;
                    break;
                case 0xD0: // Channel Pressure
                    Console.WriteLine($"  [{port}] >> ChanPress  Val={data1,-3}  Ch={channel + 1}");
                    channelName = $"{port}_Aftertouch_Ch{channel + 1}";
                    value = data1; rawValue = data1;
                    break;
                case 0xA0: // Polyphonic Aftertouch
                    Console.WriteLine($"  [{port}] >> PolyAT     Note={data1,-3}  Val={data2,-3}  Ch={channel + 1}");
                    channelName = $"{port}_PolyAT_{data1}_Ch{channel + 1}";
                    value = data2; rawValue = data2;
                    break;
                default:
                    Console.WriteLine($"  [{port}] >> ???  Status=0x{status:X2}  D1=0x{data1:X2}  D2=0x{data2:X2}");
                    return;
            }
        }

        _deviceManager.EnqueueEvent(new InputEvent
        {
            DeviceId = $"{device.Info.VendorId:X4}:{device.Info.ProductId:X4}",
            DeviceName = device.Info.ProductName,
            SourceType = "MIDI",
            Channel = channelName,
            Value = value,
            RawValue = rawValue,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private void HandleLongMessage(OpenMidiDevice device)
    {
        if (device.MidiHeader == IntPtr.Zero) return;

        var header = Marshal.PtrToStructure<MidiInterop.MIDIHDR>(device.MidiHeader);
        if (header.dwBytesRecorded > 0)
        {
            byte[] sysexData = new byte[header.dwBytesRecorded];
            Marshal.Copy(header.lpData, sysexData, 0, (int)header.dwBytesRecorded);
            Console.WriteLine($"  [{device.PortLabel}] >> SysEx ({sysexData.Length} bytes): {BitConverter.ToString(sysexData)}");

            _deviceManager.EnqueueEvent(new InputEvent
            {
                DeviceId = $"{device.Info.VendorId:X4}:{device.Info.ProductId:X4}",
                DeviceName = device.Info.ProductName,
                SourceType = "MIDI",
                Channel = $"{device.PortLabel}_SysEx",
                Value = sysexData.Length,
                RawValue = sysexData.Length,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        // Re-add the buffer for the next SysEx message
        MidiInterop.midiInAddBuffer(device.Handle, device.MidiHeader, (uint)Marshal.SizeOf(typeof(MidiInterop.MIDIHDR)));
    }

    private void SendNoteOn(IntPtr handle, byte channel, byte note, byte velocity)
    {
        if (handle == IntPtr.Zero) return;
        uint msg = (uint)(0x90 | channel | (note << 8) | (velocity << 16));
        MidiInterop.midiOutShortMsg(handle, msg);
    }

    private void RefreshLeds()
    {
        if (_activeStepButton < 0 || _activeStepButton > 15) return;

        // Port 1 (MCU Mode LED feedback)
        // Note: CC mode LED feedback is ignored by BeatStep Pro hardware, so we only send to MCU port.
        if (_outPort1Handle != IntPtr.Zero)
        {
            for (byte i = 0; i < 16; i++)
            {
                byte val = (byte)(i == _activeStepButton ? 127 : 0);
                SendNoteOn(_outPort1Handle, 0, (byte)_mcuStepNotes[i], val);
            }
        }
    }

    public void Dispose()
    {
        foreach (var dev in _openDevices)
        {
            MidiInterop.midiInStop(dev.Handle);
            MidiInterop.midiInReset(dev.Handle);

            if (dev.MidiHeader != IntPtr.Zero)
            {
                MidiInterop.midiInUnprepareHeader(dev.Handle, dev.MidiHeader, (uint)Marshal.SizeOf(typeof(MidiInterop.MIDIHDR)));
                Marshal.FreeHGlobal(dev.MidiHeader);
            }
            if (dev.SysExBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(dev.SysExBuffer);
            }

            MidiInterop.midiInClose(dev.Handle);
            Console.WriteLine($"[MidiManager] Closed: {dev.Name}");
        }
        _openDevices.Clear();

        if (_outPort1Handle != IntPtr.Zero)
        {
            MidiInterop.midiOutClose(_outPort1Handle);
            _outPort1Handle = IntPtr.Zero;
            Console.WriteLine("[MidiManager] Closed Output Port 1");
        }
    }

    private class OpenMidiDevice
    {
        public IntPtr Handle;
        public uint DeviceId;
        public string Name = string.Empty;
        public string PortLabel = string.Empty;
        public DeviceInfo Info = null!;
        public IntPtr SysExBuffer = IntPtr.Zero;
        public IntPtr MidiHeader = IntPtr.Zero;
    }
}
