using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class DeviceEnumeratorTests
{
    [TestMethod]
    public void GetAvailablePorts_ReturnsList()
    {
        using var enumerator = new DeviceEnumerator();
        var ports = enumerator.GetAvailablePorts();
        Assert.IsNotNull(ports);
    }

    [TestMethod]
    public void GetAvailablePorts_EachPortHasName()
    {
        using var enumerator = new DeviceEnumerator();
        var ports = enumerator.GetAvailablePorts();

        foreach (var port in ports)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(port.PortName));
            Assert.IsTrue(port.PortName.StartsWith("COM"));
        }
    }
}
