using System.Windows;
using BBPlusSimpleModder.Core.Models;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views;

/// <summary>
/// Модальное окно сборки: шаги (референсы → C# → DLL → pdb/xml → ресурсы → плагин),
/// лог, по завершении — «Открыть папку» / «Играть».
/// </summary>
public partial class BuildWindow : Window
{
    private readonly ModProject _project;
    private readonly string _projectPath;
    private readonly BuildService _build = App.Services.Get<BuildService>();
    private readonly GameEnvironmentService _env = App.Services.Get<GameEnvironmentService>();

    public BuildWindow(ModProject project, string projectPath)
    {
        InitializeComponent();
        _project = project;
        _projectPath = projectPath;
        Loaded += async (_, _) => await RunBuild();
    }

    private async Task RunBuild()
    {
        var steps = new List<BuildStep>
        {
            new("Референсы игры и API (BALDI_Data/Managed, BepInEx)"),
            new("Генерация C# из кирпичиков"),
            new("Компиляция DLL (Roslyn)"),
            new("Портативный PDB + XML-документация"),
            new("Ресурсы → BALDI_Data/StreamingAssets/Modded/{GUID}/"),
            new("Плагин → BepInEx/plugins/ (flat или папкой)"),
        };
        Steps.ItemsSource = steps;

        var result = await _build.BuildAsync(_project, _projectPath, steps,
            msg => Dispatcher.Invoke(() =>
            {
                LogBox.AppendText(msg + "\n");
                LogBox.ScrollToEnd();
            }));

        if (result.Success)
        {
            TitleText.Text = "✅ Готово! Мод собран.";
            OpenFolderBtn.IsEnabled = PlayBtn.IsEnabled = CloseBtn.IsEnabled = true;
        }
        else
        {
            TitleText.Text = "❌ Ошибка сборки — см. лог";
            CloseBtn.IsEnabled = true;
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => _build.OpenModFolder(_project);

    private void Play_Click(object sender, RoutedEventArgs e) => _env.LaunchGame(_project.GamePath);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
