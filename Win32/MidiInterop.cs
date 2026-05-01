using System;
using System.Runtime.InteropServices;

namespace RawInputMonitor.Win32;

/// <summary>
/// P/Invoke declarations for winmm.dll MIDI Input functions.
/// Zero-dependency, same approach as the rest of the project.
/// </summary>
public static class MidiInterop
{
    // --- Return codes ---
    public const uint MMSYSERR_NOERROR = 0;

    // --- Callback flags ---
    public const uint CALLBACK_FUNCTION = 0x00030000;

    // --- MIDI Input Messages (sent to callback) ---
    public const int MIM_OPEN = 0x3C1;
    public const int MIM_CLOSE = 0x3C2;
    public const int MIM_DATA = 0x3C3;
    public const int MIM_LONGDATA = 0x3C4;
    public const int MIM_ERROR = 0x3C5;
    public const int MIM_LONGERROR = 0x3C6;
    public const int MIM_MOREDATA = 0x3CC;

    // --- Structures ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MIDIINCAPS
    {
        public ushort wMid;          // Manufacturer ID
        public ushort wPid;          // Product ID
        public uint vDriverVersion;  // Driver version
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;       // Product name (up to 31 chars + null)
        public uint dwSupport;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MIDIOUTCAPS
    {
        public ushort wMid;          // Manufacturer ID
        public ushort wPid;          // Product ID
        public uint vDriverVersion;  // Driver version
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;       // Product name (up to 31 chars + null)
        public ushort wTechnology;
        public ushort wVoices;
        public ushort wNotes;
        public ushort wChannelMask;
        public uint dwSupport;
    }

    // --- Delegate for MIDI input callback ---
    public delegate void MidiInProc(IntPtr hMidiIn, int wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    // --- Functions ---
    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInGetNumDevs();

    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint midiInGetDevCaps(uint uDeviceID, ref MIDIINCAPS lpMidiInCaps, uint cbMidiInCaps);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInOpen(out IntPtr lphMidiIn, uint uDeviceID, MidiInProc dwCallback, IntPtr dwCallbackInstance, uint dwFlags);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInStart(IntPtr hMidiIn);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInStop(IntPtr hMidiIn);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInClose(IntPtr hMidiIn);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInReset(IntPtr hMidiIn);

    // --- SysEx / Long message support ---
    [StructLayout(LayoutKind.Sequential)]
    public struct MIDIHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public IntPtr lpNext;
        public IntPtr reserved;
        public uint dwOffset;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public IntPtr[] dwReserved;
    }

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInPrepareHeader(IntPtr hMidiIn, IntPtr lpMidiInHdr, uint cbMidiInHdr);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInUnprepareHeader(IntPtr hMidiIn, IntPtr lpMidiInHdr, uint cbMidiInHdr);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiInAddBuffer(IntPtr hMidiIn, IntPtr lpMidiInHdr, uint cbMidiInHdr);

    // --- MIDI Output Functions ---
    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiOutGetNumDevs();

    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint midiOutGetDevCaps(uint uDeviceID, ref MIDIOUTCAPS lpMidiOutCaps, uint cbMidiOutCaps);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiOutOpen(out IntPtr lphMidiOut, uint uDeviceID, IntPtr dwCallback, IntPtr dwCallbackInstance, uint dwFlags);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiOutShortMsg(IntPtr hMidiOut, uint dwMsg);

    [DllImport("winmm.dll", SetLastError = true)]
    public static extern uint midiOutClose(IntPtr hMidiOut);
}
