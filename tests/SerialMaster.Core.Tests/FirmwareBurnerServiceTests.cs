using SerialMaster.Core.Services;

namespace SerialMaster.Core.Tests;

[TestClass]
public class FirmwareBurnerServiceTests
{
    [TestMethod]
    public void BuildArguments_Esp32_UsesDefaultOffset_0x10000()
    {
        var req = new BurnRequest
        {
            Family = ChipFamily.Esp32,
            FirmwarePath = @"C:\fw\app.bin",
            PortName = "COM3",
            BaudRate = 921600,
            FlashOffset = 0
        };
        var args = FirmwareBurnerService.BuildArguments(req);
        StringAssert.Contains(args, "--chip esp32");
        StringAssert.Contains(args, "--port COM3");
        StringAssert.Contains(args, "--baud 921600");
        StringAssert.Contains(args, "0x10000");
        StringAssert.Contains(args, "\"C:\\fw\\app.bin\"");
    }

    [TestMethod]
    public void BuildArguments_Esp32_CustomOffsetHonored()
    {
        var req = new BurnRequest
        {
            Family = ChipFamily.Esp32,
            FirmwarePath = "fw.bin",
            PortName = "COM5",
            BaudRate = 460800,
            FlashOffset = 0x1000
        };
        var args = FirmwareBurnerService.BuildArguments(req);
        StringAssert.Contains(args, "0x1000");
        Assert.IsFalse(args.Contains("0x10000"), "should use custom offset, not default");
    }

    [TestMethod]
    public void BuildArguments_Esp8266_UsesZeroOffset_AndDetectFlashSize()
    {
        var req = new BurnRequest
        {
            Family = ChipFamily.Esp8266,
            FirmwarePath = "app.bin",
            PortName = "COM1",
            BaudRate = 115200,
            FlashOffset = 0
        };
        var args = FirmwareBurnerService.BuildArguments(req);
        StringAssert.Contains(args, "--chip esp8266");
        StringAssert.Contains(args, "0x0 ");
        StringAssert.Contains(args, "-fs detect");
    }

    [TestMethod]
    public void BuildArguments_Stm32Isp_UsesWriteVerifyGo()
    {
        var req = new BurnRequest
        {
            Family = ChipFamily.Stm32Isp,
            FirmwarePath = "fw.hex",
            PortName = "COM7",
            BaudRate = 115200
        };
        var args = FirmwareBurnerService.BuildArguments(req);
        StringAssert.Contains(args, "-w");
        StringAssert.Contains(args, "-v");
        StringAssert.Contains(args, "-g 0x0");
        StringAssert.Contains(args, "-b 115200");
        StringAssert.Contains(args, "COM7");
    }

    [TestMethod]
    public void BuildArguments_PathWithSpaces_IsQuoted()
    {
        var req = new BurnRequest
        {
            Family = ChipFamily.Esp32,
            FirmwarePath = @"C:\My Projects\fw v1.bin",
            PortName = "COM3",
            BaudRate = 921600
        };
        var args = FirmwareBurnerService.BuildArguments(req);
        StringAssert.Contains(args, "\"C:\\My Projects\\fw v1.bin\"");
    }

    [TestMethod]
    public void DefaultExecutable_MapsCorrectly()
    {
        Assert.AreEqual("esptool.exe", FirmwareBurnerService.DefaultExecutable(ChipFamily.Esp32));
        Assert.AreEqual("esptool.exe", FirmwareBurnerService.DefaultExecutable(ChipFamily.Esp8266));
        Assert.AreEqual("stm32flash.exe", FirmwareBurnerService.DefaultExecutable(ChipFamily.Stm32Isp));
    }
}
