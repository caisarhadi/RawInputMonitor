namespace RawInputMonitor.Core;

public record InputEvent
{
    public string DeviceId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string SourceType { get; init; } = "HID";
    public string Channel { get; init; } = string.Empty;
    public double Value { get; init; }
    public double RawValue { get; init; }
    public long Timestamp { get; init; }
}
