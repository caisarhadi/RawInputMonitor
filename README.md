# RawInputMonitor

A high-performance, zero-dependency C# .NET 8 application designed to capture raw Human Interface Device (HID) input directly via the Windows Raw Input API. It bypasses OS-level exclusive access conflicts, decodes raw reports into normalized channels, and streams them to a real-time browser dashboard over WebSockets.

## Project Documentation

To avoid duplication, detailed information is split across the following documents:

- **[Architecture Overview](docs/architecture.md)**: Details the Win32 message pump, threading model, and core components.
- **[Tangent Wave 2 Mapping](docs/mapping_docs/tangent_wave_2_mapping.md)**: Full byte-offset and bitmask mapping for the 26-byte vendor-defined HID report.
- **[Slimblade Pro Mapping](docs/mapping_docs/slimblade_pro_mapping.md)**: Raw input ingestion and channel mapping for the standard mouse-class Slimblade Pro.
- **[SpaceMouse Mapping](docs/mapping_docs/spacemouse_mapping.md)**: Multi-report HID decoding for 6DOF translation/rotation axes and buttons.

## Core Features

- **Zero-Driver Dependency**: Interfaces directly with `user32.dll` and `hid.dll` using pure Win32 P/Invoke.
- **Hardware Agnostic**: Monitors generic HID devices by default, dynamically parsing bitmasks to isolate and identify buttons and axes.
- **Custom Device Profiles**: Specifically tailored decoding profiles for professional hardware to map vendor-defined byte offsets.
- **Bit-Level Ingestion**: Dynamically parses bitmasks to isolate and identify exact button inputs encoded within raw bytes.
- **Real-Time Web Dashboard**: A zero-configuration HTML/JS interface that visualizes hardware data streams via WebSockets (`ws://localhost:9100`).

## Supported Devices

| Device | VID:PID | Status |
|--------|---------|--------|
| **Tangent Wave 2** | `04D8:FDCF` | Fully Mapped (Trackballs, Jogwheels, Knobs, Buttons) |
| **Kensington Slimblade Pro** | `047D:*` | Fully Mapped (Trackball, Twist-Scroll, 4 Buttons) |
| **3Dconnexion SpaceMouse** | `046D:*` / `256F:*` | Fully Mapped (6DOF Translation, Rotation, Buttons) |

## Known Limitations

- **OS Cursor Movement**: Standard mouse-class devices (e.g., the Slimblade Pro) inherently move the Windows OS cursor. This behavior cannot be cleanly suppressed via the Raw Input API without disabling legacy mouse input system-wide. The application will successfully capture the raw data, but the system cursor will still track the movement.

## Future Enhancements

- **Tangent Wave 2 LCD Integration**: Send text strings to the physical OLED displays via `kernel32.dll`.
- **MIDI Device Support**: Expand the event model to accommodate MIDI input sources alongside HID.

## Getting Started

1. Clone this repository.
2. Build the project using the .NET 8 SDK: `dotnet build`.
3. Run the executable.
4. Open `http://localhost:9100` in your web browser to access the live dashboard.
