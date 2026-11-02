# BB+ Simple Modder — структура окон: XAML + code-behind (.xaml.cs)

Версия дизайна: 2026-11-02. Стек: **WPF, .NET 8 (`net8.0-windows`)**, XAML-first
(проект открывается в **Blend for Visual Studio**), MVVM-lite (CommunityToolkit.Mvvm),
весь UI на **русском**, тема — минимализм + **Comic Sans** (`Themes/MinimalComicTheme.xaml`).

Навигационная карта приложения:

```
App (StartupUri = Views/StartupWindow.xaml)
 └─ StartupWindow ──► (есть проект?) ──► MainWindow
 │                    └─ нет ──► NewModWindow ──► MainWindow
 ├─ MainWindow ──► SettingsWindow (модально)
 │             ├─► BuildWindow (модально, «Собрать»)
 │             └─► ModBrowserWindow (импорт чужого DLL/.lvl)
 └─ MainWindow содержит UserControl'ы:
      ComponentTreeView | InspectorView | GraphEditorView | AssetBrowserView | LevelEditorView | LogView
```

Модели, на которые ссылаются binding'и (Models/):
`ModProject { Guid, Name, Version, Author, GamePath, Components(root), Assets, Settings }`,
`ModComponent { Id, Name, Kind (enum: Floor, FloorGen, Object, Character, Item, Event, Sprite, Model, Audio, UiScreen, Patch, Settings…), Children, Properties (Dictionary<string,object>), Graph? (для логики), AssetRef? }`,
`Graph { Nodes: List<BlockNode>, Wires: List<Wire>, Variables }`,
`BlockNode { Id, BlockId, X, Y, PortValues: Dictionary<string,object> }`,
`BlockDefinition { Id, Title, Category, Icon, Inputs/Outputs (exec), DataPorts (name, type, direction) }`,
`AssetEntry { Id, Name, Kind, FilePath (относительно проекта), Size }`.

---

## 0. App.xaml + тема

### `App.xaml`
```xml
<Application x:Class="BBPlusSimpleModder.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Views/StartupWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/Themes/MinimalComicTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### `App.xaml.cs`
```csharp
using System.Windows;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder;

public partial class App : Application
{
    /// <summary>Реестр сервисов приложения (DI-lite).</summary>
    public static ServiceRegistry Services { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        Services.RegisterAll();                 // см. Services/ServiceRegistry.cs
        Services.Get<Logger>().Init();
        base.OnStartup(e);
    }
}
```

### `Themes/MinimalComicTheme.xaml`
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Базовый стиль: Comic Sans везде, крупный шрифт, тёплый минимализм -->
    <Style TargetType="Control">
        <Setter Property="FontFamily" Value="Comic Sans MS"/>
        <Setter Property="FontSize" Value="14"/>
    </Style>
    <Style TargetType="Window">
        <Setter Property="Background" Value="#FFFDF6"/>
        <Setter Property="FontFamily" Value="Comic Sans MS"/>
    </Style>
    <Style TargetType="Button">
        <Setter Property="Padding" Value="14,8"/>
        <Setter Property="Margin" Value="4"/>
        <Setter Property="Background" Value="#FFE082"/>
        <Setter Property="BorderBrush" Value="#E0A800"/>
        <Setter Property="BorderThickness" Value="2"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="10">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="{TemplateBinding Padding}"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#FFD54F"/>
            </Trigger>
        </Style.Triggers>
    </Style>
    <Style TargetType="TextBox">
        <Setter Property="Padding" Value="8,6"/>
        <Setter Property="Margin" Value="4"/>
        <Setter Property="BorderBrush" Value="#C9B458"/>
        <Setter Property="BorderThickness" Value="2"/>
        <Setter Property="Background" Value="White"/>
    </Style>
    <Style TargetType="TreeViewItem">
        <Setter Property="Padding" Value="4,2"/>
    </Style>
    <Style x:Key="HeaderLabel" TargetType="TextBlock">
        <Setter Property="FontSize" Value="18"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="Margin" Value="4,8"/>
    </Style>
</ResourceDictionary>
```

---

## 1. StartupWindow — выбор папки игры, проверка окружения, автоустановка

Назначение: первый экран (§2 шаг 1). Папка игры, статусы BB+/BepInEx/API/PLS с кнопками
«Установить», список недавних проектов, кнопка «Продолжить».

### `Views/StartupWindow.xaml`
```xml
<Window x:Class="BBPlusSimpleModder.Views.StartupWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="BB+ Simple Modder — выберите папку игры" Height="560" Width="760"
        WindowStartupLocation="CenterScreen" AllowDrop="True"
        DragEnter="Window_DragEnter" Drop="Window_Drop">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Style="{StaticResource HeaderLabel}" Text="🎒 BB+ Simple Modder"/>

        <!-- Папка игры -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="Папка игры BB+:" VerticalAlignment="Center" Margin="4"/>
            <TextBox x:Name="GamePathBox" Grid.Column="1" IsReadOnly="True"
                     ToolTip="Перетащите папку игры сюда или нажмите «Обзор…»"/>
            <Button Grid.Column="2" Content="Обзор…" Click="Browse_Click"/>
        </Grid>

        <!-- Статусы окружения -->
        <GroupBox Grid.Row="2" Header="Состояние игры" Margin="0,8">
            <ListView x:Name="EnvList" BorderThickness="0" Background="Transparent">
                <ListView.ItemTemplate>
                    <DataTemplate>
                        <Grid Margin="2">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="{Binding Title}" VerticalAlignment="Center"/>
                            <TextBlock Grid.Column="1" Text="{Binding Status}" VerticalAlignment="Center" Margin="8,0"/>
                            <Button Grid.Column="2" Content="{Binding ActionText}" Click="Install_Click"
                                    Tag="{Binding}" Visibility="{Binding CanInstall, Converter={StaticResource BoolToVis}}"/>
                        </Grid>
                    </DataTemplate>
                </ListView.ItemTemplate>
            </ListView>
        </GroupBox>

        <!-- Недавние проекты -->
        <GroupBox Grid.Row="3" Header="Недавние моды" MaxHeight="140">
            <ListBox x:Name="RecentList" MouseDoubleClick="Recent_DoubleClick"
                     DisplayMemberPath="Display"/>
        </GroupBox>

        <StackPanel Grid.Row="4" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button x:Name="NewModBtn" Content="✨ Новый мод" Click="NewMod_Click" IsEnabled="False"/>
            <Button x:Name="OpenModBtn" Content="📂 Открыть мод…" Click="OpenMod_Click"/>
            <Button x:Name="ContinueBtn" Content="Продолжить ▶" Click="Continue_Click" IsEnabled="False"/>
        </StackPanel>
    </Grid>
</Window>
```

