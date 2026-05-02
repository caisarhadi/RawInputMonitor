using System;
using System.Runtime.InteropServices;
using RawInputMonitor.Core;

namespace RawInputMonitor.Win32;

public class MessageWindow : IDisposable
{
    private IntPtr _hwnd;
    private RawInputInterop.WndProc _wndProcDelegate;
    private readonly DeviceManager _deviceManager;

    public MessageWindow(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
        _wndProcDelegate = CustomWndProc;

        RawInputInterop.WNDCLASSEX wndClass = new RawInputInterop.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf(typeof(RawInputInterop.WNDCLASSEX)),
            lpfnWndProc = _wndProcDelegate,
            lpszClassName = "RawInputMessageWindowClass",
            hInstance = Marshal.GetHINSTANCE(typeof(MessageWindow).Module)
        };

        RawInputInterop.RegisterClassEx(ref wndClass);

        _hwnd = RawInputInterop.CreateWindowEx(
            0,
            wndClass.lpszClassName,
            "RawInputMessageWindow",
            0, 0, 0, 0, 0,
            RawInputInterop.HWND_MESSAGE,
            IntPtr.Zero,
            wndClass.hInstance,
            IntPtr.Zero);

        RegisterRawInput();
    }

    private void RegisterRawInput()
    {
        var list = new System.Collections.Generic.List<RawInputInterop.RAWINPUTDEVICE>();
        
        void AddDevice(ushort page, ushort usage) {
            list.Add(new RawInputInterop.RAWINPUTDEVICE {
                usUsagePage = page,
                usUsage = usage,
                dwFlags = RawInputInterop.RIDEV_INPUTSINK | RawInputInterop.RIDEV_DEVNOTIFY,
                hwndTarget = _hwnd
            });
        }
        
        AddDevice(0x01, 0x02); // Mouse
        AddDevice(0x01, 0x04); // Joystick
        AddDevice(0x01, 0x05); // Gamepad
        AddDevice(0x01, 0x08); // Multi-axis
        
        // Vendor pages
        AddDevice(0xF000, 0x01); // Common vendor defined
        AddDevice(0xFF00, 0x01); // Another common vendor
        AddDevice(0xFF0A, 0x01); 
        
        RawInputInterop.RegisterRawInputDevices(list.ToArray(), list.Count, Marshal.SizeOf(typeof(RawInputInterop.RAWINPUTDEVICE)));
    }

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == RawInputInterop.WM_INPUT)
        {
            _deviceManager.ProcessRawInput(lParam);
        }
        else if (msg == RawInputInterop.WM_INPUT_DEVICE_CHANGE)
        {
            _deviceManager.ProcessDeviceChange(wParam, lParam);
        }

        return RawInputInterop.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Run()
    {
        while (RawInputInterop.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            RawInputInterop.TranslateMessage(ref msg);
            RawInputInterop.DispatchMessage(ref msg);
        }
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            RawInputInterop.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }
}
