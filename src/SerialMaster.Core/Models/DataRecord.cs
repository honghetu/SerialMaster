namespace SerialMaster.Core.Models;

public record DataRecord(
    DateTime Timestamp,
    DataDirection Direction,
    byte[] Data,
    RecordStatus Status = RecordStatus.Success
);
