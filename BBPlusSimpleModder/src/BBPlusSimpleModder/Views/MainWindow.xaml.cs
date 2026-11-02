using System.Windows;
using System.Windows.Threading;
using BBPlusSimpleModder.Core.Models;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views;

/// <summary>
/// Главное окно: слева дерево компонентов, в центре вкладки (кирпичики/ассеты/уровень/код),
/// справа инспектор, снизу лог. Toolbar: сохранить, собрать, играть.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ProjectService _projects = App.Services.Get<ProjectService>();
    private readonly BuildService _build = App.Services.Get<BuildService>();
    private readonly GameEnvironmentService _env = App.Services.Get<GameEnvironmentService>();
    private readonly BlockCatalogService _catalog = App.Services.Get<BlockCatalogService>();
    private readonly Logger _logger = App.Services.Get<Logger>();
    private readonly DispatcherTimer _logTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };

    public ModProject Project { get; private set; } = null!;
    public string ProjectPath { get; }

    public MainWindow(string projectPath)
    {
        InitializeComponent();
        ProjectPath = projectPath;
        Loaded += OnLoaded;
        Closing += (_, _) => _logTimer.Stop();
    }

    private void OnLoaded()
    {
        Project = _projects.Load(ProjectPath);
        Title = $"BB+ Simple Modder — {Project.Name} ({Project.Guid})";
        ModTitle.Text = $"{Project.Name}  [{Project.Guid}]";
        GamePathLabel.Text = Project.GamePath;

        _catalog.LoadAutoBlocks(Project.GamePath);   // авто-блоки из сборок игры

        Tree.Bind(Project, SelectComponent);
        Assets.Bind(Project);
        LevelEditor.Bind(Project);
        GraphEditor.Bind(_catalog);
        Inspector.OpenGraphRequested += c =>
        {
            if (c.Graph != null)
            {
                CenterTabs.SelectedIndex = 0;
                GraphEditor.LoadGraph(c.Graph);
            }
        };

        _logger.OnMessage += msg => Dispatcher.Invoke(() => Log.Append(msg + "\n"));
        _logTimer.Tick += (_, _) =>
        {
            var tail = _env.ReadLogTail(Project.GamePath, 30);
            if (tail.Length > 0) Log.Append("--- BepInEx LogOutput.log (хвост) ---\n" + tail + "\n");
        };
        _logTimer.Start();

        StatusText.Text = "Проект загружен";
    }

    private void SelectComponent(ModComponent c)
    {
        Inspector.Show(c);
        if (c.Graph != null)
        {
            CenterTabs.SelectedIndex = 0;
            GraphEditor.LoadGraph(c.Graph);
            GraphInfoText.Text = $"Граф «{c.Graph.Title}»: {c.Graph.Nodes.Count} блоков, {c.Graph.Wires.Count} проводов";
        }
        else
        {
            GraphInfoText.Text = "";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _projects.Save(ProjectPath, Project);
        StatusText.Text = "Сохранено ✔";
    }

    private void Build_Click(object sender, RoutedEventArgs e)
    {
        Save_Click(sender, e);
        var w = new BuildWindow(Project, ProjectPath) { Owner = this };
        w.ShowDialog();
        CodePreview.Text = _build.LastGeneratedCode;
        StatusText.Text = "Сборка завершена — смотрите вкладку «📄 Сгенерированный код»";
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        Save_Click(sender, e);
        _env.LaunchGame(Project.GamePath);
        StatusText.Text = "Игра запущена — лог появится внизу";
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var w = new ModBrowserWindow(Project) { Owner = this };
        w.ShowDialog();
        Tree.Refresh();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow(Project.Settings) { Owner = this };
        if (w.ShowDialog() == true) _projects.Save(ProjectPath, Project);
    }
}
