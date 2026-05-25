using System.Reflection;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class XmodemServiceTests
{
    // CRC-XMODEM (CCITT, poly 0x1021, init 0x0000) — standard test vector
    // for "123456789" = 0x31E3
    [TestMethod]
    public void CrcXmodem_StandardVector()
    {
        var data = System.Text.Encoding.ASCII.GetBytes("123456789");
        ushort crc = InvokeCrc(data, 0, data.Length);
        Assert.AreEqual(0x31C3, crc);
    }

    [TestMethod]
    public void CrcXmodem_AllZeros_IsZero()
    {
        var data = new byte[128];
        ushort crc = InvokeCrc(data, 0, 128);
        Assert.AreEqual(0, crc);
    }

    [TestMethod]
    public void CrcXmodem_SameInputSameOutput()
    {
        var data = new byte[] { 0xAA, 0x55, 0x01, 0xFF };
        Assert.AreEqual(InvokeCrc(data, 0, 4), InvokeCrc(data, 0, 4));
    }

    [TestMethod]
    public void CrcXmodem_DifferentInputDifferentOutput()
    {
        var d1 = new byte[] { 0x01 };
        var d2 = new byte[] { 0x02 };
        Assert.AreNotEqual(InvokeCrc(d1, 0, 1), InvokeCrc(d2, 0, 1));
    }

    /// <summary>ComputeCrcXmodem is private; reach it via reflection for testing.</summary>
    private static ushort InvokeCrc(byte[] buf, int offset, int length)
    {
        var m = typeof(XmodemService).GetMethod("ComputeCrcXmodem",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ComputeCrcXmodem not found");
        return (ushort)m.Invoke(null, new object[] { buf, offset, length })!;
    }
}
