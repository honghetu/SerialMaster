using System.IO.Ports;

namespace SerialMaster.Core.Models;

public record SerialConfig(
    string PortName,
    int BaudRate = 115200,
    int DataBits = 8,
    Parity Parity = Parity.None,
    StopBits StopBits = StopBits.One,
    int ReadTimeoutMs = 2000,
    int WriteTimeoutMs = 2000
);
