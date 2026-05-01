# Implement MIDI Output for Arturia BeatStep Pro — MCU & CC Mode LED Control

The goal is to add **MIDI output** (`midiOut`) to `MidiManager` so the broker can send Note On/Off messages back to the BeatStep Pro to control step-button LEDs. This enables **Scene-like radio-button behavior** in both MCU and CC modes, where pressing a step button lights its LED and turns off all others.

The step buttons have already been configured in MCC as **Gate** mode (momentary: CC 127 on press, CC 0 on release). The LED state will be fully host-controlled — the device firmware no longer manages it.

## User Review Required

> [!IMPORTANT]
> **NuGet dependency**: No new dependency required. All MIDI output will use the same `winmm.dll` P/Invoke approach already established for MIDI input.

> [!IMPORTANT]
> **Step button Gate assumption**: The plan assumes all 16 step buttons are already configured to **Gate** mode (momentary) via Arturia MCC. If any are still in Toggle mode, the LED state tracking may mismatch.

> [!WARNING]
> **Output port names**: The plan assumes Windows names are `"Arturia BeatStep Pro"` (Port 0) and `"MIDIOUT2 (Arturia BeatStep Pro)"` (Port 1). These will be discovered at runtime via `midiOutGetDevCaps`. If naming differs, the match logic will fall back to partial string matching on `"Arturia"` and `"BeatStep"`.

## Proposed Changes

### Win32 Interop Layer

#### [MODIFY] MidiInterop.cs

Add MIDI **Output** P/Invoke declarations alongside existing MIDI Input ones:

| Function | Purpose |
|----------|---------|
| `midiOutGetNumDevs()` | Enumerate output device count |
| `midiOutGetDevCaps()` | Get output device name/caps |
| `midiOutOpen()` | Open output port handle |
| `midiOutShortMsg()` | Send a 3-byte MIDI message (Note On/Off, CC) |
| `midiOutClose()` | Close output port on shutdown |

Also add the `MIDIOUTCAPS` struct (mirrors `MIDIINCAPS` but for output ports).

---

### Core Data Pump

#### [MODIFY] MidiManager.cs

**1. Output Port Discovery & Opening** (in `Start()`):
- After opening input devices, enumerate `midiOutGetNumDevs()`
- Match `"Arturia BeatStep Pro"` → store as `_outPort0` (CC mode LED control)
- Match `"MIDIOUT2 (Arturia BeatStep Pro)"` → store as `_outPort1` (MCU mode LED control)
- Open both with `midiOutOpen()`

**2. Scene LED Logic** — new method `HandleSceneLed()`:
- When a step button press is detected (via callback):
  - **MCU mode** (Port 2 input, Note On): Send Note On vel=127 for the pressed button back to output Port 1, send Note On vel=0 for all other 15 step buttons
  - **CC mode** (Port 1 input, CC#102–117 val=127): Send Note On vel=127 for the pressed button back to output Port 0, send Note On vel=0 for all other 15 step buttons
- Track `_activeStepButton` (int, 1–16) in software to avoid redundant sends

**3. Helper method `SendNoteOn()`**:
- Packs a Note On message: `(0x90 | channel) | (note << 8) | (velocity << 16)`
- Calls `midiOutShortMsg()` on the appropriate output port handle

**4. Dispose cleanup**:
- Add `midiOutClose()` for both output handles in `Dispose()`

**Port mapping summary**:

| Direction | Port | Windows Name | Usage |
|-----------|------|-------------|-------|
| Input | 0 | `Arturia BeatStep Pro` | CC mode knobs, pads, transport, step buttons (CC#102–117) |
| Input | 1 | `MIDIIN2 (Arturia BeatStep Pro)` | MCU mode knobs (V-Pot/PitchBend), step buttons (Note), fwd/bwd |
| **Output** | **0** | **`Arturia BeatStep Pro`** | **CC mode step button LED control** |
| **Output** | **1** | **`MIDIOUT2 (Arturia BeatStep Pro)`** | **MCU mode step button LED control** |

---

### Documentation

#### [MODIFY] arturia_beatstep_pro_mapping.md

- Update section 2 "Current Status" to mark MIDI Output as ✅ implemented
- Update section 4 CC mode to reflect Gate mode as active (not pending)
- Update section 6 to reflect completed implementation (not future plan)

---

## MIDI Output Message Reference

### Step Button Note Numbers

The BeatStep Pro uses these note numbers for step button LEDs:

| MCU Mode (Port 2) | CC Mode (Port 1) |
|---|---|
| Step buttons use Note 0–15 (Ch 1) for both input and LED feedback | Step buttons use CC#102–117 for input, but LED control uses Note 0–15 (Ch 1) sent back on output Port 0 |

> [!NOTE]
> Both modes use the same LED addressing scheme: **Note On velocity 127** = LED on, **Note On velocity 0** (or Note Off) = LED off. The note number for each step button is `stepIndex - 1` (Step 1 = Note 0, Step 16 = Note 15), confirmed from the mapping doc Section 5.

### Short Message Packing (winmm)

```
uint msg = (uint)(statusByte | (noteNumber << 8) | (velocity << 16));
midiOutShortMsg(hMidiOut, msg);
```

Example — Light Step 5 LED, turn off all others:
```
Port 0 Out: 0x90 | (4 << 8) | (127 << 16)   → Step 5 ON
Port 0 Out: 0x90 | (0 << 8) | (0 << 16)     → Step 1 OFF
Port 0 Out: 0x90 | (1 << 8) | (0 << 16)     → Step 2 OFF
... (repeat for 3,5–15)
```

## Verification Plan

### Automated Tests
- `dotnet build` — confirm no compilation errors
- Run application with BeatStep Pro connected
- Console log verification: confirm output port discovery messages

### Manual Verification
- Press step button in CC mode → observe single LED lights, others off
- Press step button in MCU mode → observe single LED lights, others off
- Switch between CC and MCU mode on device → confirm LED state resets cleanly
- Verify pads, knobs, transport still function normally (no regression)