### `Views/StartupWindow.xaml.cs`
```csharp
using System.IO;
using System.Windows;
using Microsoft.Win32;
using BBPlusSimpleModder.Services;
using BBPlusSimpleModder.ViewModels;

namespace BBPlusSimpleModder.Views;

public partial class StartupWindow : Window
{
    private readonly StartupViewModel _vm = new();
    private readonly GameEnvironmentService _env = App.Services.Get<GameEnvironmentService>();
    private readonly SettingsService _settings = App.Services.Get<SettingsService>();

    public StartupWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await OnLoaded();
    }

    private async Task OnLoaded()
    {
        var lastGame = _settings.LastGamePath;
        if (!string.IsNullOrEmpty(lastGame) && Directory.Exists(lastGame))
            await SetGamePath(lastGame);
        _vm.Recent = _settings.RecentProjects;
        RecentList.ItemsSource = _vm.Recent;
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        // FolderBrowserDialog или WinForms FolderBrowserDialog; здесь — OpenFolderDialog (WinForms)
        using var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Папка игры Baldi's Basics Plus" };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            await SetGamePath(dlg.SelectedPath);
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files?.Length > 0) await SetGamePath(files[0]);
    }

    private async Task SetGamePath(string path)
    {
        GamePathBox.Text = path;
        _settings.LastGamePath = path;
        _vm.EnvItems = await _env.DetectAsync(path);   // список EnvStatusItem {Title, Status, CanInstall, ActionText}
        EnvList.ItemsSource = _vm.EnvItems;
        UpdateButtons();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is EnvStatusItem item && GamePathBox.Text is string path)
        {
            item.Status = "Скачиваю…";
            EnvList.Items.Refresh();
            await _env.InstallAsync(path, item.Kind);      // Kind: BepInEx | DevApi | LevelStudio | LevelLoader
            await SetGamePath(path);                        // перепроверить
        }
    }

    private void UpdateButtons()
    {
        bool envOk = _env.IsReady(GamePathBox.Text);       // BepInEx + DevApi стоят
        NewModBtn.IsEnabled = envOk;
        ContinueBtn.IsEnabled = envOk && _vm.Recent.Count > 0;
    }

    private void NewMod_Click(object sender, RoutedEventArgs e)
    {
        var w = new NewModWindow { GamePath = GamePathBox.Text };
        if (w.ShowDialog() == true) OpenMain(w.CreatedProjectPath);
    }

    private void OpenMod_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Проект мода (*.bbmod)|*.bbmod" };
        if (ofd.ShowDialog() == true) OpenMain(ofd.FileName);
    }

    private void Recent_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (RecentList.SelectedItem is RecentProject rp) OpenMain(rp.Path);
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Recent.Count > 0) OpenMain(_vm.Recent[0].Path);
    }

    private void OpenMain(string projectPath)
    {
        var main = new MainWindow(projectPath);
        main.Show();
        Close();
    }
}
```

---

## 2. NewModWindow — мастер нового мода (имя + GUID)

### `Views/NewModWindow.xaml`
```xml
<Window x:Class="BBPlusSimpleModder.Views.NewModWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Новый мод" SizeToContent="WidthAndHeight" WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize">
    <Grid Margin="14" Width="460">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <!-- x6 полей -->
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="130"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <TextBlock Style="{StaticResource HeaderLabel}" Text="✨ Новый мод для BB+"
                   Grid.ColumnSpan="2"/>

        <TextBlock Grid.Row="1" Text="Имя мода:" VerticalAlignment="Center"/>
        <TextBox x:Name="NameBox" Grid.Row="1" Grid.Column="1" TextChanged="Name_Changed"
                 ToolTip="Например: Chaos School"/>

        <TextBlock Grid.Row="2" Text="GUID плагина:" VerticalAlignment="Center"/>
        <TextBox x:Name="GuidBox" Grid.Row="2" Grid.Column="1"
                 ToolTip="com.knoxum.baldiplus.simplemodder.&lt;имя&gt;"/>

        <TextBlock Grid.Row="3" Text="Версия:" VerticalAlignment="Center"/>
        <TextBox x:Name="VersionBox" Grid.Row="3" Grid.Column="1" Text="1.0.0"/>

        <TextBlock Grid.Row="4" Text="Автор:" VerticalAlignment="Center"/>
        <TextBox x:Name="AuthorBox" Grid.Row="4" Grid.Column="1"/>

        <TextBlock Grid.Row="5" Text="Описание:" VerticalAlignment="Center"/>
        <TextBox x:Name="DescBox" Grid.Row="5" Grid.Column="1" Height="60" TextWrapping="Wrap" AcceptsReturn="True"/>

        <StackPanel Grid.Row="6" Grid.ColumnSpan="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
            <TextBlock x:Name="ErrorText" Foreground="Red" VerticalAlignment="Center" Margin="0,0,8,0"/>
            <Button Content="Отмена" Click="Cancel_Click"/>
            <Button x:Name="OkBtn" Content="Создать ▶" Click="Ok_Click" IsDefault="True"/>
        </StackPanel>
    </Grid>
</Window>
```

### `Views/NewModWindow.xaml.cs`
```csharp
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views;

public partial class NewModWindow : Window
{
    public string GamePath { get; set; } = "";
    public string? CreatedProjectPath { get; private set; }

    private static readonly Regex GuidRx = new(@"^[a-z0-9]+(\.[a-z0-9_]+)+$", RegexOptions.Compiled);

    public NewModWindow() => InitializeComponent();

    private void Name_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // автогенерация GUID из имени: "Chaos School" -> com.knoxum.baldiplus.simplemodder.chaos_school
        var slug = Regex.Replace(NameBox.Text.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "_").Trim('_');
        if (!string.IsNullOrEmpty(slug) && string.IsNullOrEmpty(GuidBox.Text) || GuidBox.Text.StartsWith("com.knoxum."))
            GuidBox.Text = $"com.knoxum.baldiplus.simplemodder.{slug}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var name = NameBox.Text.Trim();
        var guid = GuidBox.Text.Trim();
        if (name.Length < 2) { ErrorText.Text = "Имя слишком короткое"; return; }
        if (!GuidRx.IsMatch(guid)) { ErrorText.Text = "GUID должен быть вида a.b.c (латиница, цифры, точки)"; return; }

        var projectsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BBPlusSimpleModder");
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

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
```

---

## 3. MainWindow — главное окно (дерево | вкладки | инспектор | лог)

