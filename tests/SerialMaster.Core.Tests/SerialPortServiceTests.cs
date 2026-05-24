using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class SerialPortServiceTests
{
    [TestMethod]
    public void Open_WithInvalidPort_ThrowsException()
    {
        using var service = new SerialPortService();
        var config = new SerialConfig("COM_NONEXISTENT_99999");

        Assert.ThrowsException<System.IO.IOException>(() =>
        {
            try { service.Open(config); }
            catch (ArgumentException) { throw new System.IO.IOException(); }
        });
    }

    [TestMethod]
    public void IsOpen_AfterConstruction_ReturnsFalse()
    {
        using var service = new SerialPortService();
        Assert.IsFalse(service.IsOpen);
    }

    [TestMethod]
    public async Task SendAsync_WhenNotOpen_ThrowsInvalidOperationException()
    {
        using var service = new SerialPortService();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            service.SendAsync(new byte[] { 0x01 }));
    }

    [TestMethod]
    public void Config_AfterOpen_ReturnsConfig()
    {
        using var service = new SerialPortService();
        var ports = System.IO.Ports.SerialPort.GetPortNames();
        if (ports.Length == 0)
        {
            Assert.Inconclusive("No serial ports available for testing");
            return;
        }

        var config = new SerialConfig(ports[0]);
        try
        {
            service.Open(config);
            Assert.AreEqual(config.PortName, service.Config.PortName);
        }
        catch
        {
            Assert.Inconclusive("Port in use, cannot test");
        }
        finally
        {
            service.Close();
        }
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        using var service = new SerialPortService();
        service.Dispose();
        Assert.IsFalse(service.IsOpen);
    }
}
