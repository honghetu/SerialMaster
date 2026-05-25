using System.Text;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class SlcanCodecTests
{
    [TestMethod]
    public void Decode_StandardDataFrame_ReturnsFrame()
    {
        // t1238 1122334455667788  ID=0x123 DLC=8 data
        var codec = new SlcanCodec();
        var bytes = Encoding.ASCII.GetBytes("t12381122334455667788\r");

        var frames = codec.Decode(bytes).ToList();

        Assert.AreEqual(1, frames.Count);
        Assert.AreEqual(CanFrameKind.Standard, frames[0].Kind);
        Assert.AreEqual(0x123u, frames[0].Id);
        Assert.AreEqual(8, frames[0].Dlc);
        CollectionAssert.AreEqual(
            new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 },
            frames[0].Data);
    }

    [TestMethod]
    public void Decode_ExtendedDataFrame_ReturnsFrame()
    {
        var codec = new SlcanCodec();
        // T 1ABCDEF0 2 DEAD  → ID=0x1ABCDEF0, DLC=2, data DE AD
        var bytes = Encoding.ASCII.GetBytes("T1ABCDEF02DEAD\r");

        var frames = codec.Decode(bytes).ToList();

        Assert.AreEqual(1, frames.Count);
        Assert.AreEqual(CanFrameKind.Extended, frames[0].Kind);
        Assert.AreEqual(0x1ABCDEF0u, frames[0].Id);
        Assert.AreEqual(2, frames[0].Dlc);
        CollectionAssert.AreEqual(new byte[] { 0xDE, 0xAD }, frames[0].Data);
    }

    [TestMethod]
    public void Decode_StandardRtr_ReturnsRtrFrame()
    {
        var codec = new SlcanCodec();
        // r1234 = standard RTR, ID=0x123, DLC=4
        var bytes = Encoding.ASCII.GetBytes("r1234\r");

        var frames = codec.Decode(bytes).ToList();

        Assert.AreEqual(1, frames.Count);
        Assert.IsTrue(frames[0].IsRtr);
        Assert.AreEqual(CanFrameKind.StandardRtr, frames[0].Kind);
        Assert.AreEqual(0x123u, frames[0].Id);
        Assert.AreEqual(4, frames[0].Dlc);
        Assert.AreEqual(0, frames[0].Data.Length);
    }

    [TestMethod]
    public void Decode_ExtendedRtr_ReturnsRtrFrame()
    {
        var codec = new SlcanCodec();
        var bytes = Encoding.ASCII.GetBytes("R000000010\r");

        var frames = codec.Decode(bytes).ToList();

        Assert.AreEqual(1, frames.Count);
        Assert.AreEqual(CanFrameKind.ExtendedRtr, frames[0].Kind);
        Assert.AreEqual(0x1u, frames[0].Id);
        Assert.AreEqual(0, frames[0].Dlc);
    }

    [TestMethod]
    public void Decode_FragmentedAcrossCalls_ReassemblesFrame()
    {
        var codec = new SlcanCodec();

        var f1 = codec.Decode(Encoding.ASCII.GetBytes("t12")).ToList();
        var f2 = codec.Decode(Encoding.ASCII.GetBytes("3811")).ToList();
        var f3 = codec.Decode(Encoding.ASCII.GetBytes("22334455667788\r")).ToList();

        Assert.AreEqual(0, f1.Count);
        Assert.AreEqual(0, f2.Count);
        Assert.AreEqual(1, f3.Count);
        Assert.AreEqual(0x123u, f3[0].Id);
        Assert.AreEqual(8, f3[0].Dlc);
    }

    [TestMethod]
    public void Decode_TwoBackToBackFrames_BothEmitted()
    {
        var codec = new SlcanCodec();
        var bytes = Encoding.ASCII.GetBytes("t10010A\rt2002BBCC\r");

        var frames = codec.Decode(bytes).ToList();

        Assert.AreEqual(2, frames.Count);
        Assert.AreEqual(0x100u, frames[0].Id);
        Assert.AreEqual(1, frames[0].Dlc);
        Assert.AreEqual(0x0A, frames[0].Data[0]);
        Assert.AreEqual(0x200u, frames[1].Id);
        Assert.AreEqual(2, frames[1].Dlc);
    }

    [TestMethod]
    public void Decode_GarbageBetweenFrames_StillSyncs()
    {
        var codec = new SlcanCodec();
        // garbage "XX\r" produces a line that fails TryParseLine and is skipped
        var bytes = Encoding.ASCII.GetBytes("XX\rt12300\r");

        var frames = codec.Decode(bytes).ToList();

        Assert.AreEqual(1, frames.Count);
        Assert.AreEqual(0x123u, frames[0].Id);
    }

    [TestMethod]
    public void Decode_TruncatedDlc_Rejected()
    {
        var codec = new SlcanCodec();
        // t1238 says DLC=8 but only 4 data bytes given → reject
        var bytes = Encoding.ASCII.GetBytes("t123811223344\r");

        var frames = codec.Decode(bytes).ToList();

        Assert.AreEqual(0, frames.Count);
    }

    [TestMethod]
    public void Encode_StandardFrame_MatchesFormat()
    {
        var frame = new CanFrame
        {
            Kind = CanFrameKind.Standard,
            Id = 0x123,
            Dlc = 2,
            Data = new byte[] { 0xAA, 0xBB }
        };
        Assert.AreEqual("t1232AABB", SlcanCodec.Encode(frame));
    }

    [TestMethod]
    public void Encode_ExtendedFrame_UsesEightHexId()
    {
        var frame = new CanFrame
        {
            Kind = CanFrameKind.Extended,
            Id = 0x1ABCDEF0,
            Dlc = 1,
            Data = new byte[] { 0xFF }
        };
        Assert.AreEqual("T1ABCDEF01FF", SlcanCodec.Encode(frame));
    }

    [TestMethod]
    public void EncodeWithTerminator_AppendsCR()
    {
        var frame = new CanFrame
        {
            Kind = CanFrameKind.Standard,
            Id = 0x1,
            Dlc = 0,
            Data = Array.Empty<byte>()
        };
        var bytes = SlcanCodec.EncodeWithTerminator(frame);
        Assert.AreEqual((byte)'\r', bytes[^1]);
        Assert.AreEqual("t0010", Encoding.ASCII.GetString(bytes, 0, bytes.Length - 1));
    }

    [TestMethod]
    public void RoundTrip_StandardFrame()
    {
        var original = new CanFrame
        {
            Kind = CanFrameKind.Standard,
            Id = 0x7AB,
            Dlc = 3,
            Data = new byte[] { 0x10, 0x20, 0x30 }
        };
        var codec = new SlcanCodec();
        var bytes = SlcanCodec.EncodeWithTerminator(original);

        var decoded = codec.Decode(bytes).ToList();

        Assert.AreEqual(1, decoded.Count);
        Assert.AreEqual(original.Id, decoded[0].Id);
        Assert.AreEqual(original.Dlc, decoded[0].Dlc);
        CollectionAssert.AreEqual(original.Data, decoded[0].Data);
    }
}
