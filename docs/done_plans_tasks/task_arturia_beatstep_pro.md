## Phase 1: MIDI Output P/Invoke
- `[x]` **Add midiOut P/Invoke to MidiInterop.cs**
  - `[x]` Add `MIDIOUTCAPS` struct
  - `[x]` Add `midiOutGetNumDevs()`
  - `[x]` Add `midiOutGetDevCaps()`
  - `[x]` Add `midiOutOpen()`
  - `[x]` Add `midiOutShortMsg()`
  - `[x]` Add `midiOutClose()`

## Phase 2: Output Port Management in MidiManager
- `[x]` **Enumerate and open output ports**
  - `[x]` Add `_outPort0Handle` and `_outPort1Handle` fields
  - `[x]` Add `_activeStepButton` state tracker (int, -1 = none)
  - `[x]` In `Start()`, scan `midiOutGetNumDevs()` after input setup
  - `[x]` Match "Arturia BeatStep Pro" → Port 0 output
  - `[x]` Match "MIDIOUT2 (Arturia BeatStep Pro)" → Port 1 output
  - `[x]` Open both with `midiOutOpen()`
  - `[x]` Log discovery results to console

## Phase 3: Scene LED Logic
- `[x]` **Implement `SendNoteOn()` helper**
  - `[x]` Pack 3-byte MIDI message as uint
  - `[x]` Call `midiOutShortMsg()` on correct port handle
- `[x]` **Implement `HandleSceneLed()` method**
  - `[x]` Detect step button press from MCU mode (Port 2, Note On note 0–15)
  - `[x]` Detect step button press from CC mode (Port 1, CC#102–117 val=127)
  - `[x]` Light pressed button LED (Note On vel=127)
  - `[x]` Turn off all other step button LEDs (Note On vel=0)
  - `[x]` Update `_activeStepButton` state
- `[x]` **Wire `HandleSceneLed()` into `MidiCallback`**
  - `[x]` Call from `HandleShortMessage()` after identifying step button events
  - `[x]` Route to correct output port based on input source port

## Phase 4: Cleanup
- `[x]` **Dispose output handles**
  - `[x]` Add `midiOutClose()` calls in `Dispose()`
  - `[x]` Null-check handles before closing

## Phase 5: Documentation Updates
- `[x]` **Update arturia_beatstep_pro_mapping.md**
  - `[x]` Mark MIDI Output as ✅ in Status section
  - `[x]` Update CC mode Step Button section — Gate mode is now active
  - `[x]` Update Section 6 — mark Scene LED plan as implemented

## Phase 6: Verification
- `[x]` `dotnet build` — no compilation errors
- `[x]` Run with BeatStep Pro connected — output ports discovered
- `[x]` Test CC mode step buttons → LED radio-button behavior
- `[x]` Test MCU mode step buttons → LED radio-button behavior
- `[x]` Verify no regression on pads, knobs, transport

## Phase 7: Code Audit & Cleanup
- `[x]` Remove dead MIDI decoding methods in `ArturiaBeatStepProProfile.cs`
- `[x]` Remove unused `_outPort0Handle` from `MidiManager.cs` (hardware ignores MIDI input for CC mode LEDs)
- `[x]` Fix stale SysEx terminology and PID mismatch in `README.md`
- `[x]` Fix Gate/Toggle mode contradiction in mapping docs
