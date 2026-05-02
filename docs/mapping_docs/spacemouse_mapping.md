# 3Dconnexion SpaceMouse (`256F:C652`) Mapping & Decoding

This document serves as the permanent record for how the SpaceMouse's raw HID data is handled within the `RawInputMonitor` broker.

## 1. Device Identity

| Property | Value |
|----------|-------|
| **Vendor ID** | `256F` (3Dconnexion) / `046D` (Logitech legacy) |
| **Product ID** | `C652` (Universal Receiver / SpaceMouse Enterprise) |
| **Interface** | Raw HID (`RAWHID` via `WM_INPUT`) |
| **Usage Page** | `0x01` (Generic Desktop) |
| **Usage** | `0x08` (Multi-axis Controller) |
| **Report Format** | Single-report 6DOF (`FTransRotReport` convention) |

### Supported PIDs (Legacy Logitech VID `046D` only)
| PID | Model |
|-----|-------|
| `C626` | SpaceNavigator |
| `C627` | SpaceExplorer |
| `C628` | SpaceNavigator for Notebooks |
| `C629` | SpacePilot Pro |
| `C62A` | Legacy 3Dconnexion |
| `C62B` | SpaceMouse Pro |
| `C631` | SpaceMouse Pro Wireless (USB) |
| `C632` | SpaceMouse Pro Wireless Receiver |
| `C635` | SpaceMouse Compact |
| `C652` | SpaceMouse Pro V2 / Enterprise |

> **Note**: For VID `256F` (modern 3Dconnexion), all PIDs are accepted. The PID whitelist only applies to VID `046D` to avoid false positives on Logitech keyboards/mice.

## 2. Raw Input Ingestion

### Windows Raw Input vs hidapi

The SpaceMouse HID descriptor defines multiple Top-Level Collections. When using `hidapi` (as in [OpenUnrealSpaceMouse](https://github.com/microdee/OpenUnrealSpaceMouse)), the library reads from a single USB endpoint and receives raw reports exactly as the hardware sends them.

**Windows Raw Input (`WM_INPUT`)** splits the device into multiple virtual devices based on Top-Level Collections. On the C652 Universal Receiver, this produces 4 virtual device handles:

| Device Path Suffix | Interface | Class GUID | Purpose |
|---------------------|-----------|------------|---------|
| `MI_03&Col01` | Keyboard class | `{378de44c...}` | Not used for 6DOF |
| `MI_03&Col02` | HID class | `{4d1e55b2...}` | Not used for 6DOF |
| `MI_01&Col02` | HID class | `{4d1e55b2...}` | **Active 6DOF sensor data** |
| `MI_01&Col01` | HID class | `{4d1e55b2...}` | Button data / unused |

> **Verified**: All 6DOF sensor data arrives exclusively from `MI_01&Col02` as Report ID `1` with 13 bytes.

## 3. Report Structure

### Report ID 1 — 6DOF Axes (13 bytes, `FTransRotReport`)

This matches the `FSingleReportTransRotHidReader` / `FTransRotReport` struct from [OpenUnrealSpaceMouse](https://github.com/microdee/OpenUnrealSpaceMouse/blob/master/Source/SpaceMouseReader/Private/SpaceMouseReader/SingleReportTransRotHidReader.h).

| Byte | Field | Type | Range | Description |
|------|-------|------|-------|-------------|
| 0 | Report ID | `uint8` | `0x01` | Always `1` |
| 1–2 | TX | `int16 LE` | ±350 | X-axis translation (push left/right) |
| 3–4 | TY | `int16 LE` | ±350 | Y-axis translation (push forward/back) |
| 5–6 | TZ | `int16 LE` | ±350 | Z-axis translation (push up/down) |
| 7–8 | RX | `int16 LE` | ±350 | Pitch (tilt forward/back) |
| 9–10 | RY | `int16 LE` | ±350 | Roll (tilt left/right) |
| 11–12 | RZ | `int16 LE` | ±350 | Yaw (twist clockwise/counterclockwise) |

**Verified raw data example** (tilting the puck forward):
```
01-00-00-F1-FE-00-00-E8-FF-F1-FF-00-00
│  ├──┘  ├────┘  ├──┘  ├────┘  ├────┘  ├──┘
│  TX=0  TY=-271 TZ=0  RX=-24  RY=-15  RZ=0
└─ Report ID 1
```

### Report ID 3 — Buttons (variable length)

| Byte | Field | Type | Description |
|------|-------|------|-------------|
| 0 | Report ID | `uint8` | `0x03` |
| 1–N | Button Mask | Bitmask | Each bit = one button. `1` = pressed, `0` = released. Length varies by model (2–5 bytes). |

### Report ID 28 — Button Queue (Enterprise models)

Some Enterprise models use Report ID `28` with an 8-byte button queue instead of Report ID 3 bitmask. Both are handled.

### Legacy Separate-Report Devices (Report ID 1 = 7 bytes, Report ID 2 = 7 bytes)

Older SpaceMouse models (SpaceNavigator, SpaceExplorer, etc.) use the `FSeparateReportTransRotHidReader` convention where translation and rotation arrive in separate 7-byte reports:
- Report ID `1` (7 bytes): TX, TY, TZ
- Report ID `2` (7 bytes): RX, RY, RZ

The profile handles both formats by checking report length (13 vs 7).

## 4. Channel Mappings & Data Verification

### Continuous Controls (Axes)
| Control | Raw Report Field | Raw Value Example | Decoded Channel | Decoded Value |
|---------|-----------------|-------------------|-----------------|---------------|
| **Push Left/Right** | Bytes 1–2 of Report ID 1 | `-271` | `TX` | Same as Raw |
| **Push Forward/Back** | Bytes 3–4 of Report ID 1 | `+350` | `TY` | Same as Raw |
| **Push Up/Down** | Bytes 5–6 of Report ID 1 | `-120` | `TZ` | Same as Raw |
| **Tilt Forward/Back** | Bytes 7–8 of Report ID 1 | `-24` | `RX` | Same as Raw |
| **Tilt Left/Right** | Bytes 9–10 of Report ID 1 | `-15` | `RY` | Same as Raw |
| **Twist CW/CCW** | Bytes 11–12 of Report ID 1 | `+30` | `RZ` | Same as Raw |

### Discrete Controls (Buttons)
| Physical Button | Report ID 3 Bit | Decoded Channel | Decoded Value |
|-----------------|-----------------|-----------------|---------------|
| Button 1 (Left) | Bit 0 | `Button_1` | `1` (Down), `0` (Up) |
| Button 2 (Right) | Bit 1 | `Button_2` | `1` (Down), `0` (Up) |
| Button 3+ | Bit N | `Button_N+1` | `1` (Down), `0` (Up) |

## 5. Implementation Notes

- **Dead Zone**: Axis values within ±5 of zero are filtered to suppress sensor noise at rest.
- **Button Edge Detection**: Only state transitions (press/release) emit events, not continuous holds.
- **Driver Conflict**: The `3DxService` (3DxWare) creates virtual HID devices (`3DXKMJ_HIDMINI`) that spam the message pump. If raw data is jittery or missing, stop the service: `net stop 3DxService`.
- **Virtual Device Exclusion**: The `BEEF:046D` virtual device from 3DxWare is excluded by the VID/PID filter (VID `BEEF` is neither `256F` nor `046D`).
- **Reference Implementation**: Byte layout verified against [OpenUnrealSpaceMouse `FTransRotReport`](https://github.com/microdee/OpenUnrealSpaceMouse/blob/master/Source/SpaceMouseReader/Private/SpaceMouseReader/SingleReportTransRotHidReader.h).
