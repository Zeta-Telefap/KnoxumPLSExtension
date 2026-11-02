using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BBPlusSimpleModder.Core.Models;
using BBPlusSimpleModder.Services;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace BBPlusSimpleModder.Views.Controls;

/// <summary>Плитка ассетов мода с превью (спрайты — картинка, остальное — иконка).</summary>
public partial class AssetBrowserView : UserControl
{
    private ModProject? _project;
    private readonly AssetImportService _import = App.Services.Get<AssetImportService>();

    public AssetBrowserView() => InitializeComponent();

    public void Bind(ModProject p)
    {
        _project = p;
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        if (_project == null) return;
        Grid.ItemsSource = _project.Assets.Select(a => new AssetVm(a, _project)).ToList();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_project == null) return;
        var ofd = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Ассеты|*.png;*.jpg;*.jpeg;*.tga;*.dds;*.bmp;*.obj;*.wav;*.ogg;*.mp3;*.json;*.txt|Все файлы|*.*"
        };
        if (ofd.ShowDialog() == true)
        {
            _import.ImportFiles(ofd.FileNames, _project.Root, _project);
            RefreshGrid();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_project == null) return;
        foreach (var vm in Grid.SelectedItems.Cast<AssetVm>().ToList())
        {
            _import.Delete(vm.Entry, _project);
        }
        RefreshGrid();
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void Grid_Drop(object sender, DragEventArgs e)
    {
        if (_project == null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            _import.ImportFiles(files, _project.Root, _project);
            RefreshGrid();
        }
    }

    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // превью спрайта в отдельном окне
        if (Grid.SelectedItem is AssetVm vm && vm.Thumbnail != null)
        {
            var w = new Window
            {
                Title = vm.Name,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = System.Windows.Media.Brushes.White
            };
            w.Content = new Image { Source = vm.Thumbnail, MaxWidth = 640, MaxHeight = 640 };
            w.Show();
        }
    }

    /// <summary>VM ассета: превью для картинок, иконка по виду.</summary>
    private class AssetVm
    {
        public AssetEntry Entry { get; }
        public string Name => Entry.Name;
        public string KindText => Entry.Kind.ToString();
        public ImageSource? Thumbnail { get; }
        public string Icon => Entry.Kind switch
        {
            AssetKind.Model => "🧊",
            AssetKind.Audio => "🔊",
            AssetKind.Data => "📄",
            _ => ""
        };

        public AssetVm(AssetEntry entry, ModProject project)
        {
            Entry = entry;
            if (entry.Kind is AssetKind.Sprite or AssetKind.Texture)
            {
                try
                {
                    var path = Path.Combine(project.ProjectDir, entry.RelativePath);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path);
                    bmp.DecodePixelWidth = 100;
                    bmp.EndInit();
                    bmp.Freeze();
                    Thumbnail = bmp;
                }
                catch { Thumbnail = null; }
            }
        }
    }
}