### `Views/MainWindow.xaml`
```xml
<Window x:Class="BBPlusSimpleModder.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:BBPlusSimpleModder.Views.Controls"
        Title="BB+ Simple Modder" Height="800" Width="1280" WindowStartupLocation="CenterScreen">
    <DockPanel>
        <!-- Верхняя панель -->
        <Border DockPanel.Dock="Top" Background="#FFF3C4" BorderBrush="#E0A800" BorderThickness="0,0,0,2">
            <StackPanel Orientation="Horizontal" Margin="6">
                <TextBlock x:Name="ModTitle" FontSize="16" FontWeight="Bold" VerticalAlignment="Center" Margin="4,0,12,0"/>
                <Button Content="💾 Сохранить" Click="Save_Click"/>
                <Button Content="🔨 Собрать" Click="Build_Click"/>
                <Button Content="▶ Играть" Click="Play_Click"/>
                <Separator Margin="6,2"/>
                <Button Content="📥 Импорт мода/уровня" Click="Import_Click"/>
                <Button Content="⚙ Настройки" Click="Settings_Click"/>
                <TextBlock x:Name="GamePathLabel" VerticalAlignment="Center" Margin="12,0,0,0" Opacity="0.7"/>
            </StackPanel>
        </Border>

        <!-- Статус-бар -->
        <StatusBar DockPanel.Dock="Bottom">
            <StatusBarItem><TextBlock x:Name="StatusText" Text="Готово"/></StatusBarItem>
            <Separator/>
            <StatusBarItem><TextBlock x:Name="GraphInfoText"/></StatusBarItem>
        </StatusBar>

        <!-- Лог сборки/игры (снизу, сворачивается) -->
        <Expander DockPanel.Dock="Bottom" Header="📜 Лог (BepInEx / сборка)" MaxHeight="180" IsExpanded="False">
            <controls:LogView x:Name="Log"/>
        </Expander>

        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="300" MinWidth="200"/>
                <ColumnDefinition Width="5"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="5"/>
                <ColumnDefinition Width="320" MinWidth="220"/>
            </Grid.ColumnDefinitions>

            <!-- Дерево компонентов -->
            <controls:ComponentTreeView x:Name="Tree" Grid.Column="0"/>

            <GridSplitter Grid.Column="1" HorizontalAlignment="Stretch" Background="#E0A800"/>

            <!-- Центр: вкладки -->
            <TabControl x:Name="CenterTabs" Grid.Column="2" Margin="4">
                <TabItem Header="🧩 Логика (кирпичики)">
                    <controls:GraphEditorView x:Name="GraphEditor"/>
                </TabItem>
                <TabItem Header="🖼 Ассеты">
                    <controls:AssetBrowserView x:Name="Assets"/>
                </TabItem>
                <TabItem Header="🗺 Уровень">
                    <controls:LevelEditorView x:Name="LevelEditor"/>
                </TabItem>
                <TabItem Header="📄 Сгенерированный код">
                    <TextBox x:Name="CodePreview" IsReadOnly="True" FontFamily="Consolas" FontSize="12"
                             VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"/>
                </TabItem>
            </TabControl>

            <GridSplitter Grid.Column="3" HorizontalAlignment="Stretch" Background="#E0A800"/>

            <!-- Инспектор -->
            <controls:InspectorView x:Name="Inspector" Grid.Column="4"/>
        </Grid>
    </DockPanel>
</Window>
```

### `Views/MainWindow.xaml.cs`
```csharp
using System.IO;
using System.Windows;
using System.Windows.Threading;
using BBPlusSimpleModder.Models;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views;

public partial class MainWindow : Window
{
    private readonly ProjectService _projects = App.Services.Get<ProjectService>();
    private readonly BuildService _build = App.Services.Get<BuildService>();
    private readonly GameEnvironmentService _env = App.Services.Get<GameEnvironmentService>();
    private readonly DispatcherTimer _logTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public ModProject Project { get; private set; } = null!;
    public string ProjectPath { get; }

    public MainWindow(string projectPath)
    {
        InitializeComponent();
        ProjectPath = projectPath;
        Loaded += OnLoaded;
        Closing += (_, _) => _logTimer.Stop();
    }

    private void OnLoaded()
    {
        Project = _projects.Load(ProjectPath);
        Title = $"BB+ Simple Modder — {Project.Name} ({Project.Guid})";
        ModTitle.Text = $"{Project.Name}  [{Project.Guid}]";
        GamePathLabel.Text = Project.GamePath;
        Tree.Bind(Project.Root, component => SelectComponent(component));
        Assets.Bind(Project);
        LevelEditor.Bind(Project);
        GraphEditor.Bind(App.Services.Get<BlockCatalogService>(), App.Services.Get<CodegenService>());
        _logTimer.Tick += (_, _) => Log.Append(_env.ReadLogTail(Project.GamePath));
        _logTimer.Start();
        StatusText.Text = "Проект загружен";
    }

    private void SelectComponent(ModComponent c)
    {
        Inspector.Show(c);
        if (c.Graph != null) { CenterTabs.SelectedIndex = 0; GraphEditor.LoadGraph(c.Graph); }
        GraphInfoText.Text = c.Graph != null ? $"Граф: {c.Graph.Nodes.Count} блоков" : "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        GraphEditor.SaveCurrent();
        _projects.Save(ProjectPath, Project);
        StatusText.Text = "Сохранено ✔";
    }

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        Save_Click(sender, e);
        var w = new BuildWindow(Project, ProjectPath);
        w.ShowDialog();
        CodePreview.Text = _build.LastGeneratedCode;   // предпросмотр C#
    }

    private void Play_Click(object sender, RoutedEventArgs e)
        => _env.LaunchGame(Project.GamePath);

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var w = new ModBrowserWindow(Project);
        w.ShowDialog();
        Tree.Refresh();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow(Project.Settings) { Owner = this };
        if (w.ShowDialog() == true) _projects.Save(ProjectPath, Project);
    }
}
```

---

## 4. ComponentTreeView — дерево компонентов (слева в MainWindow)

XAML: TreeView с HierarchicalDataTemplate + панель инструментов (Add ▾, ✏, 📑, 🗑) и контекстное меню.

### `Views/Controls/ComponentTreeView.xaml`
```xml
<UserControl x:Class="BBPlusSimpleModder.Views.Controls.ComponentTreeView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:models="clr-namespace:BBPlusSimpleModder.Models">
    <DockPanel>
        <ToolBar DockPanel.Dock="Top">
            <Menu>
                <MenuItem Header="➕ Добавить">
                    <MenuItem Header="🗼 Этаж" Tag="Floor" Click="AddKind_Click"/>
                    <MenuItem Header="⚙ Компонент генерации этажа" Tag="FloorGen" Click="AddKind_Click"/>
                    <MenuItem Header="🚪 Объект (шкафчик, стол…)" Tag="Object" Click="AddKind_Click"/>
                    <MenuItem Header="🧍 Персонаж" Tag="Character" Click="AddKind_Click"/>
                    <MenuItem Header="🥤 Предмет" Tag="Item" Click="AddKind_Click"/>
                    <MenuItem Header="⚡ Событие" Tag="Event" Click="AddKind_Click"/>
                    <MenuItem Header="🖼 Спрайт" Tag="Sprite" Click="AddKind_Click"/>
                    <MenuItem Header="🧊 3D-модель" Tag="Model" Click="AddKind_Click"/>
                    <MenuItem Header="🔊 Аудио" Tag="Audio" Click="AddKind_Click"/>
                    <MenuItem Header="🖥 Экран UI" Tag="UiScreen" Click="AddKind_Click"/>
                    <MenuItem Header="🩹 Harmony-патч" Tag="Patch" Click="AddKind_Click"/>
                </MenuItem>
            </Menu>
            <Button Content="✏" ToolTip="Переименовать" Click="Rename_Click"/>
            <Button Content="📑" ToolTip="Дублировать" Click="Duplicate_Click"/>
            <Button Content="🗑" ToolTip="Удалить" Click="Delete_Click"/>
        </ToolBar>
        <TreeView x:Name="Tree" AllowDrop="True"
                  TreeViewItem.Expanded="TreeItem_Expanded"
                  SelectedItemChanged="Tree_SelectedItemChanged"
                  MouseRightButtonDown="Tree_MouseRightButtonDown"
                  Drop="Tree_Drop" DragOver="Tree_DragOver">
            <TreeView.Resources>
                <HierarchicalDataTemplate DataType="{x:Type models:ModComponent}"
                                          ItemsSource="{Binding Children}">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="{Binding Icon}" Margin="0,0,6,0"/>
                        <TextBlock Text="{Binding Name}"/>
                    </StackPanel>
                </HierarchicalDataTemplate>
            </TreeView.Resources>
            <TreeView.ContextMenu>
                <ContextMenu>
                    <MenuItem Header="➕ Добавить дочерний…" Click="CtxAdd_Click"/>
                    <MenuItem Header="✏ Переименовать" Click="Rename_Click"/>
                    <MenuItem Header="📑 Дублировать" Click="Duplicate_Click"/>
                    <MenuItem Header="🧩 Открыть логику (кирпичики)" Click="CtxOpenGraph_Click"/>
                    <MenuItem Header="🗑 Удалить" Click="Delete_Click"/>
                </ContextMenu>
            </TreeView.ContextMenu>
        </TreeView>
    </DockPanel>
</UserControl>
```

