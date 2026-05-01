using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RawInputMonitor.Win32;
using RawInputMonitor.Profiles;

namespace RawInputMonitor.Core;

public class DeviceManager
{
    private readonly Dictionary<IntPtr, DeviceInfo> _devices = new();
    private readonly ConcurrentQueue<InputEvent> _eventQueue = new();
    private readonly List<IDeviceProfile> _profiles = new();

    public DeviceManager()
    {
        _profiles.Add(new TangentWaveProfile());
        _profiles.Add(new SpaceMouseProfile());
        _profiles.Add(new SlimbladeProfile());
        _profiles.Add(new ArturiaBeatStepProProfile());
        RefreshDevices();
    }

    public bool TryDequeueEvent(out InputEvent evt)
    {
        return _eventQueue.TryDequeue(out evt);
    }

    public void EnqueueEvent(InputEvent evt)
    {
        _eventQueue.Enqueue(evt);
    }

    public void RegisterExternalDevice(DeviceInfo info)
    {
        lock (_devices)
        {
            // Use a random or fake IntPtr for external devices that don't have a RawInput handle
            IntPtr fakeHandle = new IntPtr(info.GetHashCode());
            info.DeviceHandle = fakeHandle;
            _devices[fakeHandle] = info;
            Console.WriteLine($"[DeviceManager] External Device Registered: {info.ProductName} (Source: {info.SourceType})");
        }
    }

    public IEnumerable<DeviceInfo> GetConnectedDevices()
    {
        lock (_devices)
        {
            return new List<DeviceInfo>(_devices.Values);
        }
    }

    public void ProcessDeviceChange(IntPtr wParam, IntPtr lParam)
    {
        RefreshDevices();
    }

    private void RefreshDevices()
    {
        uint deviceCount = 0;
        RawInputInterop.GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, (uint)Marshal.SizeOf(typeof(RawInputInterop.RAWINPUTDEVICELIST)));
        if (deviceCount == 0) return;

        var deviceList = new RawInputInterop.RAWINPUTDEVICELIST[deviceCount];
        RawInputInterop.GetRawInputDeviceList(deviceList, ref deviceCount, (uint)Marshal.SizeOf(typeof(RawInputInterop.RAWINPUTDEVICELIST)));

