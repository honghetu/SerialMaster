using System.Text.Json.Serialization;

namespace SerialMaster.Core.Models;

public enum FieldType
{
    Hex,
    UInt8,
    Int8,
    UInt16LE,
    UInt16BE,
    Int16LE,
    Int16BE,
    UInt32LE,
    UInt32BE,
    Int32LE,
    Int32BE,
    Float32LE,
    Float32BE,
    Ascii
}

public class ProtocolField
{
    public string Name { get; set; } = string.Empty;
    public int Offset { get; set; }
    public int Length { get; set; } = 1;
    public FieldType Type { get; set; } = FieldType.Hex;

    /// <summary>
    /// If set (0-based), every parsed value of this field is pushed to the corresponding
    /// waveform channel. Null = not displayed on waveform.
    /// </summary>
    public int? WaveformChannel { get; set; }
}

public class ProtocolDefinition
{
    public string Name { get; set; } = string.Empty;

    /// <summary>HEX string (e.g. "AA 55"). Empty = no header sync, frames are size-based from offset 0.</summary>
    public string HeaderHex { get; set; } = string.Empty;

    /// <summary>Fixed total frame length (including header). 0 = not fixed (rare; currently only fixed mode supported).</summary>
    public int FrameLength { get; set; }

    public List<ProtocolField> Fields { get; set; } = new();
}

public class ParsedFrame
{
    public DateTime Timestamp { get; }
    public byte[] RawBytes { get; }
    public IReadOnlyList<ParsedFieldValue> FieldValues { get; }

    public ParsedFrame(DateTime timestamp, byte[] rawBytes, IReadOnlyList<ParsedFieldValue> values)
    {
        Timestamp = timestamp;
        RawBytes = rawBytes;
        FieldValues = values;
    }
}

public class ParsedFieldValue
{
    public string Name { get; }
    public string Value { get; }
    public bool IsError { get; }

    /// <summary>Numeric value when the field is a numeric type. Null when error or non-numeric (e.g. Hex/Ascii).</summary>
    public double? NumericValue { get; }

    public int? WaveformChannel { get; }

    public ParsedFieldValue(string name, string value, bool isError = false,
        double? numericValue = null, int? waveformChannel = null)
    {
        Name = name;
        Value = value;
        IsError = isError;
        NumericValue = numericValue;
        WaveformChannel = waveformChannel;
    }
}
