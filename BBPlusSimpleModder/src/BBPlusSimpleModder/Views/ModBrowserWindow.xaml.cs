using System.Windows;
using BBPlusSimpleModder.Core.Models;
using BBPlusSimpleModder.Services;
using Microsoft.Win32;
using System.Linq;
using System.Threading.Tasks;

namespace BBPlusSimpleModder.Views;

/// <summary>Импорт чужого мода (DLL → Cecil/Decompiler → кирпичики) или уровня (.lvl → LevelEditor).</summary>
public partial class ModBrowserWindow : Window
{
    private readonly ModProject _project;
    private readonly ModImportService _import = App.Services.Get<ModImportService>();
    private string _lastDll = "";

    public ModBrowserWindow(ModProject project)
    {
        InitializeComponent();
        _project = project;
    }

    private void OpenDll_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Плагин BepInEx (*.dll)|*.dll" };
        if (ofd.ShowDialog() != true) return;
        _lastDll = ofd.FileName;
        FoundList.ItemsSource = _import.ScanDll(ofd.FileName);
        ImportBtn.IsEnabled = true;
        HintText.Text = $"Найдено сущностей: {FoundList.Items.Count}";
    }

    private void OpenLvl_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Уровень BB+ (*.lvl)|*.lvl" };
        if (ofd.ShowDialog() == true)
        {
            _import.ImportLvlIntoProject(ofd.FileName, _project);
            Close();
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (FoundList.ItemsSource is not IEnumerable<ImportItem> items) return;
        var selected = items.Where(i => i.Selected).ToList();
        ImportBtn.IsEnabled = false;
        await _import.ImportSelectedAsync(selected, _project, _lastDll);
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
