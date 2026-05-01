# Arturia BeatStep Pro (`VID: 1C75`, `PID: 0287`) Mapping & Decoding

This document details how the Arturia BeatStep Pro is handled within the `RawInputMonitor` broker. The BeatStep Pro is a USB MIDI class-compliant device captured via direct `winmm.dll` P/Invoke (not `WM_INPUT`).

## 1. Device Identity & Connection

| Property | Value |
|----------|-------|
| **USB Vendor ID** | `1C75` (Arturia) |
| **USB Product ID** | `0287` |
| **Interface** | USB MIDI Class Compliant (2 ports) |
| **Capture Method** | `MidiManager.cs` via `winmm.dll` P/Invoke |

### USB MIDI Port Layout
The BeatStep Pro exposes **two** MIDI input ports over USB. Both are captured simultaneously.

| winmm Port | Windows Name | Purpose | Dashboard Label |
|------------|-------------|---------|-----------------|
| Port 0 | `Arturia BeatStep Pro` | Standard MIDI — pads, transport, knobs (CC mode) | `MIDI_Port1` |
| Port 1 | `MIDIIN2 (Arturia BeatStep Pro)` | MCU/HUI control surface — knobs (MCU mode), step buttons, fwd/bwd | `MIDI_Port2` |

> **Hardware Routing Rule**: Pads and Transport buttons ALWAYS output on Port 0, regardless of the active knob mode. This is firmware behavior and cannot be changed.

## 2. Current Status

