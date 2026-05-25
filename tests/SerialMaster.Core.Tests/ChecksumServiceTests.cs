using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class ChecksumServiceTests
{
    // Standard Modbus RTU test vector:
    //   Request "01 03 00 00 00 0A" → CRC = 0xCDC5 → transmitted as "C5 CD"
    [TestMethod]
    public void ModbusCRC_StandardVector_ReturnsLowHighOrder()
    {
        string result = ChecksumService.Compute("01 03 00 00 00 0A", ChecksumType.ModbusCRC);
        Assert.AreEqual("C5 CD", result);
    }

    [TestMethod]
    public void CRC16_ModbusPolynomial_MatchesModbusVector()
    {
        // CRC16 and ModbusCRC share polynomial 0xA001 / init 0xFFFF.
        // CRC16 returns the raw 16-bit value as "HHLL" (hex string).
        string result = ChecksumService.Compute("01 03 00 00 00 0A", ChecksumType.CRC16);
        Assert.AreEqual("CDC5", result);
    }

    [TestMethod]
    public void XOR_KnownInput_ReturnsExpected()
    {
        // 0x01 ^ 0x02 ^ 0x03 = 0x00
        string result = ChecksumService.Compute("01 02 03", ChecksumType.XOR);
        Assert.AreEqual("00", result);

        // 0xAA ^ 0x55 = 0xFF
        result = ChecksumService.Compute("AA 55", ChecksumType.XOR);
        Assert.AreEqual("FF", result);
    }

    [TestMethod]
    public void ADD_TruncatesToByte()
    {
        // 0xFF + 0x02 = 0x101 → 0x01
        string result = ChecksumService.Compute("FF 02", ChecksumType.ADD);
        Assert.AreEqual("01", result);
    }

    [TestMethod]
    public void CRC8_DallasMaximPolynomial_ReturnsExpected()
    {
        // CRC8 with poly 0x07 (CCITT), init 0x00
        // For [0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39] ("123456789") → 0xF4
        string result = ChecksumService.Compute("31 32 33 34 35 36 37 38 39", ChecksumType.CRC8);
        Assert.AreEqual("F4", result);
    }

    [TestMethod]
    public void CRC32_IEEE_KnownVector()
    {
        // CRC32 of "123456789" = 0xCBF43926
        string result = ChecksumService.Compute("31 32 33 34 35 36 37 38 39", ChecksumType.CRC32);
        Assert.AreEqual("CBF43926", result);
    }

    [TestMethod]
    public void Compute_TextInput_TreatedAsUtf8()
    {
        // "A" = 0x41, XOR of single byte = 0x41
        string result = ChecksumService.Compute("A", ChecksumType.XOR);
        Assert.AreEqual("41", result);
    }

    [TestMethod]
    public void Compute_HexWithoutSpaces_StillRecognized()
    {
        // Regression: old impl required spaces. New impl uses LooksLikeHex.
        string result = ChecksumService.Compute("AA55", ChecksumType.XOR);
        Assert.AreEqual("FF", result);
    }
}
