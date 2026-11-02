using System.Windows;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder;

/// <summary>Точка входа. Регистрирует сервисы (DI-lite) и запускает StartupWindow.</summary>
public partial class App : Application
{
    public static ServiceRegistry Services { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        Services.RegisterAll();
        Services.Get<Logger>().Init();
        base.OnStartup(e);
    }
}
