using SerialMaster.Core.Models;
using System.Text.Json;

namespace SerialMaster.Core.Services;

public interface IProtocolDefinitionStore
{
    IReadOnlyList<ProtocolDefinition> LoadAll();
    void SaveAll(IEnumerable<ProtocolDefinition> definitions);
    string FilePath { get; }
}

public sealed class ProtocolDefinitionStore : IProtocolDefinitionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public string FilePath { get; }

    public ProtocolDefinitionStore() : this(DefaultPath()) { }

    public ProtocolDefinitionStore(string filePath)
    {
        FilePath = filePath;
    }

    private static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SerialMaster");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "protocols.json");
    }

    public IReadOnlyList<ProtocolDefinition> LoadAll()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var list = JsonSerializer.Deserialize<List<ProtocolDefinition>>(json, JsonOptions);
                // Empty list on disk is a valid user state (deleted all definitions); don't override.
                return list ?? new List<ProtocolDefinition>();
            }
        }
        catch { }

        // Only seed built-ins when no file exists yet (first run).
        return BuiltInDefinitions();
    }

    public void SaveAll(IEnumerable<ProtocolDefinition> definitions)
    {
        var list = definitions.ToList();
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(list, JsonOptions));
    }

    public static IReadOnlyList<ProtocolDefinition> BuiltInDefinitions() => new[]
    {
        new ProtocolDefinition
        {
            Name = "示例: 6 字节传感器帧 (AA 55 + u16 LE 温度 + u16 LE 湿度)",
            HeaderHex = "AA 55",
            FrameLength = 6,
            Fields =
            {
                new ProtocolField { Name = "Header",   Offset = 0, Length = 2, Type = FieldType.Hex },
                new ProtocolField { Name = "Temp x10", Offset = 2, Length = 2, Type = FieldType.UInt16LE },
                new ProtocolField { Name = "Humid x10", Offset = 4, Length = 2, Type = FieldType.UInt16LE }
            }
        },
        new ProtocolDefinition
        {
            Name = "示例: 8 字节 IMU 帧 (FF FE + 6×Int16 BE)",
            HeaderHex = "FF FE",
            FrameLength = 14,
            Fields =
            {
                new ProtocolField { Name = "Header", Offset = 0,  Length = 2, Type = FieldType.Hex },
                new ProtocolField { Name = "Ax",     Offset = 2,  Length = 2, Type = FieldType.Int16BE },
                new ProtocolField { Name = "Ay",     Offset = 4,  Length = 2, Type = FieldType.Int16BE },
                new ProtocolField { Name = "Az",     Offset = 6,  Length = 2, Type = FieldType.Int16BE },
                new ProtocolField { Name = "Gx",     Offset = 8,  Length = 2, Type = FieldType.Int16BE },
                new ProtocolField { Name = "Gy",     Offset = 10, Length = 2, Type = FieldType.Int16BE },
                new ProtocolField { Name = "Gz",     Offset = 12, Length = 2, Type = FieldType.Int16BE }
            }
        }
    };
}
