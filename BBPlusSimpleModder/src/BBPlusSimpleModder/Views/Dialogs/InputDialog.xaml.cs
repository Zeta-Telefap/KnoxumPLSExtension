using System.Windows;

namespace BBPlusSimpleModder.Views.Dialogs;

/// <summary>Маленький диалог ввода строки (для «Переименовать» и т.п.).</summary>
public partial class InputDialog : Window
{
    public string Value => ValueBox.Text;

    public InputDialog(string prompt, string initial)
    {
        InitializeComponent();
        Title = prompt;
        PromptText.Text = prompt;
        ValueBox.Text = initial;
        ValueBox.SelectAll();
        Loaded += (_, _) => ValueBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