### `Views/Controls/ComponentTreeView.xaml.cs`
```csharp
using System.Windows;
using System.Windows.Controls;
using BBPlusSimpleModder.Models;

namespace BBPlusSimpleModder.Views.Controls;

public partial class ComponentTreeView : UserControl
{
    private ModComponent? _root;
    private Action<ModComponent>? _onSelect;

    public ComponentTreeView() => InitializeComponent();

    public void Bind(ModComponent root, Action<ModComponent> onSelect)
    {
        _root = root; _onSelect = onSelect;
        Tree.ItemsSource = new[] { root };
    }

    public void Refresh() => Tree.Items.Refresh();

    private ModComponent? Selected => Tree.SelectedItem as ModComponent;

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (Selected is { } c) _onSelect?.Invoke(c);
    }

    private void AddKind_Click(object sender, RoutedEventArgs e)
    {
        var kind = Enum.Parse<ComponentKind>((string)((MenuItem)sender).Tag);
        var parent = Selected ?? _root!;
        var child = ModComponent.Create(kind, parent);
        parent.Children.Add(child);
        Refresh();
        _onSelect?.Invoke(child);
    }

    private void CtxAdd_Click(object sender, RoutedEventArgs e) => AddKind_Click(sender, e);

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is null) return;
        var box = new Views.Dialogs.InputDialog("Новое имя:", Selected.Name) { Owner = Window.GetWindow(this) };
        if (box.ShowDialog() == true) { Selected.Name = box.Value; Refresh(); }
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } c && c.Parent != null)
        {
            var copy = c.DeepCopy();
            c.Parent.Children.Add(copy);
            Refresh();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } c && c.Parent != null &&
            MessageBox.Show($"Удалить «{c.Name}» со всеми дочерними?", "Удаление",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            c.Parent.Children.Remove(c);
            Refresh();
        }
    }

    private void CtxOpenGraph_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } c) _onSelect?.Invoke(c);   // MainWindow переключит вкладку на граф
    }

    // drag&drop файлов из проводника → ассеты
    private void Tree_DragOver(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var target = Selected ?? _root!;
        App.Services.Get<AssetImportService>().ImportFiles(files!, target);
        Refresh();
    }

    private void TreeItem_Expanded(object sender, RoutedEventArgs e) { }
    private void Tree_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
}
```

---

## 5. InspectorView — свойства выбранного компонента (справа)

Назначение: «как в Unity» — у выбранного узла редактируются свойства: строки, числа,
bool, enum, цвет, ссылка на ассет; для компонентов с логикой — кнопка «🧩 Логика».

### `Views/Controls/InspectorView.xaml`
```xml
<UserControl x:Class="BBPlusSimpleModder.Views.Controls.InspectorView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DockPanel>
        <TextBlock DockPanel.Dock="Top" Style="{StaticResource HeaderLabel}" Text="🔍 Инспектор"/>
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button x:Name="OpenGraphBtn" Content="🧩 Логика (кирпичики)" Click="OpenGraph_Click" Visibility="Collapsed"/>
        </StackPanel>
        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <StackPanel x:Name="PropsPanel" Margin="6"/>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

### `Views/Controls/InspectorView.xaml.cs`
```csharp
using System.Windows;
using System.Windows.Controls;
using BBPlusSimpleModder.Models;

