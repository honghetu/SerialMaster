using SerialMaster.Core.Models;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class ProtocolParserTests
{
    private static ProtocolDefinition SensorDefinition() => new()
    {
        Name = "Sensor",
        HeaderHex = "AA 55",
        FrameLength = 6,
        Fields =
        {
            new ProtocolField { Name = "Hdr",   Offset = 0, Length = 2, Type = FieldType.Hex },
            new ProtocolField { Name = "Temp",  Offset = 2, Length = 2, Type = FieldType.UInt16LE },
            new ProtocolField { Name = "Humid", Offset = 4, Length = 2, Type = FieldType.UInt16LE }
        }
    };

    [TestMethod]
    public void Feed_SingleCompleteFrame_EmitsOneFrame()
    {
        var parser = new ProtocolParser { Definition = SensorDefinition() };
        // Temp = 0x00FA = 250, Humid = 0x0190 = 400
        var bytes = new byte[] { 0xAA, 0x55, 0xFA, 0x00, 0x90, 0x01 };

        var frames = parser.Feed(bytes).ToList();

        Assert.AreEqual(1, frames.Count);
        Assert.AreEqual("250", frames[0].FieldValues[1].Value);
        Assert.AreEqual("400", frames[0].FieldValues[2].Value);
    }

    [TestMethod]
    public void Feed_FragmentedAcrossCalls_ReassemblesFrame()
    {
        var parser = new ProtocolParser { Definition = SensorDefinition() };

        var frames1 = parser.Feed(new byte[] { 0xAA, 0x55, 0xFA }).ToList();
        var frames2 = parser.Feed(new byte[] { 0x00, 0x90 }).ToList();
        var frames3 = parser.Feed(new byte[] { 0x01 }).ToList();

        Assert.AreEqual(0, frames1.Count);
        Assert.AreEqual(0, frames2.Count);
        Assert.AreEqual(1, frames3.Count);
        Assert.AreEqual("250", frames3[0].FieldValues[1].Value);
    }

    [TestMethod]
    public void Feed_GarbageThenFrame_SyncsOnHeader()
    {
        var parser = new ProtocolParser { Definition = SensorDefinition() };
        var bytes = new byte[]
        {
            0x11, 0x22, 0x33, 0x44,           // garbage
            0xAA, 0x55, 0x10, 0x00, 0x20, 0x00 // frame: Temp=16, Humid=32
        };

        var frames = parser.Feed(bytes).ToList();

        Assert.AreEqual(1, frames.Count);
        Assert.AreEqual("16", frames[0].FieldValues[1].Value);
        Assert.AreEqual("32", frames[0].FieldValues[2].Value);
    }

    [TestMethod]
    public void Feed_TwoBackToBackFrames_EmitsTwo()
    {
        var parser = new ProtocolParser { Definition = SensorDefinition() };
        var bytes = new byte[]
        {
            0xAA, 0x55, 0x01, 0x00, 0x02, 0x00,
            0xAA, 0x55, 0x03, 0x00, 0x04, 0x00
        };

        var frames = parser.Feed(bytes).ToList();

        Assert.AreEqual(2, frames.Count);
        Assert.AreEqual("1", frames[0].FieldValues[1].Value);
        Assert.AreEqual("2", frames[0].FieldValues[2].Value);
        Assert.AreEqual("3", frames[1].FieldValues[1].Value);
        Assert.AreEqual("4", frames[1].FieldValues[2].Value);
    }

    [TestMethod]
    public void Feed_PartialHeaderAtEnd_KeptForNextFeed()
    {
        var parser = new ProtocolParser { Definition = SensorDefinition() };

        // First feed ends with 0xAA (partial header)
        var frames1 = parser.Feed(new byte[] { 0x99, 0xAA }).ToList();
        // Second feed completes header and frame
        var frames2 = parser.Feed(new byte[] { 0x55, 0x05, 0x00, 0x06, 0x00 }).ToList();

        Assert.AreEqual(0, frames1.Count);
        Assert.AreEqual(1, frames2.Count);
        Assert.AreEqual("5", frames2[0].FieldValues[1].Value);
    }

    [TestMethod]
    public void Feed_NoHeader_TreatsBufferAsContiguousFrames()
    {
        var def = new ProtocolDefinition
        {
            Name = "RawFixed",
            HeaderHex = "",
            FrameLength = 4,
            Fields =
            {
                new ProtocolField { Name = "V", Offset = 0, Length = 4, Type = FieldType.UInt32LE }
            }
        };
        var parser = new ProtocolParser { Definition = def };

        var frames = parser.Feed(new byte[] { 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00 }).ToList();

        Assert.AreEqual(2, frames.Count);
        Assert.AreEqual("1", frames[0].FieldValues[0].Value);
        Assert.AreEqual("65535", frames[1].FieldValues[0].Value);
    }

    [TestMethod]
    public void FieldType_Int16BE_DecodedCorrectly()
    {
        var def = new ProtocolDefinition
        {
            Name = "T",
            HeaderHex = "FF",
            FrameLength = 3,
            Fields =
            {
                new ProtocolField { Name = "H", Offset = 0, Length = 1, Type = FieldType.Hex },
                new ProtocolField { Name = "V", Offset = 1, Length = 2, Type = FieldType.Int16BE }
            }
        };
        var parser = new ProtocolParser { Definition = def };
        // 0xFFFF as Int16BE = -1
        var frames = parser.Feed(new byte[] { 0xFF, 0xFF, 0xFF }).ToList();
        Assert.AreEqual(1, frames.Count);
        Assert.AreEqual("-1", frames[0].FieldValues[1].Value);
    }

    [TestMethod]
    public void FieldType_Float32LE_DecodedCorrectly()
    {
        var def = new ProtocolDefinition
        {
            Name = "F",
            HeaderHex = "",
            FrameLength = 4,
            Fields =
            {
                new ProtocolField { Name = "V", Offset = 0, Length = 4, Type = FieldType.Float32LE }
            }
        };
        var parser = new ProtocolParser { Definition = def };
        // 1.0f as little-endian = 00 00 80 3F
        var frames = parser.Feed(new byte[] { 0x00, 0x00, 0x80, 0x3F }).ToList();
        Assert.AreEqual(1, frames.Count);
        Assert.AreEqual("1.0000", frames[0].FieldValues[0].Value);
    }

    [TestMethod]
    public void Reset_ClearsInternalBuffer()
    {
        var parser = new ProtocolParser { Definition = SensorDefinition() };
        parser.Feed(new byte[] { 0xAA, 0x55, 0x01 }).ToList();
        parser.Reset();
        // After reset, the remaining 0xAA 0x55 0x01 are gone; feeding the rest alone should not emit
        var frames = parser.Feed(new byte[] { 0x00, 0x02, 0x00 }).ToList();
        Assert.AreEqual(0, frames.Count);
    }

    [TestMethod]
    public void Definition_Setter_ClearsBuffer()
    {
        var parser = new ProtocolParser { Definition = SensorDefinition() };
        parser.Feed(new byte[] { 0xAA }).ToList();
        // Re-assign — buffer should clear
        parser.Definition = SensorDefinition();
        var frames = parser.Feed(new byte[] { 0x55, 0x01, 0x00, 0x02, 0x00 }).ToList();
        Assert.AreEqual(0, frames.Count, "Old 0xAA should have been discarded");
    }
}
