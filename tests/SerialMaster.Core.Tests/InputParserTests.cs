using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class InputParserTests
{
    [TestMethod]
    public void ParseHex_SpaceSeparated_ReturnsBytes()
    {
        var bytes = InputParser.ParseHex("AA BB 0C");
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0x0C }, bytes);
    }

    [TestMethod]
    public void ParseHex_ContinuousString_ReturnsBytes()
    {
        var bytes = InputParser.ParseHex("AABB0C");
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0x0C }, bytes);
    }

    [TestMethod]
    public void ParseHex_WithOxPrefix_StripsPrefix()
    {
        var bytes = InputParser.ParseHex("0xAA 0xBB 0x0C");
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0x0C }, bytes);
    }

    [TestMethod]
    public void ParseHex_WithDashesAndCommas_HandlesSeparators()
    {
        var bytes = InputParser.ParseHex("AA-BB-0C,FF");
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0x0C, 0xFF }, bytes);
    }

    [TestMethod]
    public void ParseHex_OddLength_PadsWithZero()
    {
        var bytes = InputParser.ParseHex("A");
        CollectionAssert.AreEqual(new byte[] { 0x0A }, bytes);
    }

    [TestMethod]
    public void ParseHex_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.AreEqual(0, InputParser.ParseHex("").Length);
        Assert.AreEqual(0, InputParser.ParseHex("   ").Length);
    }

    [TestMethod]
    public void LooksLikeHex_HexString_True()
    {
        Assert.IsTrue(InputParser.LooksLikeHex("AA BB CC"));
        Assert.IsTrue(InputParser.LooksLikeHex("DEADBEEF"));
        Assert.IsTrue(InputParser.LooksLikeHex("01-02-03"));
    }

    [TestMethod]
    public void LooksLikeHex_NotHex_False()
    {
        Assert.IsFalse(InputParser.LooksLikeHex("Hello"));
        Assert.IsFalse(InputParser.LooksLikeHex("AT+RST"));
        Assert.IsFalse(InputParser.LooksLikeHex(""));
        Assert.IsFalse(InputParser.LooksLikeHex("A"));
    }

    [TestMethod]
    public void Parse_AsciiMode_AppendsCRLF()
    {
        var bytes = InputParser.Parse("AT", SendMode.Ascii, LineEnding.CRLF);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x54, 0x0D, 0x0A }, bytes);
    }

    [TestMethod]
    public void Parse_AsciiMode_NoEnding()
    {
        var bytes = InputParser.Parse("AT", SendMode.Ascii, LineEnding.None);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x54 }, bytes);
    }

    [TestMethod]
    public void Parse_AsciiMode_LFOnly()
    {
        var bytes = InputParser.Parse("X", SendMode.Ascii, LineEnding.LF);
        CollectionAssert.AreEqual(new byte[] { 0x58, 0x0A }, bytes);
    }

    [TestMethod]
    public void Parse_HexMode_IgnoresLineEnding()
    {
        // Caller controls — but Parse itself respects ending param.
        // SessionViewModel forces None when mode==Hex; this just verifies Parse honors what it's given.
        var bytes = InputParser.Parse("AA BB", SendMode.Hex, LineEnding.None);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, bytes);
    }

    [TestMethod]
    public void Parse_AutoMode_HexInput_ParsesAsHex()
    {
        var bytes = InputParser.Parse("AA BB CC", SendMode.Auto, LineEnding.None);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0xCC }, bytes);
    }

    [TestMethod]
    public void Parse_AutoMode_TextInput_ParsesAsUtf8()
    {
        var bytes = InputParser.Parse("Hello", SendMode.Auto, LineEnding.None);
        CollectionAssert.AreEqual(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }, bytes);
    }

    [TestMethod]
    public void Parse_Utf8Mode_ChineseChar()
    {
        var bytes = InputParser.Parse("中", SendMode.Utf8, LineEnding.None);
        CollectionAssert.AreEqual(new byte[] { 0xE4, 0xB8, 0xAD }, bytes);
    }
}
