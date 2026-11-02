using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BBPlusSimpleModder.Core.Models;

/// <summary>Шаг сборки в окне BuildWindow (⏳ → ▶ → ✔ / ❌).</summary>
public class BuildStep : INotifyPropertyChanged
{
    private string _statusIcon = "⏳";
    private string _text;

    public BuildStep(string text) => _text = text;

    public string Text
    {
        get => _text;
        set { _text = value; OnChanged(); }
    }

    public string StatusIcon
    {
        get => _statusIcon;
        set { _statusIcon = value; OnChanged(); }
    }

    public void Running() => StatusIcon = "▶";
    public void Done() => StatusIcon = "✔";
    public void Failed() => StatusIcon = "❌";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
