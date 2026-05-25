namespace SerialMaster.UI.Models;

public class FrameField
{
    public string Name { get; set; } = string.Empty;
    public int Offset { get; set; }
    public int Length { get; set; } = 1;
    public string Type { get; set; } = "HEX";

    public string? Parse(byte[] frame)
    {
        if (Offset + Length > frame.Length) return null;
        var slice = frame.Skip(Offset).Take(Length).ToArray();

        return Type switch
        {
            "HEX" => BitConverter.ToString(slice).Replace("-", " "),
            "UInt8" => slice[0].ToString(),
            "UInt16LE" => BitConverter.ToUInt16(slice, 0).ToString(),
            "UInt16BE" => ((ushort)(slice[0] << 8 | slice[1])).ToString(),
            "UInt32LE" => BitConverter.ToUInt32(slice, 0).ToString(),
            "UInt32BE" => ((uint)(slice[0] << 24 | slice[1] << 16 | slice[2] << 8 | slice[3])).ToString(),
            "Int8" => ((sbyte)slice[0]).ToString(),
            "Int16LE" => BitConverter.ToInt16(slice, 0).ToString(),
            "Int16BE" => ((short)(slice[0] << 8 | slice[1])).ToString(),
            "Int32LE" => BitConverter.ToInt32(slice, 0).ToString(),
            "Int32BE" => ((int)(slice[0] << 24 | slice[1] << 16 | slice[2] << 8 | slice[3])).ToString(),
            "ASCII" => System.Text.Encoding.ASCII.GetString(slice),
            "FloatLE" => BitConverter.ToSingle(slice, 0).ToString("F3"),
            _ => BitConverter.ToString(slice).Replace("-", " ")
        };
    }
}