namespace BBPlusSimpleModder.Views.Controls;

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

        PropsPanel.Children.Add(new TextBlock { Text = $"{c.Icon} {c.Kind}", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) });

        foreach (var (key, value) in c.Properties)
            PropsPanel.Children.Add(BuildEditor(key, value, v => c.Properties[key] = v));
    }

    private static UIElement BuildEditor(string key, object value, Action<object> set)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new TextBlock { Text = key });
        switch (value)
        {
            case bool b:
                var cb = new CheckBox { IsChecked = b };
                cb.Checked += (_, _) => set(true); cb.Unchecked += (_, _) => set(false);
                panel.Children.Add(cb); break;
            case int i:
                var ti = new TextBox { Text = i.ToString() };
                ti.TextChanged += (_, _) => { if (int.TryParse(ti.Text, out var v)) set(v); };
                panel.Children.Add(ti); break;
            case float f:
                var tf = new TextBox { Text = f.ToString("0.###") };
                tf.TextChanged += (_, _) => { if (float.TryParse(tf.Text, out var v)) set(v); };
                panel.Children.Add(tf); break;
            case Enum en:
                var combo = new ComboBox { ItemsSource = Enum.GetValues(en.GetType()), SelectedItem = en };
                combo.SelectionChanged += (_, _) => set(combo.SelectedItem!);
                panel.Children.Add(combo); break;
            default:
                var ts = new TextBox { Text = value?.ToString() ?? "" };
                ts.TextChanged += (_, _) => set(ts.Text);
                panel.Children.Add(ts); break;
        }
        return panel;
    }

    private void OpenGraph_Click(object sender, RoutedEventArgs e)
    {
        if (_component != null) OpenGraphRequested?.Invoke(_component);
    }
}
```

---

## 6. GraphEditorView — редактор кирпичиков (центральная вкладка, самое большое окно)

Назначение: канвас с узлами и проводами. Слева — палитра блоков с поиском,
в центре — канвас (pan/zoom, drag узлов, рисование проводов), ПКМ — «добавить блок сюда».

### `Views/Controls/GraphEditorView.xaml`
```xml
<UserControl x:Class="BBPlusSimpleModder.Views.Controls.GraphEditorView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:engine="clr-namespace:BBPlusSimpleModder.BlockEngine">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="280" MinWidth="180"/>
            <ColumnDefinition Width="5"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- Палитра блоков -->
        <DockPanel Grid.Column="0">
            <TextBox x:Name="PaletteSearch" DockPanel.Dock="Top" Margin="4"
                     TextChanged="PaletteSearch_Changed" ToolTip="Поиск блока… (Ctrl+F)"/>
            <TreeView x:Name="Palette" MouseDoubleClick="Palette_DoubleClick">
                <TreeView.Resources>
                    <HierarchicalDataTemplate ItemsSource="{Binding Blocks}">
                        <HierarchicalDataTemplate.ItemTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding Title}" ToolTip="{Binding Description}"
                                           MouseLeftButtonDown="Block_MouseLeftButtonDown" Tag="{Binding}"/>
                            </DataTemplate>
                        </HierarchicalDataTemplate.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding Category}" FontWeight="Bold"/>
                        </DataTemplate>
                    </HierarchicalDataTemplate>
                </TreeView.Resources>
            </TreeView>
        </DockPanel>

        <GridSplitter Grid.Column="1" HorizontalAlignment="Stretch" Background="#E0A800"/>

        <!-- Канвас графа -->
        <Grid Grid.Column="2" Background="#FFFBEA">
            <ScrollViewer x:Name="Scroller" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto"
                          PreviewMouseWheel="Scroller_PreviewMouseWheel" ScrollChanged="Scroller_ScrollChanged">
                <Grid x:Name="ZoomHost" Width="4000" Height="3000">
                    <Canvas x:Name="WiresCanvas" IsHitTestVisible="False"/>
                    <Canvas x:Name="NodesCanvas"
                            Background="Transparent"
                            MouseLeftButtonDown="Canvas_MouseLeftButtonDown"
                            MouseMove="Canvas_MouseMove"
                            MouseLeftButtonUp="Canvas_MouseLeftButtonUp"
                            MouseRightButtonDown="Canvas_MouseRightButtonDown"
                            KeyDown="Canvas_KeyDown" Focusable="True">
                        <Canvas.ContextMenu>
                            <ContextMenu x:Name="CanvasCtx"/>
                        </Canvas.ContextMenu>
                        <Canvas.RenderTransform>
                            <TransformGroup>
                                <ScaleTransform x:Name="Zoom"/>
                                <TranslateTransform x:Name="Pan"/>
                            </TransformGroup>
                        </Canvas.RenderTransform>
                    </Canvas>
                </Grid>
            </ScrollViewer>
            <TextBlock HorizontalAlignment="Left" VerticalAlignment="Bottom" Margin="8" Opacity="0.6"
                       Text="Колесо — масштаб • СКМ/пробел+ЛКМ — панорама • ЛКМ за порт — провод • ПКМ — добавить блок • Del — удалить • Ctrl+Z/Y — отмена/повтор"/>
        </Grid>
    </Grid>
</UserControl>

<!-- Шаблон узла (в ресурсах GraphEditorView или отдельным DataTemplate в App.xaml): -->
<DataTemplate x:Key="NodeTemplate" DataType="{x:Type engine:BlockNodeView}">
    <Border Width="180" CornerRadius="8" BorderBrush="#5C4400" BorderThickness="2" Background="White">
        <StackPanel>
            <Border Background="{Binding HeaderColor}" CornerRadius="6,6,0,0" Padding="6,3">
                <TextBlock Text="{Binding Title}" FontWeight="Bold" Foreground="White"/>
            </Border>
            <ItemsControl ItemsSource="{Binding ExecInPorts}"/>   <!-- белые треугольники слева -->
            <ItemsControl ItemsSource="{Binding DataPorts}"/>      <!-- цветные круги + подписи -->
        </StackPanel>
    </Border>
