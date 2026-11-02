namespace BBPlusSimpleModder.Services;

/// <summary>Простой логгер: событие для UI + файл в папке приложения.</summary>
public class Logger
{
    public event Action<string>? OnMessage;

    private string _logFile = "";
    private readonly object _gate = new();

    public void Init()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BBPlusSimpleModder");
        Directory.CreateDirectory(dir);
        _logFile = Path.Combine(dir, "app.log");
    }

    public void Info(string msg) => Write("INFO", msg);
    public void Warn(string msg) => Write("WARN", msg);
    public void Error(string msg) => Write("ERROR", msg);

    public void Write(string level, string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {msg}";
        lock (_gate)
        {
            try { if (_logFile.Length > 0) File.AppendAllText(_logFile, line + Environment.NewLine); } catch { }
        }
        OnMessage?.Invoke(line);
    }
}
