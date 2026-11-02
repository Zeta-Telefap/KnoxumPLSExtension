namespace BBPlusSimpleModder.Core.Models;

/// <summary>Что проверяем/ставим в папке игры ( StartupWindow ).</summary>
public enum EnvKind { Game, BepInEx, DevApi, LevelLoader, LevelStudio }

/// <summary>Строка статуса окружения в StartupWindow.</summary>
public class EnvStatusItem
{
    public EnvKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Installed { get; set; }
    public bool CanInstall { get; set; }
    public string ActionText => CanInstall && !Installed ? "Установить" : "";
}
