using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BBPlusSimpleModder.Core.Models;

namespace BBPlusSimpleModder.Views.Controls;

/// <summary>
/// Инспектор свойств выбранного компонента («как в Unity»):
/// строка/число/bool/enum/цвет редактируются по типу значения.
/// </summary>
public partial class InspectorView : UserControl
{
    public event Action<ModComponent>? OpenGraphRequested;

    private ModComponent? _component;

    public InspectorView() => InitializeComponent();

    public void Show(ModComponent c)
    {
        _component = c;
        PropsPanel.Children.Clear();
        OpenGraphBtn.Visibility = c.HasGraph ? Visibility.Visible : Visibility.Collapsed;

        PropsPanel.Children.Add(new TextBlock
        {
            Text = $"{c.Icon} {c.Kind} — {c.Name}",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        if (c.Properties.Count == 0)
            PropsPanel.Children.Add(new TextBlock { Text = "(нет редактируемых свойств)", Opacity = 0.6 });

        foreach (var kv in c.Properties.ToList())
            PropsPanel.Children.Add(BuildEditor(kv.Key, Unwrap(kv.Value), v => c.Properties[kv.Key] = v));
    }

    /// <summary>JsonElement (после загрузки JSON) → обычный .NET примитив.</summary>
    private static object? Unwrap(object? v) => v switch
    {
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number => je.TryGetInt32(out var i) ? i : je.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => je.ToString()
        },
        _ => v
    };

    private static UIElement BuildEditor(string key, object? value, Action<object?> set)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new TextBlock { Text = key, FontWeight = FontWeights.Bold });

        switch (value)
        {
            case bool b:
            {
                var cb = new CheckBox { IsChecked = b, Content = b ? "да" : "нет" };
                cb.Checked += (_, _) => { set(true); cb.Content = "да"; };
                cb.Unchecked += (_, _) => { set(false); cb.Content = "нет"; };
                panel.Children.Add(cb);
                break;
            }
            case int i:
            {
                var tb = new TextBox { Text = i.ToString(CultureInfo.InvariantCulture) };
                tb.TextChanged += (_, _) => { if (int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) set(v); };
                panel.Children.Add(tb);
                break;
            }
            case float or double or decimal:
            {
                var tb = new TextBox { Text = Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.###", CultureInfo.InvariantCulture) };
                tb.TextChanged += (_, _) => { if (double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) set((float)v); };
                panel.Children.Add(tb);
                break;
            }
            case Enum en:
            {
                var combo = new ComboBox { ItemsSource = Enum.GetValues(en.GetType()), SelectedItem = en };
                combo.SelectionChanged += (_, _) => set(combo.SelectedItem);
                panel.Children.Add(combo);
                break;
            }
            case string s when s.StartsWith('#') && s.Length >= 7:
            {
                // цвет в виде #RRGGBB
                var tb = new TextBox { Text = s };
                var preview = new Border
                {
                    Width = 24, Height = 24, Margin = new Thickness(0, 4, 0, 0),
                    BorderBrush = Brushes.Gray, BorderThickness = 1
                };
                try { preview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s)); } catch { }
                tb.TextChanged += (_, _) =>
                {
                    set(tb.Text);
                    try { preview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text)); } catch { }
                };
                panel.Children.Add(tb);
                panel.Children.Add(preview);
                break;
            }
            default:
            {
                var tb = new TextBox { Text = value?.ToString() ?? "" };
                tb.TextChanged += (_, _) => set(tb.Text);
                panel.Children.Add(tb);
                break;
            }
        }
        return panel;
    }

    private void OpenGraph_Click(object sender, RoutedEventArgs e)
    {
        if (_component != null) OpenGraphRequested?.Invoke(_component);
    }
}
