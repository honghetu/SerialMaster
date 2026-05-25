namespace SerialMaster.Core.Models;

public enum CanFrameKind
{
    Standard,       // 11-bit ID, data frame
    Extended,       // 29-bit ID, data frame
    StandardRtr,    // 11-bit ID, remote request
    ExtendedRtr     // 29-bit ID, remote request
}

public class CanFrame
{
    public DateTime Timestamp { get; init; }
    public CanFrameKind Kind { get; init; }
    public uint Id { get; init; }
    public byte Dlc { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();

    public bool IsExtended => Kind is CanFrameKind.Extended or CanFrameKind.ExtendedRtr;
    public bool IsRtr => Kind is CanFrameKind.StandardRtr or CanFrameKind.ExtendedRtr;
}
