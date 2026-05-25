using System.Diagnostics;

namespace SerialMaster.Core.Services;

public enum ChipFamily
{
    Esp32,
    Esp8266,
    Stm32Isp
}

public sealed class BurnRequest
{
    public ChipFamily Family { get; set; }
    public string FirmwarePath { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 921600;

    /// <summary>Flash offset in bytes (ESP family). Defaults: ESP32 = 0x10000, ESP8266 = 0x0.</summary>
    public int FlashOffset { get; set; }

    /// <summary>Optional override for the burner executable path. Empty = look up by family.</summary>
    public string ExecutableOverride { get; set; } = string.Empty;
}

/// <summary>
/// Wraps external flashers as child processes:
///   esptool.exe (ESP32 / ESP8266)
///   stm32flash.exe (STM32 ISP)
/// The user is responsible for placing the binaries in PATH or providing ExecutableOverride.
/// </summary>
public sealed class FirmwareBurnerService
{
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? Exited;

    private Process? _process;

    public bool IsRunning => _process != null && !_process.HasExited;

    public static string DefaultExecutable(ChipFamily family) => family switch
    {
        ChipFamily.Esp32     => "esptool.exe",
        ChipFamily.Esp8266   => "esptool.exe",
        ChipFamily.Stm32Isp  => "stm32flash.exe",
        _ => string.Empty
    };

    public static string BuildArguments(BurnRequest req)
    {
        switch (req.Family)
        {
            case ChipFamily.Esp32:
            {
                int offset = req.FlashOffset == 0 ? 0x10000 : req.FlashOffset;
                return $"--chip esp32 --port {req.PortName} --baud {req.BaudRate} " +
                       $"write_flash -z 0x{offset:X} \"{req.FirmwarePath}\"";
            }
            case ChipFamily.Esp8266:
            {
                int offset = req.FlashOffset; // 默认 0x0
                return $"--chip esp8266 --port {req.PortName} --baud {req.BaudRate} " +
                       $"write_flash -fm dio -fs detect 0x{offset:X} \"{req.FirmwarePath}\"";
            }
            case ChipFamily.Stm32Isp:
                return $"-w \"{req.FirmwarePath}\" -v -g 0x0 -b {req.BaudRate} {req.PortName}";
            default:
                return string.Empty;
        }
    }

    public Task StartAsync(BurnRequest req)
    {
        if (IsRunning) throw new InvalidOperationException("Burn already in progress");
        if (string.IsNullOrWhiteSpace(req.FirmwarePath))
            throw new ArgumentException("Firmware path required");
        if (string.IsNullOrWhiteSpace(req.PortName))
            throw new ArgumentException("Port required");

        string exe = string.IsNullOrWhiteSpace(req.ExecutableOverride)
            ? DefaultExecutable(req.Family)
            : req.ExecutableOverride;

        string args = BuildArguments(req);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (e.Data != null) OutputReceived?.Invoke(this, e.Data); };
        _process.ErrorDataReceived  += (_, e) => { if (e.Data != null) OutputReceived?.Invoke(this, e.Data); };
        _process.Exited += (_, _) =>
        {
            int code = _process?.ExitCode ?? -1;
            Exited?.Invoke(this, code);
        };

        OutputReceived?.Invoke(this, $"> {exe} {args}");

        try
        {
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke(this, $"启动失败: {ex.Message}");
            OutputReceived?.Invoke(this, $"提示: 请确认 {exe} 在 PATH 中，或在「可执行路径」字段提供完整路径。");
            _process = null;
            Exited?.Invoke(this, -1);
        }

        return Task.CompletedTask;
    }

    public void Cancel()
    {
        if (_process == null || _process.HasExited) return;
        try { _process.Kill(entireProcessTree: true); }
        catch { }
    }
}
