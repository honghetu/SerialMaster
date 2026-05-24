using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerialMaster.Core.Models;
using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class SettingsServiceTests
{
    [TestMethod]
    public void Load_NoFile_ReturnsDefault()
    {
        var service = new SettingsService();
        var settings = service.Load();

        Assert.AreEqual("Dark", settings.Theme);
        Assert.AreEqual(115200, settings.LastBaudRate);
    }

    [TestMethod]
    public void SaveAndLoad_RoundTrip()
    {
        var service = new SettingsService();
        var original = new AppSettings
        {
            Theme = "Light",
            LastPort = "COM3",
            LastBaudRate = 9600
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.AreEqual(original.Theme, loaded.Theme);
        Assert.AreEqual(original.LastPort, loaded.LastPort);
        Assert.AreEqual(original.LastBaudRate, loaded.LastBaudRate);
    }
}