</DataTemplate>
```

### `Views/Controls/GraphEditorView.xaml.cs` (ядро interactions; ~300 строк)
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BBPlusSimpleModder.BlockEngine;

namespace BBPlusSimpleModder.Views.Controls;

public partial class GraphEditorView : UserControl
{
    private Graph _graph = new();
    private BlockCatalogService _catalog = null!;
    private CodegenService _codegen = null!;
    private readonly Dictionary<BlockNode, Border> _nodeViews = new();
    private readonly List<Path> _wirePaths = new();
    private readonly Stack<string> _undo = new(), _redo = new();
    private Point _panStart; private bool _panning;
    private Point _wireStart; private PortRef? _wireFrom; private Path? _tempWire;
    private BlockNode? _dragNode; private Point _dragOffset;
    private double _zoom = 1.0;

    public GraphEditorView() => InitializeComponent();

    public void Bind(BlockCatalogService catalog, CodegenService codegen)
    {
        _catalog = catalog; _codegen = codegen;
        RefreshPalette("");
        Keyboard.Focus(NodesCanvas);
    }

    public void LoadGraph(Graph g)
    {
        _graph = g;
        NodesCanvas.Children.Clear();
        WiresCanvas.Children.Clear();
        _nodeViews.Clear(); _wirePaths.Clear();
        foreach (var n in _graph.Nodes) AddNodeView(n);
        foreach (var w in _graph.Wires) AddWireView(w);
        PushUndo();
    }

    public void SaveCurrent() { /* сериализует _graph обратно в ModComponent.Graph */ }

    // ---------- палитра ----------
    private void RefreshPalette(string filter)
    {
        var groups = _catalog.Search(filter);   // List<BlockCategory { Category, Blocks }>
        Palette.ItemsSource = groups;
    }
    private void PaletteSearch_Changed(object s, TextChangedEventArgs e) => RefreshPalette(PaletteSearch.Text);
    private void Palette_DoubleClick(object s, MouseButtonEventArgs e) => AddBlockAtCenter();
    private void Block_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
    {
        // drag&drop блока из палитры на канвас
        if ((s as FrameworkElement)?.Tag is BlockDefinition def)
            DragDrop.DoDragDrop((DependencyObject)s, def, DragDropEffects.Copy);
    }

    // ---------- добавление узлов ----------
    private void AddBlockAtCenter() { /* добавить выделенный в палитре блок в центр видимой области */ }
    private void Canvas_MouseRightButtonDown(object s, MouseButtonEventArgs e)
    {
        var pos = MouseToGraph(e.GetPosition(NodesCanvas));
        CanvasCtx.Items.Clear();
        foreach (var def in _catalog.All().Take(400))
        {
            var mi = new MenuItem { Header = $"{def.Icon} {def.Title}", Tag = (def, pos) };
            mi.Click += (_, _) => AddNode((BlockDefinition)((MenuItem)((MenuItem)_)).Tag, pos);
            CanvasCtx.Items.Add(mi);
        }
        CanvasCtx.IsOpen = true;
    }
    private void AddNode(BlockDefinition def, Point pos)
    {
        var node = new BlockNode { Id = Guid.NewGuid(), BlockId = def.Id, X = pos.X, Y = pos.Y };
        _graph.Nodes.Add(node);
        AddNodeView(node);
        PushUndo();
    }

    private void AddNodeView(BlockNode n)
    {
        var def = _catalog.Get(n.BlockId);
        var view = new Border { DataContext = BlockNodeView.From(n, def), Tag = n };
        view.SetValue(Canvas.LeftProperty, n.X);
        view.SetValue(Canvas.TopProperty, n.Y);
        view.MouseLeftButtonDown += Node_MouseLeftButtonDown;   // drag за шапку
        NodesCanvas.Children.Add(view);
        _nodeViews[n] = view;
    }

    // ---------- pan / zoom ----------
    private void Scroller_PreviewMouseWheel(object s, MouseWheelEventArgs e)
    {
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.1 : 1/1.1), 0.2, 3.0);
        Zoom.ScaleX = Zoom.ScaleY = _zoom;
        e.Handled = true;
    }
    private void Scroller_ScrollChanged(object s, ScrollChangedEventArgs e) { }
    private Point MouseToGraph(Point p) => NodesCanvas.TransformToAncestor(ZoomHost).Inverse.Transform(p);

    // ---------- drag узла ----------
    private void Node_MouseLeftButtonDown(object s, MouseButtonEventArgs e) { /* начать drag, _dragNode = Tag */ }
    private void Canvas_MouseMove(object s, MouseEventArgs e)
    {
        if (_dragNode != null && _nodeViews.TryGetValue(_dragNode, out var v))
        {
            var p = MouseToGraph(e.GetPosition(NodesCanvas));
            _dragNode.X = p.X - _dragOffset.X; _dragNode.Y = p.Y - _dragOffset.Y;
            v.SetValue(Canvas.LeftProperty, _dragNode.X);
            v.SetValue(Canvas.TopProperty, _dragNode.Y);
            RedrawWires();
        }
        else if (_wireFrom != null) { /* тянуть временный провод */ }
    }

    // ---------- провода ----------
    // ЛКМ по data-порту out → _wireFrom; отпускание по порту in → создать Wire
    private void Canvas_MouseLeftButtonUp(object s, MouseButtonEventArgs e)
    {
        if (_wireFrom != null && TryGetPort(e.GetPosition(NodesCanvas), out var target))
        {
            _graph.Wires.Add(new Wire(_wireFrom, target!));
            AddWireView(_graph.Wires[^1]);
            PushUndo();
        }
        _wireFrom = null; _tempWire = null;
        _dragNode = null;
    }
    private void AddWireView(Wire w) { /* Path: кубический bezier между центрами портов */ }
    private void RedrawWires() { /* пер перерисовке всех Path по позициям узлов */ }

    // ---------- клавиатура ----------
    private void Canvas_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) { /* удалить выделенные узлы/провода */ }
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0) Undo();
        if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) != 0) Redo();
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0) PaletteSearch.Focus();
    }
    private void PushUndo() { _undo.Push(_graph.ToJson()); if (_undo.Count > 100) _undo.TrimExcess(); _redo.Clear(); }
    private void Undo() { if (_undo.Count > 1) { _redo.Push(_undo.Pop()); LoadGraph(Graph.FromJson(_undo.Peek())); } }
    private void Redo() { if (_redo.Count > 0) { _undo.Push(_redo.Pop()); LoadGraph(Graph.FromJson(_undo.Peek())); } }

    // предпросмотр сгенерированного C# для текущего графа
    public string PreviewCode() => _codegen.GenerateClass(_graph);
}
```

---

## 7. AssetBrowserView — ассеты мода (спрайты/модели/аудио)

### `Views/Controls/AssetBrowserView.xaml`
```xml
<UserControl x:Class="BBPlusSimpleModder.Views.Controls.AssetBrowserView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DockPanel>
        <ToolBar DockPanel.Dock="Top">
            <Button Content="➕ Добавить файлы…" Click="Add_Click"/>
            <Button Content="🗑 Удалить" Click="Delete_Click"/>
            <TextBlock Text="Перетащите файлы прямо сюда" VerticalAlignment="Center" Margin="8,0" Opacity="0.7"/>
        </ToolBar>
        <ListView x:Name="Grid" AllowDrop="True" Drop="Grid_Drop" DragOver="Grid_DragOver"
                  SelectionMode="Extended" MouseDoubleClick="Grid_DoubleClick">
            <ListView.ItemsPanel>
                <ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate>
            </ListView.ItemsPanel>
            <ListView.ItemTemplate>
                <DataTemplate>
                    <StackPanel Width="110" Margin="6">
                        <Border Width="100" Height="100" BorderBrush="#C9B458" BorderThickness="2" CornerRadius="8" Background="White">
                            <Image Source="{Binding Thumbnail}" Stretch="Uniform"/>
                        </Border>
                        <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis" HorizontalAlignment="Center"/>
                        <TextBlock Text="{Binding Kind}" FontSize="11" Opacity="0.6" HorizontalAlignment="Center"/>
                    </StackPanel>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </DockPanel>
</UserControl>
```

### `Views/Controls/AssetBrowserView.xaml.cs`
```csharp
using System.Windows;
using System.Windows.Controls;
using BBPlusSimpleModder.Models;

namespace BBPlusSimpleModder.Views.Controls;

public partial class AssetBrowserView : UserControl
{
    private ModProject _project = null!;
    private readonly AssetImportService _import = App.Services.Get<AssetImportService>();

    public AssetBrowserView() => InitializeComponent();

    public void Bind(ModProject p) { _project = p; Grid.ItemsSource = _project.Assets; }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Все ассеты|*.png;*.jpg;*.tga;*.dds;*.bmp;*.obj;*.wav;*.ogg;*.json|Все файлы|*.*"
        };
        if (ofd.ShowDialog() == true) { _import.ImportFiles(ofd.FileNames, _project.Root); Grid.Items.Refresh(); }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Grid.SelectedItems.Cast<AssetEntry>().ToList())
        {
            _import.Delete(item);
            _project.Assets.Remove(item);
        }
        Grid.Items.Refresh();
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void Grid_Drop(object sender, DragEventArgs e)
    {
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        _import.ImportFiles(files, _project.Root);
        Grid.Items.Refresh();
    }

    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // превью: спрайт — во всплывающем окне; аудио — play; obj — 3D-превью (Helix) позже
        if (Grid.SelectedItem is AssetEntry a) _import.Preview(a);
    }
}
```

---

## 8. LevelEditorView — 2D-редактор уровня (.lvl)

Сетка клеток (как в PLS): палитра инструментов (коридор/класс/faculty/офис, дверь, окно,
NPC-спавн, item-спавн, постер), рисование ЛКМ, свойства клетки в Inspector, сохранение .lvl.

