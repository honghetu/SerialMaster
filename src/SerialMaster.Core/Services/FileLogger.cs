namespace SerialMaster.Core.Services;

/// <summary>
/// Minimal thread-safe file logger. Writes to %LocalAppData%/SerialMaster/logs/serialmaster-yyyyMMdd.log.
/// Rotates by date. No external dependencies.
/// </summary>
public static class FileLogger
{
    private static readonly object _lock = new();
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SerialMaster", "logs");

    public static string LogDirectory => _dir;

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var path = Path.Combine(_dir, $"serialmaster-{DateTime.Now:yyyyMMdd}.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            if (ex != null)
                line += $"\n  {ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}";

            lock (_lock)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never throw to the caller.
        }
    }
}
