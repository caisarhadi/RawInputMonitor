# Tangent Wave 2 (`04D8:FDCF`) Mapping & Decoding

This document serves as the permanent record for how the Tangent Wave 2's raw HID report is handled within the `RawInputMonitor` broker.

## 1. Raw Input Ingestion
The Tangent Wave 2 uses a **vendor-defined HID Usage Page** (`0xF000`), meaning no standard HID labels exist for its controls. The OS delivers its data as a **26-byte `RAWHID` report** under the `RIM_TYPEHID` type.
Our core `DeviceManager` extracts the raw byte array and passes it to `TangentWaveProfile.Decode()`.

### Decoding Strategy
The profile maintains two lookup dictionaries to avoid brittle conditional chains:
- **`_axisMap`** (`Dictionary<int, string>`): Maps a byte index to a continuous channel name.
- **`_buttonMap`** (`Dictionary<string, string>`): Maps a `Bit[Byte:Bit]` coordinate to a discrete button name.

A state-diffing loop compares each incoming 26-byte report against the previous report. Only bytes that have changed are processed:
- If the changed byte index exists in `_axisMap`, the raw unsigned byte is cast to a **signed byte** (`sbyte`) via Two's Complement to produce a relative delta (e.g., `255` → `-1`).
- If the changed byte index is *not* in `_axisMap`, individual bit changes are inspected against `_buttonMap`.
- Any unmapped byte or bit changes are still emitted as generic `Byte[NN]` or `Bit[NN:N]` channels, ensuring universal capture.

## 2. Channel Mappings

### Continuous Controls (Axes)
All axis controls report **relative deltas** (signed movement from the last position), not absolute values.
- Clockwise / Right / Up → positive values (e.g., `1`, `2`, `3`)
- Counter-clockwise / Left / Down → Two's Complement wrapping values (e.g., `255` → `-1`, `254` → `-2`)

#### Trackballs (3×)
Three optical trackballs, each with independent X/Y relative axes.

| Control | Byte Index | Raw Value | Decoded Channel | Decoded Value |
|---------|-----------|-----------|-----------------|---------------|
| **Trackball 1 X** | `Byte[20]` | `0x00`–`0xFF` | `Trackball_1_X` | Signed delta (e.g., `-5` to `+5`) |
| **Trackball 1 Y** | `Byte[21]` | `0x00`–`0xFF` | `Trackball_1_Y` | Signed delta |
| **Trackball 2 X** | `Byte[22]` | `0x00`–`0xFF` | `Trackball_2_X` | Signed delta |
| **Trackball 2 Y** | `Byte[23]` | `0x00`–`0xFF` | `Trackball_2_Y` | Signed delta |
| **Trackball 3 X** | `Byte[24]` | `0x00`–`0xFF` | `Trackball_3_X` | Signed delta |
| **Trackball 3 Y** | `Byte[25]` | `0x00`–`0xFF` | `Trackball_3_Y` | Signed delta |

#### JogWheels (4×)
Three master dials (one physically surrounding each trackball) plus one transport jog dial.

| Control | Byte Index | Raw Value | Decoded Channel | Decoded Value |
|---------|-----------|-----------|-----------------|---------------|
| **JogWheel 1** | `Byte[16]` | `0x00`–`0xFF` | `JogWheel_1` | Signed delta |
| **JogWheel 2** | `Byte[17]` | `0x00`–`0xFF` | `JogWheel_2` | Signed delta |
| **JogWheel 3** | `Byte[18]` | `0x00`–`0xFF` | `JogWheel_3` | Signed delta |
| **JogWheel 4** (Transport) | `Byte[19]` | `0x00`–`0xFF` | `JogWheel_4` | Signed delta |

#### Rotary Knobs (9×)
Nine programmable rotary encoders with integral push-to-reset switches (buttons mapped separately below).

| Control | Byte Index | Raw Value | Decoded Channel | Decoded Value |
|---------|-----------|-----------|-----------------|---------------|
| **Rotary Knob 1** | `Byte[07]` | `0x00`–`0xFF` | `RotaryKnob_1` | Signed delta |
| **Rotary Knob 2** | `Byte[08]` | `0x00`–`0xFF` | `RotaryKnob_2` | Signed delta |
| **Rotary Knob 3** | `Byte[09]` | `0x00`–`0xFF` | `RotaryKnob_3` | Signed delta |
| **Rotary Knob 4** | `Byte[10]` | `0x00`–`0xFF` | `RotaryKnob_4` | Signed delta |
| **Rotary Knob 5** | `Byte[11]` | `0x00`–`0xFF` | `RotaryKnob_5` | Signed delta |
| **Rotary Knob 6** | `Byte[12]` | `0x00`–`0xFF` | `RotaryKnob_6` | Signed delta |
| **Rotary Knob 7** | `Byte[13]` | `0x00`–`0xFF` | `RotaryKnob_7` | Signed delta |
| **Rotary Knob 8** | `Byte[14]` | `0x00`–`0xFF` | `RotaryKnob_8` | Signed delta |
| **Rotary Knob 9** | `Byte[15]` | `0x00`–`0xFF` | `RotaryKnob_9` | Signed delta |

### Discrete Controls (Buttons)
All buttons are packed as **bitmasks** across bytes 2–6 of the report. When a bit flips to `1`, the decoder broadcasts `1` (Pressed). When it flips to `0`, it broadcasts `0` (Released).