### `Views/Controls/LevelEditorView.xaml` (схема)
```xml
<UserControl x:Class="BBPlusSimpleModder.Views.Controls.LevelEditorView" ...>
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="200"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <StackPanel Grid.Column="0"> <!-- палитра инструментов: RadioButton'ы H/C/F/O, Door, Window, NPC, Item, Poster -->
            <RadioButton x:Name="ToolHall" Content="⬜ Коридор" IsChecked="True"/>
            <RadioButton Content="🏫 Класс"/> <RadioButton Content="🚷 Faculty"/> <RadioButton Content="💼 Офис"/>
            <Separator/> <RadioButton Content="🚪 Дверь"/> <RadioButton Content="🪟 Окно"/>
            <Separator/> <RadioButton Content="🧍 NPC"/> <RadioButton Content="🥤 Item"/> <RadioButton Content="🖼 Постер"/>
            <Button Content="💾 Сохранить .lvl" Click="SaveLvl_Click"/>
            <Button Content="📂 Открыть .lvl" Click="OpenLvl_Click"/>
        </StackPanel>
        <ScrollViewer Grid.Column="1" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
            <Canvas x:Name="LevelCanvas" Width="2000" Height="2000" Background="#F5F5F5"
                    MouseLeftButtonDown="Cell_MouseDown" MouseMove="Cell_MouseMove" MouseLeftButtonUp="Cell_MouseUp"/>
        </ScrollViewer>
    </Grid>
</UserControl>
```

### `Views/Controls/LevelEditorView.xaml.cs` (суть)
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using BBPlusSimpleModder.Models;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views.Controls;

public partial class LevelEditorView : UserControl
{
    private const int CellSize = 32;
    private ModProject _project = null!;
    private LvlFormatService _lvl = App.Services.Get<LvlFormatService>();
    private BaldiLevelModel _level = new(40, 40);
    private bool _painting;

    public LevelEditorView() => InitializeComponent();

    public void Bind(ModProject p) { _project = p; DrawAll(); }

    private string CurrentTool =>
        ToolHall.IsChecked == true ? "hall" : "class"; /* …остальные RadioButton'ы */

    private void Cell_MouseDown(object s, MouseButtonEventArgs e)
    {
        _painting = true;
        PaintAt(e.GetPosition(LevelCanvas));
        LevelCanvas.CaptureMouse();
    }
    private void Cell_MouseMove(object s, MouseEventArgs e) { if (_painting) PaintAt(e.GetPosition(LevelCanvas)); }
    private void Cell_MouseUp(object s, MouseButtonEventArgs e) { _painting = false; LevelCanvas.ReleaseMouseCapture(); }

    private void PaintAt(Point p)
    {
        int cx = (int)(p.X / CellSize), cy = (int)(p.Y / CellSize);
        _level.SetCell(cx, cy, CurrentTool);      // модель уровня
        DrawCell(cx, cy);                          // прямоугольник на канвасе
    }

    private void DrawCell(int cx, int cy)
    {
        var r = new Rectangle { Width = CellSize - 1, Height = CellSize - 1, Fill = BrushFor(_level.GetCell(cx, cy)) };
        Canvas.SetLeft(r, cx * CellSize); Canvas.SetTop(r, cy * CellSize);
        LevelCanvas.Children.Add(r);
    }
    private void DrawAll() { LevelCanvas.Children.Clear(); for (int x = 0; x < _level.W; x++) for (int y = 0; y < _level.H; y++) DrawCell(x, y); }

    private void SaveLvl_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "Уровень BB+ (*.lvl)|*.lvl", FileName = "level.lvl" };
        if (sfd.ShowDialog() == true) _lvl.Write(sfd.FileName, _level);   // формат v10, BaldiLevel.Write
    }

    private void OpenLvl_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Уровень BB+ (*.lvl)|*.lvl" };
        if (ofd.ShowDialog() == true) { _level = _lvl.Read(ofd.FileName); DrawAll(); }  // BaldiLevel.Read
    }
}
```

---

## 9. BuildWindow — модальное окно сборки («Собрать»)

Показывает шаги: референсы → генерация C# → компиляция (Roslyn) → pdb/xml → копирование
ресурсов в `StreamingAssets/Modded/{GUID}/` → копирование плагина в `BepInEx/plugins/`
(flat или папкой — из настроек). Лог + итог.

### `Views/BuildWindow.xaml`
```xml
<Window x:Class="BBPlusSimpleModder.Views.BuildWindow" Title="🔨 Сборка мода"
        Width="640" Height="460" WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <TextBlock Style="{StaticResource HeaderLabel}" x:Name="TitleText" Text="Сборка…"/>
        <ListBox x:Name="Steps" Grid.Row="1" Margin="0,6">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="{Binding StatusIcon}" Margin="0,0,8,0"/>
                        <TextBlock Text="{Binding Text}"/>
                    </StackPanel>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
        <TextBox x:Name="LogBox" Grid.Row="2" IsReadOnly="True" FontFamily="Consolas" FontSize="12"
                 VerticalScrollBarVisibility="Auto" TextWrapping="Wrap" Background="#1E1E1E" Foreground="#D4D4D4"/>
        <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button x:Name="OpenFolderBtn" Content="📂 Открыть папку мода" Click="OpenFolder_Click" IsEnabled="False"/>
            <Button x:Name="PlayBtn" Content="▶ Играть" Click="Play_Click" IsEnabled="False"/>
            <Button x:Name="CloseBtn" Content="Закрыть" Click="Close_Click" IsEnabled="False"/>
        </StackPanel>
    </Grid>
</Window>
```

### `Views/BuildWindow.xaml.cs`
```csharp
using System.Windows;
using BBPlusSimpleModder.Models;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views;

public partial class BuildWindow : Window
{
    private readonly ModProject _project;
    private readonly string _projectPath;
    private readonly BuildService _build = App.Services.Get<BuildService>();

    public BuildWindow(ModProject project, string projectPath)
    {
        InitializeComponent();
        _project = project; _projectPath = projectPath;
        Loaded += async (_, _) => await RunBuild();
    }

