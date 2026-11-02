namespace BBPlusSimpleModder.Services;

/// <summary>DI-lite: реестр сервисов приложения (App.Services).</summary>
public class ServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T instance) where T : notnull => _services[typeof(T)] = instance;

    public T Get<T>() where T : notnull =>
        _services.TryGetValue(typeof(T), out var s) ? (T)s : throw new InvalidOperationException($"Сервис {typeof(T).Name} не зарегистрирован");

    public void RegisterAll()
    {
        var logger = new Logger();
        Register<Logger>(logger);
        var settings = new SettingsService();
        Register<SettingsService>(settings);
        Register(settings.Model);   // SettingsModel как сервис
        Register(new GameEnvironmentService(settings.Model, logger));
        Register(new ProjectService(settings.Model, logger));
        Register(new AssetImportService(logger));
        Register(new BlockCatalogService(logger));
        Register(new LvlFormatService(logger));
        Register(new ModImportService(logger));
        Register(new BuildService(logger));
    }
}
