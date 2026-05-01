# Task: Kensington Slimblade Pro Integration

This document outlines the steps to integrate the **Kensington Slimblade Pro** (VID `047D`) into the RawInputMonitor pipeline.

## 1. Win32 Interop Adjustments
- [x] Expand `RAWINPUT` struct in `Win32/RawInputInterop.cs` to explicitly include the `RAWMOUSE` structure.
- [x] Verify `MessageWindow.cs` is correctly registering Usage Page `0x01`, Usage `0x02` (Mouse).

## 2. Core Decoding Logic
- [x] Update `Core/DeviceManager.cs` to handle `RIM_TYPEMOUSE` (Type 0).
- [x] Update `IDeviceProfile.cs` to define `DecodeMouse(RAWMOUSE mouse, DeviceInfo device)`.
- [x] Route `RAWMOUSE` reports from the `DeviceManager` into the `DecodeMouse` function.

## 3. Slimblade Pro Profile
- [x] Create `Profiles/SlimbladeProfile.cs` implementing `IDeviceProfile`.
- [x] Implement `CanHandle` for VID `047D`.
- [x] Implement `DecodeMouse`:
  - Extract relative `LastX` and `LastY` coordinates (Trackball pan).
  - Extract `ButtonFlags` for the 4 physical buttons.
  - Extract vertical `Wheel` data (Trackball twist).
- [x] Emit normalized `InputEvent` channels: `X`, `Y`, `Scroll`, and `Button1` - `Button4`.

## 4. Verification
- [x] Ensure physical Twist action correctly outputs as `Scroll`.
- [x] Ensure the 4 buttons independently trigger correctly without bleeding into X/Y.