### What is working ✅
- Both MIDI ports detected and listening via `winmm.dll` P/Invoke
- All 16 knobs captured in CC mode (absolute CC, 7-bit, Port 1)
- All 16 knobs captured in MCU mode (relative V-Pot + 14-bit PitchBend, Port 2)
- All 16 pads captured (Note On/Off, Port 1, both modes)
- Transport Play/Stop/Record captured (Port 1, both modes)
- Step buttons 1–16 captured in MCU mode (Note On/Off, Port 2)
- Step buttons 1–16 captured in CC mode (CC#102–117, Port 1) — **configured via MCC**
- **MIDI Output** (`midiOut`) implemented for LED Scene behavior in both modes
- Forward/Backward captured in MCU mode (Port 2)
- MIDI Clock (`0xF8`) and Active Sensing (`0xFE`) filtered out
- SysEx buffer prepared for long messages
- Dashboard shows Port 1 and Port 2 as separate device panels

### MCC Configuration Applied ✅
- Scene Mode: **OFF** (global setting)
- Step buttons 1–16: Mode = **CC**, Channel = **1**, CC# = **102–117**
- Play Mode: **Gate** (momentary) — host manages LED state via midiOut
- Off Value: **0**, On Value: **127**

### What needs implementation 🔧
- **Software-side banking** — offset tracking for virtual channel multiplication
- **MCU V-Pot relative delta handling** — accumulate relative values into absolute positions

## 3. Dual-Mode Strategy

Both CC and MCU modes are captured simultaneously. The mode switch (KNOBS button on device) only changes how the 16 rotary encoders and step buttons behave.

### Control routing per mode
| Feature | CC Mode (Port 1) | MCU Mode (Port 2) |
|---------|-----------------|-------------------|
| **Knobs 1–8** | 7-bit absolute CC (0–127) | Relative V-Pot (value 1=CW, 65=CCW, infinite rotation) |
| **Knobs 9–16** | 7-bit absolute CC (0–127) | 14-bit absolute PitchBend (0–16383) on Ch 1–8 |
| **Step Buttons 1–16** | CC#102–117 (configured via MCC, Gate mode) | Note On/Off |
| **Forward / Backward** | Not available | Note On/Off (MCU Bank Left/Right) |
| **Pads 1–16** | Note On/Off (always Port 1) | Note On/Off (always Port 1) |
| **Transport** | Play/Stop/Record (always Port 1) | Play/Stop/Record (always Port 1) |

### Software-Side Banking
Banking is purely a software offset tracked in `MidiManager`. Any button can be designated as a bank trigger:

```
effectiveChannel = physicalControl + (bankOffset × numberOfControls)
```

## 4. CC Mode Details (Port 1)

### Knobs
- Message: **Control Change** on Ch 1
- Default observed CC range: CC#10–CC#25
- Value range: 0–127 (absolute, 7-bit)

### Step Buttons (configured via MCC)
- Message: **Control Change** CC#102–117 on Ch 1
- Set to **Gate** mode (sends 127 on press, 0 on release)
- The midiOut Scene logic handles LED state, turning off the other 15 step LEDs when one is pressed.

### Pads
- Message: **Note On** (velocity 1–127) / **Note Off** (velocity 0)
- Note numbers depend on active sequencer and drum map
- Velocity-sensitive

### Transport
- **Play**: `0xFA` (Start) / `0xFB` (Continue)
- **Stop**: `0xFC`
- **Record**: Note On/Off or CC (configurable)

## 5. MCU Mode Details (Port 2)

### Knobs 1–8: V-Pots (Relative Encoding)
- Message: **Control Change** CC#16–CC#23 on Ch 1
- Value `1`–`63` = clockwise (1 = 1 click, higher = faster)
- Value `65`–`127` = counter-clockwise (65 = 1 click, higher = faster)
- Infinite rotation, no endpoints

### Knobs 9–16: Faders (14-bit Absolute)
- Message: **Pitch Bend** on Ch 1–8
- Range: 0–16383, center: 8192

### Step Buttons 1–16
- Message: **Note On** (vel 127) / **Note Off** (vel 0) on Port 2
- LEDs are host-controlled (require midiOut to light)

### Forward / Backward
- MCU Bank Left = Note 46, Bank Right = Note 47

## 6. MIDI Output Implementation — Scene LED Behavior

### Goal
When a step button is pressed, its LED lights up and all other step button LEDs turn off — replicating Scene mode's "radio button" behavior, while still sending MIDI values.

### How it works

The BeatStep Pro accepts incoming Note On/Off messages to control LED state on Port 1 (MCU mode):
- **Note On velocity 127** → LED on
- **Note On velocity 0** (or Note Off) → LED off

### Implementation in `MidiManager`

#### 1. MIDI Output P/Invoke in `MidiInterop.cs`
```csharp
[DllImport("winmm.dll")]
public static extern uint midiOutGetNumDevs();

[DllImport("winmm.dll", CharSet = CharSet.Auto)]
public static extern uint midiOutGetDevCaps(uint uDeviceID, ref MIDIOUTCAPS caps, uint cbMidiOutCaps);

[DllImport("winmm.dll")]
public static extern uint midiOutOpen(out IntPtr lphMidiOut, uint uDeviceID, IntPtr dwCallback, IntPtr dwCallbackInstance, uint dwFlags);

[DllImport("winmm.dll")]
public static extern uint midiOutShortMsg(IntPtr hMidiOut, uint dwMsg);

[DllImport("winmm.dll")]
public static extern uint midiOutClose(IntPtr hMidiOut);
```

#### 2. Open MIDI output device in `MidiManager.Start()`
- Enumerate `midiOutGetNumDevs()`, match "MIDIOUT2 (Arturia BeatStep Pro)"
- Open Port 1 output handle (`_outPort1Handle`)

#### 3. Scene LED logic in `MidiCallback`
When a step button message is received:
```
On step button N pressed:
  1. Send Note On (vel 127) for note N → light this LED
  2. For all other step buttons (1–16 except N):
     Send Note On (vel 0) → turn off their LEDs
  3. Track activeStepButton = N in software
```

#### 4. CC mode step buttons
- MCC Play Mode set to **Gate** (momentary)
- Gate sends CC 127 on press, CC 0 on release
- The midiOut Scene logic handles LED state
- On button press: light the pressed button, turn off the rest via Note On messages sent back to the device

#### 5. MCU mode step buttons
- Same logic — echo Note On back to the output port for LED control
- MCU protocol already expects this pattern

### Port mapping for output
| Output Port | Windows Name | Used For |
|-------------|-------------|----------|
| Port 1 | `MIDIOUT2 (Arturia BeatStep Pro)` | MCU mode step button LED control |

## 7. Controls That DO NOT Send MIDI

These buttons are firmware-internal and cannot be captured:

| Button | Function |
|--------|----------|
| SEQ1 / SEQ2 / DRUM | Track selection |
| CHAN | Channel assignment |
| PROJECT | Project save/load |
| ROLLER | Touch-strip mode |
| SWING / RANDOMNESS / PROBABILITY | Per-step parameters |
| STEP SIZE / LAST STEP | Sequence settings |
| SAVE / RECALL | Pattern memory |

## 8. SysEx Reference

Header: `F0 00 20 6B 7F 42 02 00 41 ... F7`

| Parameter | Byte | Values |
|-----------|------|--------|
| MCU / HUI | `0x0C` | `00` = MCU, `01` = HUI |
| Transport mode | `0x60` | `00` = OFF, `01` = MIDI, `02` = MMC, `03` = Both |
| Drum map | `0x27` | `00` = Custom, `01` = Spark, `02` = GM, `03` = Chromatic |
| Scene Mode | `0x0D` | `00` = OFF, `01` = ON |
| Seq 1 MIDI channel | `0x40` | `00`–`0F` |
| Seq 2 MIDI channel | `0x42` | `00`–`0F` |
| Drum MIDI channel | `0x44` | `00`–`0F` |
| User MIDI channel | `0x06` | `00`–`0F` |

Source: [Pl0p/Beatstep-pro-Sysex](https://github.com/Pl0p/Beatstep-pro-Sysex)
