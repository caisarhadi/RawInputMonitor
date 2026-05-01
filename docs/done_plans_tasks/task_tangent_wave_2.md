# Task: Scaffold Project & Tangent Wave 2 Integration

This document outlines the focused steps to initialize the `RawInputMonitor` project and implement the core Raw Input pipeline specifically for the **Tangent Wave 2** (VID `04D8`, PID `FDCF`).

## 1. Project Scaffolding
- [x] Initialize .NET 8.0 Console Application (`RawInputMonitor.csproj`).
- [x] Create project directories (`Win32`, `Core`, `Profiles`, `Server`, `Dashboard`).

## 2. Win32 Interop & Core Pipeline
- [x] Create `Win32/RawInputInterop.cs` (P/Invoke for Raw Input).
- [x] Create `Win32/HidPInterop.cs` (P/Invoke for HID).
- [x] Create `Win32/MessageWindow.cs` (Hidden window and message pump).
- [x] Create `Core/DeviceInfo.cs` and `Core/InputEvent.cs`.
- [x] Create `Core/DeviceManager.cs` (Device enumeration and hotplug).

## 3. Tangent Wave 2 Focus
- [x] Create `Profiles/IDeviceProfile.cs`.
- [x] Create `Profiles/TangentWaveProfile.cs`.
  - [x] Implement raw hex ingestion.
  - [x] Execute `tangent_wave_2_mapping_plan.md` to map specific byte-offsets:
    - Trackballs (3)
    - Dials (4)
    - Knobs (9)
    - Buttons & Transport controls (32)

## 4. WebSocket & Dashboard Scaffolding
- [x] Create `Server/WebSocketServer.cs` (Localhost WebSocket server).
- [x] Create basic `Dashboard/index.html`, `Dashboard/style.css`, and `Dashboard/app.js`.
- [x] Integrate components in `Program.cs`.
