using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class NetworkBridgeServiceTests
{
    [TestMethod]
    public void Stats_InitialState_AllZero()
    {
        var stats = new BridgeStats();
        Assert.AreEqual(0, stats.ReceivedFromSerial);
        Assert.AreEqual(0, stats.ForwardedToNetwork);
        Assert.AreEqual(0, stats.ReceivedFromNetwork);
        Assert.AreEqual(0, stats.ForwardedToSerial);
    }

    [TestMethod]
    public void Stats_Accumulate()
    {
        var stats = new BridgeStats { ReceivedFromSerial = 100 };
        stats.ReceivedFromSerial += 50;
        stats.ForwardedToNetwork = 150;
        Assert.AreEqual(150, stats.ReceivedFromSerial);
        Assert.AreEqual(150, stats.ForwardedToNetwork);
    }

    [TestMethod]
    public void BridgeMode_EnumValues()
    {
        // Lock down the public surface — adding/reordering should be a deliberate decision.
        var values = Enum.GetValues<BridgeMode>();
        CollectionAssert.AreEquivalent(
            new[] { BridgeMode.TcpServer, BridgeMode.TcpClient, BridgeMode.Udp },
            values);
    }

    [TestMethod]
    public void Constructor_NotRunning()
    {
        var fake = new FakeSerialService();
        using var bridge = new NetworkBridgeService(fake);
        Assert.IsFalse(bridge.IsRunning);
    }

    [TestMethod]
    public async Task StartAsync_WithoutOpenSerial_Throws()
    {
        var fake = new FakeSerialService { IsOpen = false };
        using var bridge = new NetworkBridgeService(fake);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await bridge.StartAsync(BridgeMode.Udp, "127.0.0.1", 65111));
    }

    private sealed class FakeSerialService : ISerialPortService
    {
        public Models.SerialConfig Config { get; private set; } =
            new("COM_FAKE", 9600);
        public bool IsOpen { get; set; }
        public System.Threading.Channels.ChannelReader<Models.DataRecord> ReceivedData =>
            System.Threading.Channels.Channel.CreateBounded<Models.DataRecord>(1).Reader;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler? ConnectionStateChanged;
        public event EventHandler<Models.DataRecord>? DataReceived;
        public void Open(Models.SerialConfig config) { Config = config; IsOpen = true; }
        public void Close() { IsOpen = false; }
        public Task SendAsync(byte[] data) => Task.CompletedTask;
        public void SetPinState(string pin, bool state) { }
        public bool GetPinState(string pin) => false;
        public void Dispose() { }
        private void _suppress() { ErrorOccurred?.Invoke(this, ""); ConnectionStateChanged?.Invoke(this, EventArgs.Empty); DataReceived?.Invoke(this, null!); }
    }
}
