using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BBPlusSimpleModder.Core.BlockEngine;
using BBPlusSimpleModder.Core.Codegen;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views.Controls;

/// <summary>
/// Редактор кирпичиков: канвас с узлами и проводами.
/// Колесо — зум, ЛКМ за шапку — drag узла, ЛКМ на порту — провод (out→in),
/// ПКМ — меню добавления блока, Del — удалить, Ctrl+Z/Y — undo/redo (JSON-снапшоты).
/// </summary>
public partial class GraphEditorView : UserControl
{
    private Graph _graph = new();
    private BlockCatalogService _catalog = null!;

    private readonly Dictionary<string, Border> _nodeViews = new();      // nodeId → Border
    private readonly Dictionary<string, Ellipse> _portVisuals = new();   // "nodeId:port" → Ellipse
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    private double _zoom = 1.0;
    private BlockNode? _dragNode;
    private Point _dragMouseStart;
    private Point _dragNodeStart;
    private BlockNode? _selected;
    private PortRef? _connectFrom;
    private Ellipse? _connectFromVisual;
    private Path? _tempWire;
    private Point _mouseGraph;

    public GraphEditorView()
    {
        InitializeComponent();
        PreviewKeyDown += GraphEditorView_PreviewKeyDown;
    }

    public void Bind(BlockCatalogService catalog)
    {
        _catalog = catalog;
        RefreshPalette("");
    }

    public void LoadGraph(Graph g, bool recordHistory = true)
    {
        _graph = g;
        Rebuild();
        if (recordHistory) PushUndo();
    }

    /// <summary>Текущий граф (живой объект компонента — изменения применяются на месте).</summary>
    public Graph CurrentGraph => _graph;

    public string PreviewCode() => new GraphToCSharp().GenerateMethod(_graph);

    // ================= палитра =================

    private void RefreshPalette(string filter) => Palette.ItemsSource = _catalog.Search(filter);

    private void PaletteSearch_Changed(object sender, TextChangedEventArgs e) => RefreshPalette(PaletteSearch.Text);

