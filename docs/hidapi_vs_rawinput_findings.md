# hidapi vs Windows Raw Input — Research Findings

## 1. Architecture Comparison

| Aspect | Windows Raw Input (`WM_INPUT`) | hidapi |
|--------|-------------------------------|--------|
| **Platform** | Windows only | Cross-platform (Win/Linux/macOS) |
| **Dependencies** | None (native Win32) | Native C library + P/Invoke or NuGet wrapper |
| **Data Direction** | Input only (read from device) | Bidirectional (read + write reports) |
| **Delivery Model** | Push (OS delivers via message pump) | Pull (app polls with `hid_read`) |
| **Latency** | Bypasses legacy message queue | Adds abstraction layer between app and HID stack |
| **Multi-device** | OS registers all devices automatically via handles | Must enumerate and open each device manually |
| **Report ID Handling** | OS may split HID collections into separate virtual devices | Receives raw reports exactly as hardware sends them |
| **Exclusive Access** | OS shares the device with system drivers | Windows HID stack does not support exclusive mode |
| **Cursor Suppression** | Cannot suppress (mouse-class devices move system cursor) | Cannot suppress — same underlying HID stack on Windows |
| **Thread Safety** | Message pump is single-threaded by design | Must manage threading for concurrent device reads |
| **Output/Feature Reports** | Not supported — input only | Supported — can send Output and Feature reports to device |

---

## 2. Per-Device Analysis

### 2.1 Tangent Wave 2 (`04D8:FDCF`)

| Factor | Raw Input | hidapi |
|--------|-----------|--------|
| **Report Format** | Vendor-defined, single collection — no split | Same reports, same bytes |
| **Output Reports** | Not possible | Possible (LED control, if device supports it) |
| **Collection Splitting** | None observed — single collection device | N/A |

### 2.2 3Dconnexion SpaceMouse (`256F:C652`)

| Factor | Raw Input | hidapi |
|--------|-----------|--------|
| **Collection Splitting** | Windows splits into 4 virtual devices (`MI_01&Col01`, `MI_01&Col02`, `MI_03&Col01`, `MI_03&Col02`) | Reads from single endpoint — no splitting |
| **Report Format** | Report ID 1, 13 bytes from `MI_01&Col02` only | Report ID 1, 13 bytes from single handle — matches `FTransRotReport` directly |
| **3DxService Conflict** | Must compete with virtual `3DXKMJ_HIDMINI` devices | Same conflict — 3DxService intercepts at driver level |
| **Reference** | N/A | [OpenUnrealSpaceMouse](https://github.com/microdee/OpenUnrealSpaceMouse) uses hidapi for this reason |

### 2.3 Kensington Slimblade Pro (`047D:80D4`)

| Factor | Raw Input | hidapi |
|--------|-----------|--------|
| **Input Type** | Mouse-class device — OS delivers pre-processed `RAWMOUSE` struct (deltas, button flags, scroll) | Raw HID mouse reports — must parse HID report descriptor manually |
| **Cursor Suppression** | Cannot suppress cursor movement | Cannot suppress — same HID stack on Windows |
| **Button/Scroll Parsing** | OS provides structured `usButtonFlags` and `usButtonData` | Must manually decode from raw HID report bytes |

### 2.4 MIDI Controllers (Planned)

| Factor | Raw Input | hidapi | Windows MIDI Services |
|--------|-----------|--------|-----------------------|
| **Protocol** | Cannot parse MIDI protocol | Can read USB-MIDI HID reports — must parse manually | Native MIDI 1.0 + 2.0 support |
| **Multi-client** | N/A | N/A | Multiple apps can share device simultaneously |
| **Latency** | N/A | Application-controlled polling | Optimized for real-time audio scheduling |
| **API** | N/A | N/A | `Microsoft.Windows.Devices.Midi2` or legacy `WinMM midiIn*` |
| **.NET Library** | N/A | N/A | `NAudio` NuGet package provides managed MIDI API |

> **Note**: Standard MIDI controllers use the USB-MIDI class specification, not generic HID. Some proprietary controllers (DJ gear, custom surfaces) may use vendor-defined HID reports instead.

---

## 3. .NET hidapi Libraries

| Package | NuGet | .NET Version | Notes |
|---------|-------|-------------|-------|
| **HidApi.Net** | `HidApi.Net` | .NET 8+ | Direct C bindings, requires native `hidapi.dll` |
| **HIDSharp** | `HIDSharp` | .NET Standard 2.0+ | Pure managed, cross-platform, includes report descriptor parsing |
| **Hid.Net** | `Hid.Net` | .NET Standard 2.0+ | Part of Device.Net framework, abstracts USB+HID |

---

## 4. Cursor Suppression Options (Mouse-Class Devices)

Neither Raw Input nor hidapi can suppress cursor movement for mouse-class HID devices on Windows. Known approaches:

| Approach | Complexity | Notes |
|----------|-----------|-------|
| **HidHide driver** | Moderate | Does not work for mouse/keyboard class devices (tested — access denied on `mouclass`-owned devices) |
| **Disable in Device Manager** | Easy | Stops all OS input from device entirely |
| **WinUSB via Zadig** | High | Replaces HID driver — device leaves the HID stack, must use `libusb`/`WinUSB` API for all communication |
| **Interception driver** | High | Kernel-level filter driver — intercepts and can suppress input before it reaches the OS input stack |
| **ClipCursor / SetCursorPos** | Low | Application-level workaround — confines or resets cursor position but does not truly suppress input |

---

## 5. Key Differences Summary

### What Raw Input provides that hidapi does not
- Push-based delivery via OS message pump (no polling loop needed)
- Pre-processed `RAWMOUSE` struct for mouse-class devices
- Automatic device registration and handle management
- Zero external dependencies

### What hidapi provides that Raw Input does not
- Cross-platform support (Linux, macOS)
- Bidirectional communication (Output and Feature reports)
- Raw report access without OS collection splitting
- Single-endpoint access to multi-collection devices (e.g., SpaceMouse)
