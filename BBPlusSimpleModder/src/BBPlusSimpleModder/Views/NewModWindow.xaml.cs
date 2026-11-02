using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views;

/// <summary>Мастер нового мода: имя + GUID (дефолт com.knoxum.baldiplus.simplemodder.&lt;имя&gt;).</summary>
public partial class NewModWindow : Window
{
    public string GamePath { get; set; } = "";
    public string? CreatedProjectPath { get; private set; }

    private static readonly Regex GuidRx = new(@"^[a-z0-9]+(\.[a-z0-9_]+)+$", RegexOptions.Compiled);
    private bool _guidTouched;

    public NewModWindow()
    {
        InitializeComponent();
        GuidBox.TextChanged += (_, _) => _guidTouched = true;
    }

    private void Name_Changed(object sender, TextChangedEventArgs e)
    {
        if (_guidTouched) return;
        var slug = Slugify(NameBox.Text);
        if (slug.Length > 0)
            GuidBox.Text = $"com.knoxum.baldiplus.simplemodder.{slug}";
    }

    /// <summary>«Chaos School» → «chaos_school» (для GUID и имени сборки).</summary>
    public static string Slugify(string name)
        => Regex.Replace((name ?? "").ToLowerInvariant().Trim(), @"[^a-z0-9]+", "_").Trim('_');

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var name = NameBox.Text.Trim();
        var guid = GuidBox.Text.Trim();
        if (name.Length < 2) { ErrorText.Text = "Имя слишком короткое"; return; }
        if (!GuidRx.IsMatch(guid)) { ErrorText.Text = "GUID должен быть вида a.b.c (латиница, цифры, точки)"; return; }

        var projectsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BBPlusSimpleModder");
        var dir = Path.Combine(projectsRoot, guid);
        try
        {
            CreatedProjectPath = App.Services.Get<ProjectService>()
                .CreateNew(dir, name, guid, VersionBox.Text.Trim(), AuthorBox.Text.Trim(), DescBox.Text.Trim(), GamePath);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
