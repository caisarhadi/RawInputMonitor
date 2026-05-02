# Kensington Slimblade Pro (`047D:80D4`) Mapping & Decoding

This document serves as the permanent record for how the Slimblade Pro's raw mouse input is handled within the `RawInputMonitor` broker.

## 1. Raw Input Ingestion
Unlike the Tangent Wave 2 (which uses `RAWHID`), the Slimblade Pro is a standard Mouse-class device. The OS delivers its data via the `RAWMOUSE` struct under the `RIM_TYPEMOUSE` type.
Our core `DeviceManager` extracts the `RAWMOUSE` payload and passes it to `SlimbladeProfile.DecodeMouse()`.

## 2. Channel Mappings & Data Verification
The device emits standard relative mouse movement and standard bitwise button flags. Below is the verified mapping table showing how raw `RAWMOUSE` struct data is ingested and decoded into frontend values.

### Continuous Controls (Axes)
| Control | Raw Input Source (`RAWMOUSE`) | Raw Value Example | Decoded Channel | Decoded Value |
|---------|-------------------------------|-------------------|-----------------|---------------|
| **Trackball X** | `mouse.lLastX` (Relative Delta) | `-5` to `+5` | `X` | Same as Raw (`-5` to `+5`) |
| **Trackball Y** | `mouse.lLastY` (Relative Delta) | `-5` to `+5` | `Y` | Same as Raw (`-5` to `+5`) |
| **Twist_Scroll** | `mouse.usButtonData` (when `RI_MOUSE_WHEEL` is flagged) | `120` or `-120` | `Twist_Scroll` | `1` (Right/Up) or `-1` (Left/Down) |

*Velocity Note*: Unlike proprietary jogwheels that accumulate velocity dynamically, the Slimblade acts as a standard mouse wheel. It natively emits rapid streams of delta events (`WHEEL_DELTA` = `120` or `-120`). Our decoder normalizes this by dividing by `120.0`.

### Discrete Controls (Buttons)
The 5 physical hardware buttons map to the `usButtonFlags` bitmask. When pressed (Down), they output `1`. When released (Up), they output `0`.

| Physical Button | Raw Input Flag (`usButtonFlags`) | Hex Mask (Down / Up) | Decoded Channel | Decoded Value |
|-----------------|----------------------------------|----------------------|-----------------|---------------|
| **Bottom Left** | `RI_MOUSE_LEFT_BUTTON` | `0x0001` / `0x0002` | `Button_BottomLeft` | `1` (Down), `0` (Up) |
| **Bottom Right**| `RI_MOUSE_RIGHT_BUTTON`| `0x0004` / `0x0008` | `Button_BottomRight`| `1` (Down), `0` (Up) |
| **Top Left**    | `RI_MOUSE_MIDDLE_BUTTON` (Button 3) | `0x0010` / `0x0020` | `Button_TopLeft` | `1` (Down), `0` (Up) |
| **Top Right**   | `RI_MOUSE_BUTTON_4` (Back Button) | `0x0040` / `0x0080` | `Button_TopRight` | `1` (Down), `0` (Up) |
| **Extra**       | `RI_MOUSE_BUTTON_5` (Forward Button) | `0x0100` / `0x0200` | `Button_Extra` | `1` (Down), `0` (Up) |

