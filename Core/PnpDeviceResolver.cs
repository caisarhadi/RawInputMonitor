using System;
using System.Runtime.InteropServices;
using RawInputMonitor.Win32;

namespace RawInputMonitor.Core;

public static class PnpDeviceResolver
{
    private static readonly Guid[] _interfaceGuids = new[]
    {
        new Guid("4d1e55b2-f16f-11cf-88cb-001111000030"), // HID
        new Guid("378de44c-56ef-11d1-bc8c-00a0c91405dd"), // Mouse
        new Guid("884b96c3-56ef-11d1-bc8c-00a0c91405dd")  // Keyboard
    };

    /// <summary>
    /// Resolves a raw input device path (\\?\HID#...) to a human-readable PnP name.
    /// Traverses up the device tree to find specific manufacturer names, bypassing generic "HID Keyboard Device" strings.
    /// </summary>
    public static string? GetDeviceNameFromPath(string devicePath)
    {
        IntPtr deviceInfoSet = SetupApiInterop.SetupDiGetClassDevs(
            IntPtr.Zero,
            null,
            IntPtr.Zero,
            SetupApiInterop.DIGCF_ALLCLASSES | SetupApiInterop.DIGCF_PRESENT | SetupApiInterop.DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == new IntPtr(-1))
            return null;

        try
        {
            var interfaceData = new SetupApiInterop.SP_DEVICE_INTERFACE_DATA();
            interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

            foreach (var guid in _interfaceGuids)
            {
                uint memberIndex = 0;
                Guid currentGuid = guid;

                while (SetupApiInterop.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref currentGuid, memberIndex, ref interfaceData))
                {
                    memberIndex++;

                    uint requiredSize = 0;
                    SetupApiInterop.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

                    if (requiredSize == 0) continue;

                    IntPtr detailDataPtr = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detailDataPtr, IntPtr.Size == 8 ? 8 : 5);

                        var devInfoData = new SetupApiInterop.SP_DEVINFO_DATA();
                        devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

                        if (SetupApiInterop.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailDataPtr, requiredSize, out _, ref devInfoData))
                        {
                            string currentPath = Marshal.PtrToStringAuto(detailDataPtr + 4) ?? string.Empty;

                            if (string.Equals(currentPath, devicePath, StringComparison.OrdinalIgnoreCase))
                            {
                                return GetBestNameFromDevNode(deviceInfoSet, devInfoData);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailDataPtr);
                    }
                }
            }
        }
        finally
        {
            SetupApiInterop.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return null;
    }

    private static string? GetBestNameFromDevNode(IntPtr hDevInfo, SetupApiInterop.SP_DEVINFO_DATA devInfoData)
    {
        uint currentDevInst = devInfoData.DevInst;
        string? bestName = null;

        // Walk up the tree up to 4 levels looking for a non-generic name
        for (int i = 0; i < 4; i++)
        {
            string? name = GetRegistryProperty(hDevInfo, devInfoData, SetupApiInterop.SPDRP_FRIENDLYNAME);
            if (string.IsNullOrEmpty(name))
            {
                name = GetRegistryProperty(hDevInfo, devInfoData, SetupApiInterop.SPDRP_DEVICEDESC);
            }

            if (!string.IsNullOrEmpty(name) && !IsGenericName(name))
            {
                bestName = name;
                break; // Found a good name
            }

            // If we only found a generic name (like "HID Keyboard Device"), store it but keep looking up the tree
            if (string.IsNullOrEmpty(bestName) && !string.IsNullOrEmpty(name))
            {
                bestName = name;
            }

            // Move to parent
            int cr = SetupApiInterop.CM_Get_Parent(out uint parentDevInst, currentDevInst, 0);
            if (cr != SetupApiInterop.CR_SUCCESS)
                break;

            currentDevInst = parentDevInst;
            devInfoData.DevInst = currentDevInst;
        }

        return bestName;
    }

    private static string? GetRegistryProperty(IntPtr hDevInfo, SetupApiInterop.SP_DEVINFO_DATA devInfoData, uint property)
    {
        SetupApiInterop.SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfoData, property, out _, null, 0, out uint reqSize);
        if (reqSize == 0) return null;

        byte[] buffer = new byte[reqSize];
        if (SetupApiInterop.SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfoData, property, out _, buffer, (uint)buffer.Length, out _))
        {
            return System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }
        return null;
    }

    public static bool IsGenericName(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        string lower = name.ToLowerInvariant();
        return lower.Contains("hid keyboard") || 
               lower.Contains("hid-compliant") || 
               lower.Contains("usb input device") || 
               lower.Contains("usb composite device");
    }
}
