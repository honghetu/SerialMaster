using System.Text;

namespace SerialMaster.Core.Services;

public enum ChecksumType
{
    XOR,
    ADD,
    CRC8,
    CRC16,
    CRC32,
    ModbusCRC
}

public static class ChecksumService
{
    public static string Compute(string input, ChecksumType type)
    {
        byte[] data = ParseInput(input);
        return type switch
        {
            ChecksumType.XOR => ComputeXor(data),
            ChecksumType.ADD => ComputeAdd(data),
            ChecksumType.CRC8 => ComputeCrc8(data),
            ChecksumType.CRC16 => ComputeCrc16(data),
            ChecksumType.CRC32 => ComputeCrc32(data),
            ChecksumType.ModbusCRC => ComputeModbusCrc(data),
            _ => "00"
        };
    }

    private static byte[] ParseInput(string input)
    {
        input = input.Trim();
        return InputParser.LooksLikeHex(input)
            ? InputParser.ParseHex(input)
            : Encoding.UTF8.GetBytes(input);
    }

    private static string ComputeXor(byte[] data)
    {
        byte result = 0;
        foreach (var b in data) result ^= b;
        return result.ToString("X2");
    }

    private static string ComputeAdd(byte[] data)
    {
        int sum = 0;
        foreach (var b in data) sum += b;
        return (sum & 0xFF).ToString("X2");
    }

    private static string ComputeCrc8(byte[] data)
    {
        byte crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
        }
        return crc.ToString("X2");
    }

    private static string ComputeCrc16(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x0001) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        return crc.ToString("X4");
    }

    private static string ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return (~crc).ToString("X8");
    }

    private static string ComputeModbusCrc(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x0001) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        byte lowByte = (byte)(crc & 0xFF);
        byte highByte = (byte)(crc >> 8);
        return $"{lowByte:X2} {highByte:X2}";
    }
}