    private async Task RunBuild()
    {
        var steps = new List<BuildStep> {
            new("Референсы игры и API"), new("Генерация C# из кирпичиков"),
            new("Компиляция DLL (Roslyn)"), new("PDB + XML"),
            new("Копирование ресурсов → StreamingAssets/Modded/{GUID}/"),
            new("Копирование плагина → BepInEx/plugins/"),
        };
        Steps.ItemsSource = steps;
        try
        {
            var result = await _build.BuildAsync(_project, _projectPath, steps,
                msg => Dispatcher.Invoke(() => LogBox.AppendText(msg + "\n")));
            TitleText.Text = "✅ Готово!";
            OpenFolderBtn.IsEnabled = PlayBtn.IsEnabled = CloseBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            TitleText.Text = "❌ Ошибка сборки";
            LogBox.AppendText(ex.ToString());
            CloseBtn.IsEnabled = true;
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
        => _build.OpenModFolder(_project);
    private void Play_Click(object sender, RoutedEventArgs e)
        => App.Services.Get<GameEnvironmentService>().LaunchGame(_project.GamePath);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
```

---

## 10. SettingsWindow — настройки проекта/приложения

Поля: папка игры, куда класть плагин (flat `plugins/{GUID}.dll` vs папка `plugins/{GUID}/`),
чекбоксы автоустановки (BepInEx, DevAPI, PLS/Loader), URL'ы скачивания (`downloads.json`),
язык UI, размер шрифта. Сохранение в `settings.json` рядом с приложением.

### `Views/SettingsWindow.xaml` (схема полей)
```xml
<Window x:Class="BBPlusSimpleModder.Views.SettingsWindow" Title="⚙ Настройки" SizeToContent="WidthAndHeight">
    <StackPanel Margin="14" Width="520">
        <TextBlock Style="{StaticResource HeaderLabel}" Text="⚙ Настройки"/>
        <TextBlock Text="Папка игры BB+:"/><TextBox x:Name="GamePathBox"/>
        <GroupBox Header="Куда класть собранный плагин">
            <StackPanel>
                <RadioButton x:Name="PluginFlat" Content="Плоско: BepInEx/plugins/{GUID}.dll (+ .pdb/.xml рядом)" IsChecked="True"/>
                <RadioButton x:Name="PluginFolder" Content="Папкой: BepInEx/plugins/{GUID}/ (dll+pdb+xml внутри)"/>
            </StackPanel>
        </GroupBox>
        <GroupBox Header="Автоустановка при отсутствии (URL в downloads.json)">
            <StackPanel>
                <CheckBox x:Name="AutoBepInEx" Content="BepInEx 5.x (win x64)" IsChecked="True"/>
                <CheckBox x:Name="AutoDevApi" Content="MTM101BaldAPI" IsChecked="True"/>
                <CheckBox x:Name="AutoPls" Content="Plus Level Studio + Loader (опционально)"/>
            </StackPanel>
        </GroupBox>
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
            <Button Content="Отмена" Click="Cancel_Click"/>
            <Button Content="Сохранить ✔" Click="Save_Click" IsDefault="True"/>
        </StackPanel>
    </StackPanel>
</Window>
```

### `Views/SettingsWindow.xaml.cs`
```csharp
using System.Windows;
using BBPlusSimpleModder.Models;

namespace BBPlusSimpleModder.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsModel _settings;
    public SettingsWindow(SettingsModel settings)
    {
        InitializeComponent();
        _settings = settings;
        GamePathBox.Text = settings.GamePath;
        PluginFlat.IsChecked = !settings.PluginInFolder;
        PluginFolder.IsChecked = settings.PluginInFolder;
        AutoBepInEx.IsChecked = settings.AutoInstallBepInEx;
        AutoDevApi.IsChecked = settings.AutoInstallDevApi;
        AutoPls.IsChecked = settings.AutoInstallPls;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.GamePath = GamePathBox.Text.Trim();
        _settings.PluginInFolder = PluginFolder.IsChecked == true;
        _settings.AutoInstallBepInEx = AutoBepInEx.IsChecked == true;
        _settings.AutoInstallDevApi = AutoDevApi.IsChecked == true;
        _settings.AutoInstallPls = AutoPls.IsChecked == true;
        DialogResult = true; Close();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
```

---

## 11. ModBrowserWindow — импорт чужих модов (.dll) и уровней (.lvl)

Верх: выбор файла DLL или .lvl. Для DLL: Mono.Cecil-скан → список найденных
BepInEx-плагинов и Harmony-патчей → чекбоксы → «Импортировать»: декомпиляция
(ICSharpCode.Decompiler), pattern-match в кирпичики, остальное — Raw C# блоки.
Для .lvl: открыть в LevelEditorView.

### `Views/ModBrowserWindow.xaml` (схема)
```xml
<Window x:Class="BBPlusSimpleModder.Views.ModBrowserWindow" Title="📥 Импорт мода / уровня" Width="720" Height="520">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <RowDefinition Height="*"/> <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <StackPanel Orientation="Horizontal">
            <Button Content="📦 Открыть DLL мода…" Click="OpenDll_Click"/>
            <Button Content="🗺 Открыть .lvl…" Click="OpenLvl_Click"/>
        </StackPanel>
        <ListBox x:Name="FoundList" Grid.Row="1" Margin="0,8">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <CheckBox IsChecked="{Binding Selected}" VerticalAlignment="Center" Margin="0,0,8,0"/>
                        <TextBlock Text="{Binding Description}"/>
                    </StackPanel>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button x:Name="ImportBtn" Content="⬇ Импортировать выбранное" Click="Import_Click" IsEnabled="False"/>
            <Button Content="Закрыть" Click="Close_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

### `Views/ModBrowserWindow.xaml.cs`
```csharp
using System.Windows;
using BBPlusSimpleModder.Models;
using BBPlusSimpleModder.Services;

namespace BBPlusSimpleModder.Views;

public partial class ModBrowserWindow : Window
{
    private readonly ModProject _project;
    private readonly ModImportService _import = App.Services.Get<ModImportService>();

    public ModBrowserWindow(ModProject project) { InitializeComponent(); _project = project; }

    private void OpenDll_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Плагин BepInEx (*.dll)|*.dll" };
        if (ofd.ShowDialog() != true) return;
        FoundList.ItemsSource = _import.ScanDll(ofd.FileName);   // Cecil: плагины, [HarmonyPatch], классы NPC/Item…
        ImportBtn.IsEnabled = true;
    }

    private void OpenLvl_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Уровень (*.lvl)|*.lvl" };
        if (ofd.ShowDialog() == true)
        {
            _import.ImportLvlIntoProject(ofd.FileName, _project);   // .lvl → LevelEditorView + ассеты
            Close();
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var items = FoundList.ItemsSource.Cast<ImportItem>().Where(i => i.Selected).ToList();
        ImportBtn.IsEnabled = false;
        await _import.ImportSelectedAsync(items, _project);          // Decompiler → графы (узнаваемые блоки) + Raw C#
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
```

---

## 12. LogView — маленький контрол хвоста лога

```xml
<UserControl x:Class="BBPlusSimpleModder.Views.Controls.LogView" ...>
    <TextBox x:Name="Box" IsReadOnly="True" FontFamily="Consolas" FontSize="12"
             VerticalScrollBarVisibility="Auto" Background="#1E1E1E" Foreground="#D4D4D4" TextWrapping="Wrap"/>
</UserControl>
```
```csharp
public partial class LogView : UserControl
{
    public LogView() => InitializeComponent();
    public void Append(string text)
    {
        Box.AppendText(text);
        Box.ScrollToEnd();
        if (Box.LineCount > 2000) Box.Clear();   // чтобы не разрастался
    }
}
```

---

## Примечания по реализации

- Все окна — обычные WPF `Window`/`UserControl`; навигация — как в примерах выше
  (`new MainWindow(path).Show(); Close();`), модальные — через `ShowDialog()`.
- В Blend for VS редактируется только XAML; вся логика — в `.xaml.cs` + сервисах,
  поэтому Blend не ломает code-behind.
- Тема `MinimalComicTheme.xaml` даёт Comic Sans и «мягкие» кнопки глобально —
  отдельные окна могут переопределять локально.
- GraphEditorView — единственный сложный контрол; его interacciones (pan/zoom/drag/wires/undo)
  расписаны в §6, остальное — рутинная WPF-обвязка сервисов.
