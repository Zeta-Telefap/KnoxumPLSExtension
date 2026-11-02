using BBPlusSimpleModder.Core.LvlFormat;

namespace BBPlusSimpleModder.Services;

/// <summary>Тонкая обёртка над Core.LvlFormat: чтение/запись .lvl (Plus Level Format v10).</summary>
public class LvlFormatService
{
    private readonly Logger _logger;

    public LvlFormatService(Logger logger) => _logger = logger;

    public BaldiLevelModel Read(string path)
    {
        var m = BaldiLevelModel.Load(path);
        _logger.Info($"Уровень загружен: {path} ({m.Width}x{m.Height}, комнат {m.Rooms.Count})");
        return m;
    }

    public void Write(string path, BaldiLevelModel model)
    {
        model.Save(path);
        _logger.Info($"Уровень сохранён: {path}");
    }
}
