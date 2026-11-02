using System.Windows;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Views;

/// <summary>Настройки проекта: папка игры, flat-vs-папка для плагина, автоустановки.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsModel _settings;

    public SettingsWindow(SettingsModel settings)
    {
        InitializeComponent();
        _settings = settings;
        GamePathBox.Text = settings.GamePath;
        PluginFlat.IsChecked = !settings.PluginInFolder;
        PluginFolder.IsChecked = settings.PluginInFolder;
        AutoBepInEx.IsChecked = settings.AutoInstallBepInEx;
        AutoDevApi.IsChecked = settings.AutoInstallDevApi;
        AutoPls.IsChecked = settings.AutoInstallPls;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.GamePath = GamePathBox.Text.Trim();
        _settings.PluginInFolder = PluginFolder.IsChecked == true;
        _settings.AutoInstallBepInEx = AutoBepInEx.IsChecked == true;
        _settings.AutoInstallDevApi = AutoDevApi.IsChecked == true;
        _settings.AutoInstallPls = AutoPls.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
