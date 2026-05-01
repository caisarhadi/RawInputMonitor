# RawInputMonitor

A high-performance, ground-up C# application designed to capture **all** Human Interface Device (HID) input reports via the Windows Raw Input API. It bypasses OS-level exclusive access conflicts, decodes raw reports into human-readable channels, and streams them to a real-time browser dashboard over WebSockets.

## Core Features
- **Zero-Driver Dependency**: Interfaces directly with `user32.dll` and `hid.dll` using pure Win32 P/Invoke.
- **Hardware Agnostic**: Monitors generic HID devices by default, automatically identifying buttons and axes.
- **Custom Device Profiles**: Specifically tailored decoding profiles for professional hardware (e.g., Tangent Wave 2, 3Dconnexion SpaceMouse).
- **Bit-Level Ingestion**: Dynamically parses bitmasks to isolate and identify exact button inputs encoded within raw bytes.
- **Real-Time Web Dashboard**: A zero-configuration HTML/JS interface that visualizes hardware data streams via WebSockets (`localhost:9100`).

## Architecture
- **Win32 Message Pump**: A hidden window captures `WM_INPUT` efficiently without requiring window focus.
- **Device Manager**: Enumerates devices, handles hot-plugging, and routes data.
- **Profile Decoder**: Converts raw hex arrays into normalized `InputEvent` records.
- **WebSocket Broadcaster**: Streams event JSON to connected web clients.

## Supported Devices
- [x] **Tangent Wave 2** (`04D8:FDCF`): Fully mapped (Trackballs, Jogwheels, Rotary Knobs, 32 Buttons).
- [x] **Kensington Slimblade Pro** (`047D:*`): Fully mapped (Trackball, Twist-Scroll, 4 Buttons).
- [ ] **3Dconnexion SpaceMouse**: Pending.

## Hardware Configuration Notes
### Mouse Cursor Suppression (HidHide)
Devices that act as standard mice (like the Slimblade Pro) will naturally move the Windows OS cursor. To suppress this behavior while allowing `RawInputMonitor` to read the data, use the **HidHide** driver to hide the device from the OS, and whitelist `RawInputMonitor.exe` in the HidHide configuration.

### Future Enhancements
- **Tangent Wave 2 LCD Integration**: To send text strings to the Tangent's physical LCD displays, a future update will utilize `kernel32.dll` (`CreateFile` / `WriteFile`) to stream "Output Reports" to the device handle acquired by the `DeviceManager`. (Requires proprietary driver uninstallation to obtain `GENERIC_WRITE` access).
