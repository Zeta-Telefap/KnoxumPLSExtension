using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BBPlusSimpleModder.Core.LvlFormat;
using BBPlusSimpleModder.Core.Models;
using BBPlusSimpleModder.Services;
using Microsoft.Win32;
using System.IO;
using System.Linq;

namespace BBPlusSimpleModder.Views.Controls;

/// <summary>
/// 2D-редактор уровня: сетка клеток (как в PLS), палитра инструментов,
/// чтение/запись .lvl (Plus Level Format v10) через LvlFormatService.
/// </summary>
public partial class LevelEditorView : UserControl
{
    private const int CellSize = 32;

    private readonly LvlFormatService _lvl = App.Services.Get<LvlFormatService>();
    private ModProject? _project;
    private BaldiLevelModel _level = new(40, 40);
    private bool _painting;

    public LevelEditorView() => InitializeComponent();

    public void Bind(ModProject p)
    {
        _project = p;
        var existing = Directory.Exists(System.IO.Path.Combine(p.ProjectDir, "levels"))
            ? Directory.GetFiles(System.IO.Path.Combine(p.ProjectDir, "levels"), "*.lvl").FirstOrDefault()
            : null;
        if (existing != null)
        {
            try { _level = _lvl.Read(existing); } catch { _level = new BaldiLevelModel(40, 40); }
        }
        DrawAll();
    }

    private byte CurrentTool()
    {
        if (ToolHall.IsChecked == true) return 1;
        if (ToolClass.IsChecked == true) return 2;
        if (ToolFaculty.IsChecked == true) return 3;
        if (ToolOffice.IsChecked == true) return 4;
        return 0; // ластик
    }

    private bool IsPlacementTool() =>
        ToolDoor.IsChecked == true || ToolWindow.IsChecked == true ||
        ToolNpc.IsChecked == true || ToolItem.IsChecked == true || ToolPoster.IsChecked == true;

    private void Cell_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _painting = true;
        LevelCanvas.CaptureMouse();
        HandleAt(e.GetPosition(LevelCanvas));
    }

    private void Cell_MouseMove(object sender, MouseEventArgs e)
    {
        if (_painting && !IsPlacementTool()) HandleAt(e.GetPosition(LevelCanvas));
    }

    private void Cell_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_painting && IsPlacementTool()) HandleAt(e.GetPosition(LevelCanvas));
        _painting = false;
        LevelCanvas.ReleaseMouseCapture();
    }

    private void HandleAt(Point p)
    {
        int cx = (int)(p.X / CellSize), cy = (int)(p.Y / CellSize);
        if (!_level.InBounds(cx, cy)) return;

        if (ToolDoor.IsChecked == true) _level.Doors.Add(new DoorModel { X = cx, Y = cy, Direction = 0 });
        else if (ToolWindow.IsChecked == true) _level.Windows.Add(new WindowModel { X = cx, Y = cy, Direction = 0 });
        else if (ToolNpc.IsChecked == true) _level.Npcs.Add(new NpcSpawnModel { X = cx, Y = cy });
        else if (ToolItem.IsChecked == true) _level.Items.Add(new ItemSpawnModel { X = cx, Y = cy });
        else if (ToolPoster.IsChecked == true) _level.Posters.Add(new PosterModel { X = cx, Y = cy, Direction = 0 });
        else _level.SetCell(cx, cy, CurrentTool());

        DrawAll();
        InfoText.Text = $"Клетка ({cx};{cy}) • двери { _level.Doors.Count}, окна {_level.Windows.Count}, NPC {_level.Npcs.Count}, предметы {_level.Items.Count}, постеры {_level.Posters.Count}";
    }

    private static Brush BrushFor(byte cell) => cell switch
    {
        1 => Brushes.SandyBrown,   // коридор
        2 => Brushes.LightSteelBlue, // класс
        3 => Brushes.MistyRose,    // faculty
        4 => Brushes.PaleGreen,    // офис
        _ => Brushes.White
    };

    private void DrawCell(int cx, int cy)
    {
        var r = new Rectangle
        {
            Width = CellSize - 1,
            Height = CellSize - 1,
            Fill = BrushFor(_level.GetCell(cx, cy)),
            Stroke = Brushes.LightGray,
            StrokeThickness = 0.5
        };
        Canvas.SetLeft(r, cx * CellSize);
        Canvas.SetTop(r, cy * CellSize);
        LevelCanvas.Children.Add(r);
    }

    private void DrawAll()
    {
        LevelCanvas.Children.Clear();
        for (int x = 0; x < _level.Width; x++)
            for (int y = 0; y < _level.Height; y++)
                DrawCell(x, y);
        foreach (var d in _level.Doors) Mark(d.X, d.Y, Brushes.Brown, "🚪");
        foreach (var w in _level.Windows) Mark(w.X, w.Y, Brushes.SkyBlue, "🪟");
        foreach (var n in _level.Npcs) Mark(n.X, n.Y, Brushes.IndianRed, "🧍");
        foreach (var i in _level.Items) Mark(i.X, i.Y, Brushes.Goldenrod, "🥤");
        foreach (var p in _level.Posters) Mark(p.X, p.Y, Brushes.MediumPurple, "🖼️");
    }

    private void Mark(int cx, int cy, Brush brush, string icon)
    {
        var r = new Rectangle
        {
            Width = CellSize - 1, Height = CellSize - 1,
            Fill = brush, Opacity = 0.6
        };
        Canvas.SetLeft(r, cx * CellSize);
        Canvas.SetTop(r, cy * CellSize);
        LevelCanvas.Children.Add(r);
        var t = new TextBlock { Text = icon, FontSize = 16 };
        Canvas.SetLeft(t, cx * CellSize + 6);
        Canvas.SetTop(t, cy * CellSize + 4);
        LevelCanvas.Children.Add(t);
    }

    private void SaveLvl_Click(object sender, RoutedEventArgs e)
    {
        if (_project == null) return;
        var sfd = new SaveFileDialog
        {
            Filter = "Уровень BB+ (*.lvl)|*.lvl",
            FileName = "level1.lvl",
            InitialDirectory = Directory.Exists(System.IO.Path.Combine(_project.ProjectDir, "levels"))
                ? System.IO.Path.Combine(_project.ProjectDir, "levels")
                : _project.ProjectDir
        };
        if (sfd.ShowDialog() == true) _lvl.Write(sfd.FileName, _level);
    }

    private void OpenLvl_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Уровень BB+ (*.lvl)|*.lvl" };
        if (ofd.ShowDialog() == true)
        {
            _level = _lvl.Read(ofd.FileName);
            DrawAll();
            InfoText.Text = $"Загружен {_level.LevelTitle} ({_level.Width}x{_level.Height})";
        }
    }
}
