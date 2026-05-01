# Tangent Wave 2: Mapping & Decoding Plan

This document outlines the systematic approach to translating the Tangent Wave 2's raw 26-byte HID report into human-readable, normalized control channels.

## 1. The Mapping Dictionary Strategy
To avoid a massive block of brittle `if/else` statements, the `TangentWaveProfile` will utilize explicit lookup dictionaries:
- **`AxisDictionary`**: Maps raw `Byte` indices to continuous channel names.
  - *Example:* `[3] -> "Trackball_1_X"`
- **`ButtonDictionary`**: Maps exact `Bit` coordinates (Byte:Bit) to discrete button names.
  - *Example:* `[21, 3] -> "Button_Transport_Play"`

## 2. Decoding Logic

### Continuous Controls (Axes & Dials)
Trackballs, jogwheels, and rotary knobs send **relative** deltas (movement from the last position), not absolute values.
- Movement right/up typically reports as `1`, `2`, `3`.
- Movement left/down typically reports as Two's Complement wrapping values like `255`, `254`.
- **Action**: When a mapped Axis byte changes, the decoder will convert it from an unsigned byte into a signed integer and broadcast the named channel (e.g., `JogWheel_Main: -1`).

### Discrete Controls (Buttons)
The console packs the state of up to 8 buttons into a single byte as a **bitmask**.
- **Action**: When a mapped Button bit flips to `1`, the decoder broadcasts `"ButtonName": 1.0` (Pressed). When it flips to `0`, it broadcasts `"ButtonName": 0.0` (Released).

## 3. Collaborative Mapping Execution
Since the hardware is operated by the user, we will map the console incrementally through the dashboard:
1. **Target a Control Group**: Select a physical section (e.g., "The 3 Trackballs").
2. **Observe Raw Input**: The user interacts with the physical hardware, and the dashboard flashes the raw `Byte` or `Bit` ID.
3. **Log the ID**: The user provides the raw ID mapping for the control.
4. **Update Dictionary**: The ID is permanently added to the backend dictionaries.
5. **Validate**: The dashboard instantly stops showing the raw ID and replaces it with the newly mapped human-readable channel name.

*Next immediate step: Map the Trackball X/Y axes.*
