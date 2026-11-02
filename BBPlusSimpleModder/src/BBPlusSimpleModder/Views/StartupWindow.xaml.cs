using System.Windows;
using System.Windows.Input;
using BBPlusSimpleModder.Core.Models;
using BBPlusSimpleModder.Services;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;

namespace BBPlusSimpleModder.Views;

/// <summary>Стартовое окно: выбор папки игры, проверка/установка BepInEx+API, недавние проекты.</summary>
public partial class StartupWindow : Window
{
    private readonly GameEnvironmentService _env = App.Services.Get<GameEnvironmentService>();
    private readonly SettingsService _settings = App.Services.Get<SettingsService>();

    public StartupWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await OnLoaded();
    }

    private async Task OnLoaded()
    {
        var last = _settings.Model.LastGamePath;
        if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
            await SetGamePath(last);
        RecentList.ItemsSource = _settings.Model.RecentProjects;
        RecentList.DisplayMemberPath = "Display";
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        // выбор папки чисто на WPF: OpenFileDialog с отключенной проверкой файла
        var ofd = new OpenFileDialog
        {
            Title = "Папка игры Baldi's Basics Plus",
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false,
            FileName = "Выберите папку игры"
        };
        if (ofd.ShowDialog() == true)
            await SetGamePath(Path.GetDirectoryName(ofd.FileName)!);
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var p = files[0];
            if (File.Exists(p)) p = Path.GetDirectoryName(p)!;          // перетащили файл из папки игры
            if (Directory.Exists(Path.Combine(p, "BALDI_Data")))
                await SetGamePath(p);                                    // сама папка игры
            else if (Directory.GetParent(p) is { } parent && Directory.Exists(Path.Combine(parent.FullName, "BALDI_Data")))
                await SetGamePath(parent.FullName);                      // перетащили подпапку игры
            else
                await SetGamePath(p);
        }
    }

    private async Task SetGamePath(string path)
    {
        GamePathBox.Text = path;
        _settings.Model.LastGamePath = path;
        _settings.Save();
        var items = await _env.DetectAsync(path);
        EnvList.ItemsSource = items;
        UpdateButtons();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is EnvStatusItem item && GamePathBox.Text.Length > 0)
        {
            item.Status = "⏳ скачиваю…";
            EnvList.Items.Refresh();
            await _env.InstallAsync(GamePathBox.Text, item.Kind);
            await SetGamePath(GamePathBox.Text);
        }
    }

    private void UpdateButtons()
    {
        bool envOk = _env.IsReady(GamePathBox.Text);
        NewModBtn.IsEnabled = envOk;
        ContinueBtn.IsEnabled = envOk && _settings.Model.RecentProjects.Count > 0;
    }

    private void NewMod_Click(object sender, RoutedEventArgs e)
    {
        var w = new NewModWindow { GamePath = GamePathBox.Text, Owner = this };
        if (w.ShowDialog() == true) OpenMain(w.CreatedProjectPath!);
    }

    private void OpenMod_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Проект мода (*.bbmod.json)|*.bbmod.json|Все файлы|*.*" };
        if (ofd.ShowDialog() == true) OpenMain(ofd.FileName);
    }

    private void Recent_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentList.SelectedItem is RecentProject rp) OpenMain(rp.Path);
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Model.RecentProjects.Count > 0) OpenMain(_settings.Model.RecentProjects[0].Path);
    }

    private void OpenMain(string projectPath)
    {
        var main = new MainWindow(projectPath);
        main.Show();
        Close();
    }
}