#### Modifier & Trackball Controls (Bytes 2–4)
| Physical Button | Bit Coordinate | Decoded Channel | Decoded Value |
|-----------------|----------------|-----------------|---------------|
| **Alt** | `Bit[02:1]` | `Button_Alt` | `1` (Down), `0` (Up) |
| **TB1 Left** | `Bit[02:2]` | `Button_TB1_Left` | `1` / `0` |
| **TB1 Center** | `Bit[02:3]` | `Button_TB1_Center` | `1` / `0` |
| **TB1 Right** | `Bit[02:4]` | `Button_TB1_Right` | `1` / `0` |
| **TB1 Reset Ball** | `Bit[02:5]` | `Button_TB1_ResetBall` | `1` / `0` |
| **TB1 Reset Wheel** | `Bit[02:6]` | `Button_TB1_ResetWheel` | `1` / `0` |
| **TB2 Left** | `Bit[02:7]` | `Button_TB2_Left` | `1` / `0` |
| **TB2 Center** | `Bit[03:0]` | `Button_TB2_Center` | `1` / `0` |
| **TB2 Right** | `Bit[03:1]` | `Button_TB2_Right` | `1` / `0` |
| **TB2 Reset Ball** | `Bit[03:2]` | `Button_TB2_ResetBall` | `1` / `0` |
| **TB2 Reset Wheel** | `Bit[03:3]` | `Button_TB2_ResetWheel` | `1` / `0` |
| **TB3 Left** | `Bit[03:4]` | `Button_TB3_Left` | `1` / `0` |
| **TB3 Center** | `Bit[03:5]` | `Button_TB3_Center` | `1` / `0` |
| **TB3 Right** | `Bit[03:6]` | `Button_TB3_Right` | `1` / `0` |
| **TB3 Reset Ball** | `Bit[03:7]` | `Button_TB3_ResetBall` | `1` / `0` |
| **TB3 Reset Wheel** | `Bit[04:0]` | `Button_TB3_ResetWheel` | `1` / `0` |

#### Navigation Buttons (Byte 4)
| Physical Button | Bit Coordinate | Decoded Channel | Decoded Value |
|-----------------|----------------|-----------------|---------------|
| **Up** | `Bit[04:1]` | `Button_Up` | `1` / `0` |
| **Down** | `Bit[04:2]` | `Button_Down` | `1` / `0` |

#### Function Buttons (Bytes 4–5)
| Physical Button | Bit Coordinate | Decoded Channel | Decoded Value |
|-----------------|----------------|-----------------|---------------|
| **Fn7** | `Bit[04:3]` | `Button_Fn7` | `1` / `0` |
| **Fn8** | `Bit[04:4]` | `Button_Fn8` | `1` / `0` |
| **Fn9** | `Bit[04:5]` | `Button_Fn9` | `1` / `0` |
| **Fn4** | `Bit[04:6]` | `Button_Fn4` | `1` / `0` |
| **Fn5** | `Bit[04:7]` | `Button_Fn5` | `1` / `0` |
| **Fn6** | `Bit[05:0]` | `Button_Fn6` | `1` / `0` |
| **Fn1** | `Bit[05:1]` | `Button_Fn1` | `1` / `0` |
| **Fn2** | `Bit[05:2]` | `Button_Fn2` | `1` / `0` |
| **Fn3** | `Bit[05:3]` | `Button_Fn3` | `1` / `0` |

#### Transport Buttons (Bytes 5–6)
| Physical Button | Bit Coordinate | Decoded Channel | Decoded Value |
|-----------------|----------------|-----------------|---------------|
| **Fast Rewind** | `Bit[05:4]` | `Button_Trans_FastRewind` | `1` / `0` |
| **Fast Forward** | `Bit[05:5]` | `Button_Trans_FastForward` | `1` / `0` |
| **Play Reverse** | `Bit[05:6]` | `Button_Trans_PlayReverse` | `1` / `0` |
| **Stop** | `Bit[05:7]` | `Button_Trans_Stop` | `1` / `0` |
| **Play Forward** | `Bit[06:0]` | `Button_Trans_PlayForward` | `1` / `0` |

## 3. Report Byte Map (Summary)

A visual summary of how each byte in the 26-byte report is allocated:

| Byte Index | Allocation |
|-----------|------------|
| `0–1` | Reserved / Report header |
| `2` | Buttons: Alt, TB1 Left/Center/Right, TB1 ResetBall/ResetWheel, TB2 Left |
| `3` | Buttons: TB2 Center/Right, TB2 ResetBall/ResetWheel, TB3 Left/Center/Right, TB3 ResetBall |
| `4` | Buttons: TB3 ResetWheel, Up, Down, Fn7/Fn8/Fn9, Fn4/Fn5 |
| `5` | Buttons: Fn6, Fn1/Fn2/Fn3, Transport FastRewind/FastForward/PlayReverse/Stop |
| `6` | Buttons: Transport PlayForward (bit 0); remaining bits unused |
| `7–15` | Rotary Knobs 1–9 (relative delta) |
| `16–19` | JogWheels 1–4 (relative delta) |
| `20–25` | Trackballs 1–3 X/Y (relative delta) |

## 4. Future Enhancement: LCD Output

The Tangent Wave 2 has 3× OLED displays. A future update will implement HID Output Reports using `kernel32.dll` (`CreateFile` / `WriteFile`) or `HidD_SetOutputReport` to write text strings to the physical displays.
