using SerialMaster.Core.Models;
using System.Text;

namespace SerialMaster.Core.Services;

/// <summary>
/// Streaming protocol parser. Feed arbitrary byte chunks via <see cref="Feed"/>;
/// it accumulates them in an internal buffer, syncs on the header (if any),
/// and emits one ParsedFrame per complete frame.
/// </summary>
public sealed class ProtocolParser
{
    private readonly List<byte> _buffer = new();
    private byte[] _headerBytes = Array.Empty<byte>();
    private ProtocolDefinition _definition = new();

    public ProtocolDefinition Definition
    {
        get => _definition;
        set
        {
            _definition = value ?? new ProtocolDefinition();
            _headerBytes = InputParser.ParseHex(_definition.HeaderHex);
            _buffer.Clear();
        }
    }

    public void Reset() => _buffer.Clear();

    public IEnumerable<ParsedFrame> Feed(byte[] data)
    {
        if (data == null || data.Length == 0) return Array.Empty<ParsedFrame>();
        _buffer.AddRange(data);
        return DrainFrames();
    }

    private IEnumerable<ParsedFrame> DrainFrames()
    {
        int frameLen = _definition.FrameLength;
        if (frameLen <= 0) yield break;

        while (true)
        {
            int frameStart = FindFrameStart();
            if (frameStart < 0)
            {
                TrimBufferBeforePotentialHeaderStart();
                yield break;
            }

            if (_buffer.Count - frameStart < frameLen)
            {
                if (frameStart > 0) _buffer.RemoveRange(0, frameStart);
                yield break;
            }

            var raw = new byte[frameLen];
            for (int i = 0; i < frameLen; i++) raw[i] = _buffer[frameStart + i];

            _buffer.RemoveRange(0, frameStart + frameLen);

            yield return ParseFrame(raw);
        }
    }