        lock (_devices)
        {
            var currentHandles = new HashSet<IntPtr>();

            foreach (var dev in deviceList)
            {
                currentHandles.Add(dev.hDevice);
                if (!_devices.ContainsKey(dev.hDevice))
                {
                    var info = LoadDeviceInfo(dev.hDevice);
                    if (info != null)
                    {
                        _devices[dev.hDevice] = info;
                        Console.WriteLine($"[DeviceManager] Connected: {info.ProductName} (VID: {info.VendorId:X4}, PID: {info.ProductId:X4})");
                    }
                }
            }

            foreach (var key in new List<IntPtr>(_devices.Keys))
            {
                if (!currentHandles.Contains(key))
                {
                    var info = _devices[key];
                    // Don't remove externally registered devices (e.g. MIDI) — 
                    // they aren't tracked by WM_INPUT and would be falsely disconnected.
                    if (info.SourceType != "HID" && info.SourceType != "Mouse")
                        continue;

                    info.IsConnected = false;
                    Console.WriteLine($"[DeviceManager] Disconnected: {info.ProductName}");
                    _devices.Remove(key);
                }
            }
        }
    }

    private DeviceInfo? LoadDeviceInfo(IntPtr hDevice)
    {
        uint nameSize = 0;
        RawInputInterop.GetRawInputDeviceInfo(hDevice, RawInputInterop.RIDI_DEVICENAME, IntPtr.Zero, ref nameSize);
        if (nameSize == 0) return null;

        var namePtr = Marshal.AllocHGlobal((int)nameSize * 2);
        RawInputInterop.GetRawInputDeviceInfo(hDevice, RawInputInterop.RIDI_DEVICENAME, namePtr, ref nameSize);
        string devicePath = Marshal.PtrToStringUni(namePtr) ?? string.Empty;
        Marshal.FreeHGlobal(namePtr);

        ushort vid = 0;
        ushort pid = 0;
        var vidMatch = System.Text.RegularExpressions.Regex.Match(devicePath, @"VID(?:_|&)([A-Fa-f0-9]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var pidMatch = System.Text.RegularExpressions.Regex.Match(devicePath, @"PID(?:_|&)([A-Fa-f0-9]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (vidMatch.Success)
        {
            string v = vidMatch.Groups[1].Value;
            if (v.Length > 4) v = v.Substring(v.Length - 4);
            vid = Convert.ToUInt16(v, 16);
        }
        if (pidMatch.Success)
        {
            string p = pidMatch.Groups[1].Value;
            if (p.Length > 4) p = p.Substring(p.Length - 4);
            pid = Convert.ToUInt16(p, 16);
        }

        Console.WriteLine($"[DeviceManager] Path: {devicePath}");

        uint size = 0;
        RawInputInterop.GetRawInputDeviceInfo(hDevice, RawInputInterop.RIDI_DEVICEINFO, IntPtr.Zero, ref size);
        if (size == 0) return null;

        var infoPtr = Marshal.AllocHGlobal((int)size);
        RawInputInterop.GetRawInputDeviceInfo(hDevice, RawInputInterop.RIDI_DEVICEINFO, infoPtr, ref size);
        var ridi = Marshal.PtrToStructure<RawInputInterop.RID_DEVICE_INFO>(infoPtr);
        Marshal.FreeHGlobal(infoPtr);

        if (ridi.dwType != RawInputInterop.RIM_TYPEHID && ridi.dwType != RawInputInterop.RIM_TYPEMOUSE) 
            return null;

        if (ridi.dwType == RawInputInterop.RIM_TYPEHID)
        {
            if (vid == 0) vid = (ushort)ridi.hid.dwVendorId;
            if (pid == 0) pid = (ushort)ridi.hid.dwProductId;
        }

        var info = new DeviceInfo
        {
            DeviceHandle = hDevice,
            DevicePath = devicePath,
            VendorId = vid,
            ProductId = pid,
        };

        // Resolve profile
        foreach (var profile in _profiles)
        {
            if (profile.CanHandle(info.VendorId, info.ProductId))
            {
                info.Profile = profile;
                info.ProductName = profile.FriendlyName;
                break;
            }
        }

        if (string.IsNullOrEmpty(info.ProductName))
        {
            info.ProductName = $"Unknown HID ({info.VendorId:X4}:{info.ProductId:X4})";
        }

        return info;
    }

    public void ProcessRawInput(IntPtr hRawInput)
    {
        int size = 0;
        int headerSize = Marshal.SizeOf(typeof(RawInputInterop.RAWINPUTHEADER));
        RawInputInterop.GetRawInputData(hRawInput, RawInputInterop.RID_INPUT, IntPtr.Zero, ref size, headerSize);
        if (size == 0) return;

        var dataPtr = Marshal.AllocHGlobal(size);
        RawInputInterop.GetRawInputData(hRawInput, RawInputInterop.RID_INPUT, dataPtr, ref size, headerSize);

        var header = Marshal.PtrToStructure<RawInputInterop.RAWINPUTHEADER>(dataPtr);

        DeviceInfo? device;
        lock (_devices)
        {
            if (!_devices.TryGetValue(header.hDevice, out device))
            {
                Marshal.FreeHGlobal(dataPtr);
                return;
            }
        }

        if (header.dwType == RawInputInterop.RIM_TYPEHID && device.Profile != null)
        {
            int dwSizeHid = Marshal.ReadInt32(dataPtr, headerSize);
            int dwCount = Marshal.ReadInt32(dataPtr, headerSize + 4);
            int dataOffset = headerSize + 8;
            
            for (int i = 0; i < dwCount; i++)
            {
                byte[] report = new byte[dwSizeHid];
                Marshal.Copy(dataPtr + dataOffset + (i * dwSizeHid), report, 0, dwSizeHid);
                
                var events = device.Profile.Decode(report, dwSizeHid, device);
                if (events != null)
                {
                    foreach (var ev in events)
                    {
                        _eventQueue.Enqueue(ev);
                    }
                }
            }
        }
        else if (header.dwType == RawInputInterop.RIM_TYPEMOUSE && device.Profile != null)
        {
            var mouse = Marshal.PtrToStructure<RawInputInterop.RAWMOUSE>(dataPtr + headerSize);
            var events = device.Profile.DecodeMouse(mouse, device);
            if (events != null)
            {
                foreach (var ev in events)
                {
                    _eventQueue.Enqueue(ev);
                }
            }
        }

        Marshal.FreeHGlobal(dataPtr);
    }
}
