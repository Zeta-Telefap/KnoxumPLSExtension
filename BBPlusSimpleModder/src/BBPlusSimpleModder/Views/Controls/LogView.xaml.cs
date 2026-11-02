using System.Windows.Controls;
using System.Linq;

namespace BBPlusSimpleModder.Views.Controls;

/// <summary>Прокручиваемый лог (хвост BepInEx LogOutput.log + сообщения приложения).</summary>
public partial class LogView : UserControl
{
    private const int MaxLines = 3000;

    public LogView() => InitializeComponent();

    public void Append(string text)
    {
        Box.AppendText(text);
        if (Box.LineCount > MaxLines)
        {
            var lines = Box.Text.Split('\n');
            Box.Text = string.Join("\n", lines.Skip(lines.Length - MaxLines / 2));
        }
        Box.ScrollToEnd();
    }
}