    private int FindFrameStart()
    {
        if (_headerBytes.Length == 0) return 0;
        if (_buffer.Count < _headerBytes.Length) return -1;

        for (int i = 0; i <= _buffer.Count - _headerBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < _headerBytes.Length; j++)
            {
                if (_buffer[i + j] != _headerBytes[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    private void TrimBufferBeforePotentialHeaderStart()
    {
        if (_headerBytes.Length == 0)
        {
            // No header — keep at most one frame's worth to allow next Feed to complete.
            int keep = Math.Max(_definition.FrameLength, 1);
            if (_buffer.Count > keep)
                _buffer.RemoveRange(0, _buffer.Count - keep);
            return;
        }

        // Keep at most (header length - 1) bytes; the last (n-1) could be a partial header.
        int retain = _headerBytes.Length - 1;
        if (_buffer.Count > retain)
            _buffer.RemoveRange(0, _buffer.Count - retain);
    }

    private ParsedFrame ParseFrame(byte[] raw)
    {
        var values = new List<ParsedFieldValue>(_definition.Fields.Count);
        foreach (var field in _definition.Fields)
        {
            values.Add(ParseField(field, raw));
        }
        return new ParsedFrame(DateTime.Now, raw, values);
    }

    private static ParsedFieldValue ParseField(ProtocolField field, byte[] frame)
    {
        if (field.Offset < 0 || field.Length <= 0 ||
            field.Offset + field.Length > frame.Length)
        {
            return new ParsedFieldValue(field.Name, "(超出帧范围)", isError: true);
        }

        var slice = new ReadOnlySpan<byte>(frame, field.Offset, field.Length);
        try
        {
            var text = FormatField(field.Type, slice);
            double? numeric = TryNumeric(field.Type, slice);
            return new ParsedFieldValue(field.Name, text, isError: false,
                numericValue: numeric, waveformChannel: field.WaveformChannel);
        }
        catch (Exception ex)
        {
            return new ParsedFieldValue(field.Name, $"(错误: {ex.Message})", isError: true);
        }
    }

    private static double? TryNumeric(FieldType type, ReadOnlySpan<byte> slice)
    {
        switch (type)
        {
            case FieldType.UInt8:     return slice.Length >= 1 ? slice[0] : null;
            case FieldType.Int8:      return slice.Length >= 1 ? (sbyte)slice[0] : null;
            case FieldType.UInt16LE:  return slice.Length >= 2 ? BitConverter.ToUInt16(slice) : null;
            case FieldType.UInt16BE:  return slice.Length >= 2 ? (ushort)((slice[0] << 8) | slice[1]) : null;
            case FieldType.Int16LE:   return slice.Length >= 2 ? BitConverter.ToInt16(slice) : null;
            case FieldType.Int16BE:   return slice.Length >= 2 ? (short)((slice[0] << 8) | slice[1]) : null;
            case FieldType.UInt32LE:  return slice.Length >= 4 ? BitConverter.ToUInt32(slice) : null;
            case FieldType.UInt32BE:  return slice.Length >= 4 ? (uint)((slice[0] << 24) | (slice[1] << 16) | (slice[2] << 8) | slice[3]) : null;
            case FieldType.Int32LE:   return slice.Length >= 4 ? BitConverter.ToInt32(slice) : null;
            case FieldType.Int32BE:   return slice.Length >= 4 ? (int)((slice[0] << 24) | (slice[1] << 16) | (slice[2] << 8) | slice[3]) : null;
            case FieldType.Float32LE: return slice.Length >= 4 ? BitConverter.ToSingle(slice) : null;
            case FieldType.Float32BE:
                if (slice.Length < 4) return null;
                Span<byte> rev = stackalloc byte[4] { slice[3], slice[2], slice[1], slice[0] };
                return BitConverter.ToSingle(rev);
            default: return null;
        }
    }

    private static string FormatField(FieldType type, ReadOnlySpan<byte> slice)
    {
        switch (type)
        {
            case FieldType.Hex:
                return Convert.ToHexString(slice).Insert2(' ');
            case FieldType.UInt8:
                EnsureLen(slice, 1); return slice[0].ToString();
            case FieldType.Int8:
                EnsureLen(slice, 1); return ((sbyte)slice[0]).ToString();
            case FieldType.UInt16LE:
                EnsureLen(slice, 2); return BitConverter.ToUInt16(slice).ToString();
            case FieldType.UInt16BE:
                EnsureLen(slice, 2); return ((ushort)((slice[0] << 8) | slice[1])).ToString();
            case FieldType.Int16LE:
                EnsureLen(slice, 2); return BitConverter.ToInt16(slice).ToString();
            case FieldType.Int16BE:
                EnsureLen(slice, 2); return ((short)((slice[0] << 8) | slice[1])).ToString();
            case FieldType.UInt32LE:
                EnsureLen(slice, 4); return BitConverter.ToUInt32(slice).ToString();
            case FieldType.UInt32BE:
                EnsureLen(slice, 4); return ((uint)((slice[0] << 24) | (slice[1] << 16) | (slice[2] << 8) | slice[3])).ToString();
            case FieldType.Int32LE:
                EnsureLen(slice, 4); return BitConverter.ToInt32(slice).ToString();
            case FieldType.Int32BE:
                EnsureLen(slice, 4); return ((int)((slice[0] << 24) | (slice[1] << 16) | (slice[2] << 8) | slice[3])).ToString();
            case FieldType.Float32LE:
                EnsureLen(slice, 4); return BitConverter.ToSingle(slice).ToString("F4");
            case FieldType.Float32BE:
            {
                EnsureLen(slice, 4);
                Span<byte> rev = stackalloc byte[4] { slice[3], slice[2], slice[1], slice[0] };
                return BitConverter.ToSingle(rev).ToString("F4");
            }
            case FieldType.Ascii:
                return Encoding.ASCII.GetString(slice).TrimEnd('\0');
            default:
                return Convert.ToHexString(slice);
        }
    }

    private static void EnsureLen(ReadOnlySpan<byte> slice, int min)
    {
        if (slice.Length < min) throw new ArgumentException($"need at least {min} bytes, got {slice.Length}");
    }
}

internal static class HexFormat
{
    public static string Insert2(this string s, char sep)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= 2) return s;
        var sb = new StringBuilder(s.Length + s.Length / 2);
        for (int i = 0; i < s.Length; i += 2)
        {
            if (i > 0) sb.Append(sep);
            sb.Append(s, i, 2);
        }
        return sb.ToString();
    }
}