    private void Palette_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Palette.SelectedItem is BlockDefinition def)
            AddNode(def, ViewportCenter());
    }

    private Point ViewportCenter()
    {
        var x = (Scroller.HorizontalOffset + Scroller.ViewportWidth / 2) / _zoom;
        var y = (Scroller.VerticalOffset + Scroller.ViewportHeight / 2) / _zoom;
        return new Point(x, y);
    }

    // ================= узлы =================

    private void AddNode(BlockDefinition def, Point pos)
    {
        var node = new BlockNode { BlockId = def.Id, X = pos.X, Y = pos.Y };
        _graph.Nodes.Add(node);
        AddNodeView(node);
        Select(node);
        PushUndo();
    }

    private void Rebuild()
    {
        NodesCanvas.Children.Clear();
        WiresCanvas.Children.Clear();
        _nodeViews.Clear();
        _portVisuals.Clear();
        foreach (var n in _graph.Nodes) AddNodeView(n);
        RedrawWires();
    }

    private void AddNodeView(BlockNode node)
    {
        var def = _catalog.Get(node.BlockId);
        var border = new Border
        {
            Width = 200,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0x44, 0x00)),
            BorderThickness = 2,
            CornerRadius = new CornerRadius(8),
            Tag = node
        };
        var panel = new StackPanel();

        // шапка (за неё таскаем)
        var header = new Border
        {
            Background = new SolidColorBrush(CategoryColor(def?.Category ?? "?")),
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Padding = new Thickness(6, 3, 6, 3),
            Tag = node
        };
        header.Child = new TextBlock
        {
            Text = $"{def?.Icon ?? "⬜"} {def?.Title ?? node.BlockId}",
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        header.MouseLeftButtonDown += Header_MouseLeftButtonDown;
        panel.Children.Add(header);

        // порты
        if (def != null)
        {
            foreach (var port in def.Ports)
            {
                var row = new Grid { Margin = new Thickness(4, 2, 4, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var ellipse = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = PortBrush(port),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = new PortRef(node.Id, port.Name),
                    ToolTip = $"{(port.IsExec ? "exec" : port.Type.ToString())} {port.Direction} «{port.Name}»",
                    Cursor = Cursors.Cross
                };
                ellipse.MouseLeftButtonDown += Port_MouseLeftButtonDown;
                _portVisuals[$"{node.Id}:{port.Name}"] = ellipse;

                var label = new TextBlock
                {
                    Text = port.IsExec ? port.Name : $"{port.Name} ({port.Type})",
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                };

                if (port.Direction == PortDirection.In)
                {
                    Grid.SetColumn(ellipse, 0);
                    Grid.SetColumn(label, 1);
                    row.Children.Add(ellipse);
                    row.Children.Add(label);
                }
                else
                {
                    Grid.SetColumn(label, 0);
                    Grid.SetColumn(ellipse, 1);
                    label.HorizontalAlignment = HorizontalAlignment.Right;
                    row.Children.Add(label);
                    row.Children.Add(ellipse);
                }
                panel.Children.Add(row);
            }
        }

        border.Child = panel;
        border.MouseLeftButtonDown += (s, e) => Select(node);
        border.MouseDoubleClick += (s, e) => EditNodeValues(node);
        Canvas.SetLeft(border, node.X);
        Canvas.SetTop(border, node.Y);
        NodesCanvas.Children.Add(border);
        _nodeViews[node.Id] = border;
    }

    private void Select(BlockNode? node)
    {
        _selected = node;
        foreach (var kv in _nodeViews)
            kv.Value.BorderBrush = kv.Key == node?.Id
                ? new SolidColorBrush(Color.FromRgb(0xE0, 0xA8, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x5C, 0x44, 0x00));
    }

    /// <summary>Двойной клик по узлу — редактировать его значения (литералы, op, имя переменной, raw C#…).</summary>
    private void EditNodeValues(BlockNode node)
    {
        var def = _catalog.Get(node.BlockId);
        var keys = DefaultValueKeys(node.BlockId).ToList();
        foreach (var k in node.Values.Keys.ToList())
            if (!keys.Contains(k)) keys.Add(k);
        foreach (var key in keys)
        {
            node.Values.TryGetValue(key, out var cur);
            var dlg = new Dialogs.InputDialog($"{def?.Title ?? node.BlockId} — значение «{key}»:", cur?.ToString() ?? "")
            {
                Owner = Window.GetWindow(this)
            };
            if (dlg.ShowDialog() == true)
                node.Values[key] = dlg.Value;
        }
        PushUndo();
    }

    private static IEnumerable<string> DefaultValueKeys(string blockId) => blockId switch
    {
        "lit.int" or "lit.float" or "lit.bool" or "lit.string" or "lit.vector3" => new[] { "value" },
        "math.op" or "math.compare" => new[] { "op" },
        "var.get" or "stmt.setvar" => new[] { "имя" },
        "call" => new[] { "target", "method", "args" },
        "stmt.raw" => new[] { "code" },
        _ => Array.Empty<string>()
    };

    // ================= drag узла =================

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BlockNode node)
        {
            _dragNode = node;
            _dragMouseStart = e.GetPosition(NodesCanvas);
            _dragNodeStart = new Point(node.X, node.Y);
            Select(node);
            NodesCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    // ================= провода =================

    private void Port_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var ellipse = (Ellipse)sender;
        var pr = (PortRef)ellipse.Tag!;
        var portDef = GetPortDef(pr);

        if (_connectFrom == null)
        {
            // начать провод
            _connectFrom = pr;
            _connectFromVisual = ellipse;
            _tempWire = new Path { Stroke = Brushes.DarkOrange, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 2 } };
            WiresCanvas.Children.Add(_tempWire);
        }
        else
        {
            // завершить провод
            var fromDef = GetPortDef(_connectFrom);
            if (fromDef != null && portDef != null && Compatible(fromDef, portDef))
            {
                var from = fromDef.Direction == PortDirection.Out ? _connectFrom! : pr;
                var to = fromDef.Direction == PortDirection.Out ? pr : _connectFrom!;
                _graph.Wires.Add(new Wire { From = from, To = to });
                RedrawWires();
                PushUndo();
            }
            CancelConnect();
        }
        e.Handled = true;
    }

    private static bool Compatible(BlockPort a, BlockPort b)
    {
        if (a.Direction == b.Direction) return false;
        if (a.IsExec != b.IsExec) return false;
        if (a.IsExec) return true;
        return a.Type == DataType.Any || b.Type == DataType.Any || a.Type == b.Type;
    }

    private BlockPort? GetPortDef(PortRef pr)
    {
        var node = _graph.FindNode(pr.NodeId);
        var def = node == null ? null : _catalog.Get(node.BlockId);
        return def?.Ports.FirstOrDefault(p => p.Name == pr.PortName);
    }

    private void CancelConnect()
    {
        _connectFrom = null;
        _connectFromVisual = null;
        if (_tempWire != null) { WiresCanvas.Children.Remove(_tempWire); _tempWire = null; }
    }

    private Point PortCenter(Ellipse e)
        => e.TranslatePoint(new Point(e.Width / 2, e.Height / 2), NodesCanvas);

    private void RedrawWires()
    {
        WiresCanvas.Children.Clear();
        foreach (var w in _graph.Wires)
        {
            if (!_portVisuals.TryGetValue($"{w.From.NodeId}:{w.From.PortName}", out var e1)) continue;
            if (!_portVisuals.TryGetValue($"{w.To.NodeId}:{w.To.PortName}", out var e2)) continue;
            WiresCanvas.Children.Add(MakeWirePath(PortCenter(e1), PortCenter(e2), GetPortDef(w.From)));
        }
    }

    private Path MakeWirePath(Point p1, Point p2, BlockPort? portDef)
    {
        var dx = Math.Max(40, Math.Abs(p2.X - p1.X) / 2);
        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = p1 };
        fig.Segments.Add(new BezierSegment(
            new Point(p1.X + dx, p1.Y),
            new Point(p2.X - dx, p2.Y),
            p2, true));
        geo.Figures.Add(fig);
        return new Path
        {
            Data = geo,
            Stroke = portDef is { IsExec: true } ? Brushes.Black : PortBrush(portDef),
            StrokeThickness = 2
        };
    }

    // ================= мышь на канвасе =================

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Select(null);
        if (_connectFrom != null) CancelConnect();
        NodesCanvas.Focus();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        _mouseGraph = e.GetPosition(NodesCanvas);
        if (_dragNode != null)
        {
            var delta = _mouseGraph - _dragMouseStart;
            _dragNode.X = _dragNodeStart.X + delta.X;
            _dragNode.Y = _dragNodeStart.Y + delta.Y;
            if (_nodeViews.TryGetValue(_dragNode.Id, out var v))
            {
                Canvas.SetLeft(v, _dragNode.X);
                Canvas.SetTop(v, _dragNode.Y);
            }
            RedrawWires();
        }
        else if (_tempWire != null && _connectFromVisual != null)
        {
            var p1 = PortCenter(_connectFromVisual);
            _tempWire.Data = new PathGeometry();
            ((PathGeometry)_tempWire.Data).Figures.Add(new PathFigure
            {
                StartPoint = p1,
                Segments = { new BezierSegment(new Point(p1.X + 60, p1.Y), new Point(_mouseGraph.X - 60, _mouseGraph.Y), _mouseGraph, true) }
            });
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragNode != null)
        {
            _dragNode = null;
            PushUndo();
        }
        NodesCanvas.ReleaseMouseCapture();
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(NodesCanvas);
        var menu = new ContextMenu();
        foreach (var cat in _catalog.Search(""))
        {
            var catItem = new MenuItem { Header = $"{cat.Category} ({cat.Blocks.Count})" };
            foreach (var def in cat.Blocks.Take(60))
            {
                var mi = new MenuItem { Header = $"{def.Icon} {def.Title}", Tag = def };
                mi.Click += (_, _) => AddNode(def, pos);
                catItem.Items.Add(mi);
            }
            menu.Items.Add(catItem);
        }
        menu.IsOpen = true;
        e.Handled = true;
    }

    // ================= зум =================

    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        double newZoom = Math.Clamp(_zoom * factor, 0.2, 3.0);
        var mp = e.GetPosition(Scroller);
        double ratio = newZoom / _zoom;
        Scroller.ScrollToHorizontalOffset((Scroller.HorizontalOffset + mp.X) * ratio - mp.X);
        Scroller.ScrollToVerticalOffset((Scroller.VerticalOffset + mp.Y) * ratio - mp.Y);
        _zoom = newZoom;
        Zoom.ScaleX = Zoom.ScaleY = _zoom;
        e.Handled = true;
    }

    // ================= клавиатура / undo =================

    private void GraphEditorView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return;   // печатаем в поле — не трогаем граф
        if (e.Key == Key.Delete && _selected != null) { DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { Undo(); e.Handled = true; }
        else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { Redo(); e.Handled = true; }
        else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { PaletteSearch.Focus(); e.Handled = true; }
    }

    private void DeleteSelected()
    {
        if (_selected == null) return;
        _graph.Nodes.Remove(_selected);
        _graph.Wires.RemoveAll(w => w.From.NodeId == _selected.Id || w.To.NodeId == _selected.Id);
        Select(null);
        Rebuild();
        PushUndo();
    }

    private void PushUndo()
    {
        _undo.Push(_graph.ToJson());
        if (_undo.Count > 100) { var arr = _undo.ToArray(); _undo.Clear(); foreach (var s in arr.Take(100)) _undo.Push(s); }
        _redo.Clear();
    }

    private void Undo()
    {
        if (_undo.Count <= 1) return;
        _redo.Push(_undo.Pop());
        LoadGraph(Graph.FromJson(_undo.Peek()), recordHistory: false);
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(_redo.Pop());
        LoadGraph(Graph.FromJson(_undo.Peek()), recordHistory: false);
    }

    // ================= цвета =================

    private static Brush PortBrush(BlockPort? p)
    {
        if (p == null) return Brushes.Gray;
        if (p.IsExec) return Brushes.White;
        return p.Type switch
        {
            DataType.Bool => Brushes.Gold,
            DataType.Int => Brushes.SteelBlue,
            DataType.Float => Brushes.CornflowerBlue,
            DataType.String => Brushes.OrangeRed,
            DataType.Vector3 => Brushes.SeaGreen,
            DataType.Color => Brushes.Magenta,
            DataType.NPC or DataType.Entity or DataType.PlayerManager => Brushes.IndianRed,
            DataType.EnvironmentController or DataType.RoomController or DataType.Cell => Brushes.Purple,
            DataType.SoundObject or DataType.AudioClip => Brushes.Teal,
            DataType.Texture2D => Brushes.Tomato,
            _ => Brushes.Gray
        };
    }

    private static Color CategoryColor(string category)
    {
        // стабильный пастельный цвет по имени категории
        int hash = category.Aggregate(0, (h, c) => h * 31 + c);
        var hue = (hash % 360 + 360) % 360;
        return HslToColor(hue, 0.55, 0.45);
    }

    private static Color HslToColor(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = l - c / 2;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return Color.FromRgb(
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255));
    }
}
