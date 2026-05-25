using SerialMaster.Core.Models;
using System.Text;

namespace SerialMaster.Core.Services;

/// <summary>
/// SLCAN (Serial Line CAN, aka LAWICEL/CANUSB protocol) codec.
/// Frame format (ASCII, terminated with CR = 0x0D):
///   tIIILDD...  standard data frame (3-hex ID, 1-hex DLC, DLC*2 hex data)
///   TIIIIIIIILDD... extended data frame (8-hex ID)
///   rIIIL       standard RTR
///   RIIIIIIIIL  extended RTR
/// Commands: Snn (bitrate), O (open), C (close), b/B (transmit), etc.
/// </summary>
public sealed class SlcanCodec
{
    private readonly StringBuilder _rxLine = new(64);

    public static readonly IReadOnlyList<(int Index, int Kbps, string Cmd)> BitrateTable = new[]
    {
        (0, 10,   "S0"),
        (1, 20,   "S1"),
        (2, 50,   "S2"),
        (3, 100,  "S3"),
        (4, 125,  "S4"),
        (5, 250,  "S5"),
        (6, 500,  "S6"),
        (7, 800,  "S7"),
        (8, 1000, "S8")
    };

    /// <summary>Feed bytes received from the SLCAN adapter, yielding any complete frames.</summary>
    public IEnumerable<CanFrame> Decode(byte[] data)
    {
        if (data == null || data.Length == 0) yield break;

        for (int i = 0; i < data.Length; i++)
        {
            char c = (char)data[i];
            if (c == '\r')
            {
                if (_rxLine.Length > 0)
                {
                    var line = _rxLine.ToString();
                    _rxLine.Clear();
                    if (TryParseLine(line, out var frame))
                        yield return frame;
                }
            }
            else if (c >= 0x20 && c < 0x7F)
            {
                _rxLine.Append(c);
                if (_rxLine.Length > 64) _rxLine.Clear();  // overflow safety
            }
            // ignore everything else (LF, NUL, etc.)
        }
    }

    public void Reset() => _rxLine.Clear();

    public static bool TryParseLine(string line, out CanFrame frame)
    {
        frame = null!;
        if (string.IsNullOrEmpty(line)) return false;

        try
        {
            switch (line[0])
            {
                case 't': return TryParseDataFrame(line, idHexLen: 3, CanFrameKind.Standard, out frame);
                case 'T': return TryParseDataFrame(line, idHexLen: 8, CanFrameKind.Extended, out frame);
                case 'r': return TryParseRtrFrame(line, idHexLen: 3, CanFrameKind.StandardRtr, out frame);
                case 'R': return TryParseRtrFrame(line, idHexLen: 8, CanFrameKind.ExtendedRtr, out frame);
                default: return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseDataFrame(string line, int idHexLen, CanFrameKind kind, out CanFrame frame)
    {
        frame = null!;
        if (line.Length < 1 + idHexLen + 1) return false;

        uint id = Convert.ToUInt32(line.Substring(1, idHexLen), 16);
        int dlc = HexNibble(line[1 + idHexLen]);
        if (dlc < 0 || dlc > 8) return false;

        int dataStart = 1 + idHexLen + 1;
        if (line.Length < dataStart + dlc * 2) return false;

        var bytes = new byte[dlc];
        for (int i = 0; i < dlc; i++)
            bytes[i] = Convert.ToByte(line.Substring(dataStart + i * 2, 2), 16);

        frame = new CanFrame
        {
            Timestamp = DateTime.Now,
            Kind = kind,
            Id = id,
            Dlc = (byte)dlc,
            Data = bytes
        };
        return true;
    }

    private static bool TryParseRtrFrame(string line, int idHexLen, CanFrameKind kind, out CanFrame frame)
    {
        frame = null!;
        if (line.Length < 1 + idHexLen + 1) return false;

        uint id = Convert.ToUInt32(line.Substring(1, idHexLen), 16);
        int dlc = HexNibble(line[1 + idHexLen]);
        if (dlc < 0 || dlc > 8) return false;

        frame = new CanFrame
        {
            Timestamp = DateTime.Now,
            Kind = kind,
            Id = id,
            Dlc = (byte)dlc,
            Data = Array.Empty<byte>()
        };
        return true;
    }

    private static int HexNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };

    /// <summary>Encode a frame to a SLCAN ASCII line (no trailing CR).</summary>
    public static string Encode(CanFrame frame)
    {
        char prefix = frame.Kind switch
        {
            CanFrameKind.Standard    => 't',
            CanFrameKind.Extended    => 'T',
            CanFrameKind.StandardRtr => 'r',
            CanFrameKind.ExtendedRtr => 'R',
            _ => 't'
        };

        int idLen = frame.IsExtended ? 8 : 3;
        var sb = new StringBuilder();
        sb.Append(prefix);
        sb.Append(frame.Id.ToString(idLen == 8 ? "X8" : "X3"));
        sb.Append(Math.Min(frame.Dlc, (byte)8).ToString("X"));

        if (!frame.IsRtr)
        {
            int count = Math.Min(frame.Dlc, frame.Data.Length);
            for (int i = 0; i < count; i++)
                sb.Append(frame.Data[i].ToString("X2"));
        }
        return sb.ToString();
    }

    /// <summary>Encode + append CR + return UTF8 bytes ready for serial write.</summary>
    public static byte[] EncodeWithTerminator(CanFrame frame)
        => Encoding.ASCII.GetBytes(Encode(frame) + "\r");
}
