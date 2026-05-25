using System.Text;

namespace SerialMaster.Core.Services;

public enum SendMode
{
    Auto,
    Hex,
    Ascii,
    Utf8
}

public enum LineEnding
{
    None,
    CR,
    LF,
    CRLF
}

public static class InputParser
{
    private static readonly HashSet<char> HexChars = new("0123456789ABCDEFabcdef");

    public static byte[] Parse(string input, SendMode mode, LineEnding ending = LineEnding.None)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<byte>();

        byte[] payload = mode switch
        {
            SendMode.Hex => ParseHex(input),
            SendMode.Ascii => Encoding.ASCII.GetBytes(input),
            SendMode.Utf8 => Encoding.UTF8.GetBytes(input),
            SendMode.Auto => LooksLikeHex(input) ? ParseHex(input) : Encoding.UTF8.GetBytes(input),
            _ => Array.Empty<byte>()
        };

        byte[] tail = ending switch
        {
            LineEnding.CR => new byte[] { 0x0D },
            LineEnding.LF => new byte[] { 0x0A },
            LineEnding.CRLF => new byte[] { 0x0D, 0x0A },
            _ => Array.Empty<byte>()
        };

        if (tail.Length == 0) return payload;

        var result = new byte[payload.Length + tail.Length];
        Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
        Buffer.BlockCopy(tail, 0, result, payload.Length, tail.Length);
        return result;
    }

    public static byte[] ParseHex(string input)
    {
        var sb = new StringBuilder(input.Length);
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (c == '0' && i + 1 < input.Length && (input[i + 1] == 'x' || input[i + 1] == 'X'))
            {
                i += 2;
                continue;
            }
            if (HexChars.Contains(c)) sb.Append(c);
            i++;
        }

        if (sb.Length == 0) return Array.Empty<byte>();
        if (sb.Length % 2 == 1) sb.Insert(0, '0');

        var bytes = new byte[sb.Length / 2];
        for (int k = 0; k < bytes.Length; k++)
            bytes[k] = Convert.ToByte(sb.ToString(k * 2, 2), 16);
        return bytes;
    }

    public static bool LooksLikeHex(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        int hexDigits = 0;
        int total = 0;
        foreach (char c in input)
        {
            if (char.IsWhiteSpace(c) || c == '-' || c == ',' || c == ':') continue;
            total++;
            if (HexChars.Contains(c)) hexDigits++;
            else return false;
        }

        return total >= 2 && hexDigits == total;
    }
}
