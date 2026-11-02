using System.Windows;
using System.Windows.Controls;
using BBPlusSimpleModder.Core.Models;
using BBPlusSimpleModder.Services;
using BBPlusSimpleModder.Views.Dialogs;
using System.Windows.Input;

namespace BBPlusSimpleModder.Views.Controls;

/// <summary>
/// Дерево компонентов мода (как иерархия в Unity): Add/Rename/Duplicate/Delete,
/// drag&drop файлов из проводника → импорт ассетов.
/// </summary>
public partial class ComponentTreeView : UserControl
{
    private ModProject? _project;
    private Action<ModComponent>? _onSelect;

    public ComponentTreeView() => InitializeComponent();

    public void Bind(ModProject project, Action<ModComponent> onSelect)
    {
        _project = project;
        _onSelect = onSelect;
        Tree.ItemsSource = new[] { project.Root };
    }

    public void Refresh() => Tree.Items.Refresh();

    private ModComponent? Selected => Tree.SelectedItem as ModComponent;

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (Selected is { } c) _onSelect?.Invoke(c);
    }

    private void AddKind_Click(object sender, RoutedEventArgs e)
    {
        if (_project == null) return;
        var tag = (sender as MenuItem)?.Tag as string ?? (sender as MenuItem)?.Header?.ToString();
        if (!Enum.TryParse<ComponentKind>(tag, out var kind))
        {
            // «➕ Добавить дочерний…» из контекстного меню — берём выделенный тип или Folder
            kind = ComponentKind.Folder;
        }
        var parent = Selected ?? _project.Root;
        var child = ModComponent.Create(kind, parent);
        parent.Children.Add(child);
        Refresh();
        _onSelect?.Invoke(child);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is null || _project == null) return;
        var dlg = new InputDialog("Новое имя:", Selected.Name) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && dlg.Value.Trim().Length > 0)
        {
            Selected.Name = dlg.Value.Trim();
            Refresh();
        }
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } c && c.Parent != null)
        {
            var copy = c.DeepCopy();
            copy.Parent = c.Parent;
            c.Parent.Children.Add(copy);
            Refresh();
            _onSelect?.Invoke(copy);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } c && c.Parent != null && c.Kind != ComponentKind.Root)
        {
            if (MessageBox.Show($"Удалить «{c.Name}» со всеми дочерними?", "Удаление",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                c.Parent.Children.Remove(c);
                Refresh();
            }
        }
    }

    private void CtxOpenGraph_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } c) _onSelect?.Invoke(c);
    }

    private void Tree_DragOver(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        if (_project == null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            var target = Selected ?? _project.Root;
            App.Services.Get<AssetImportService>().ImportFiles(files, target, _project);
            Refresh();
        }
    }
}
