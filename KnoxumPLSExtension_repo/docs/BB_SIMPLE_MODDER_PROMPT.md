# ГИГАНТСКИЙ ПРОМПТ: «BB+ Simple Modder» — генератор модов для Baldi's Basics Plus

> Куда вставлять: любой AI-ассистент/агент, умеющий писать код и работать с файлами
> (Cursor / Arena Agent Mode / и т.п.). К промпту можно приложить файлы-приложения:
> `BLOCK_MOD_STUDIO_DESIGN.md` (полный дизайн) и `UI_XAML_WINDOWS.md` (все окна с XAML/C#).
> Промпт самодостаточный: все ключевые факты продублированы ниже.

---

Ты — senior C#-разработчик. Твоя задача: **с нуля создать настольное WPF-приложение
«BB+ Simple Modder»** — визуальный ("кирпичиками") редактор модов для игры
**Baldi's Basics Plus (BB+)**. Приложение собирает из блоков настоящий BepInEx-плагин
(DLL + портативный PDB + XML) и раскладывает ресурсы мода по папкам игры.

Работай итеративно, фаза за фазой (план в конце). После каждой фазы проект должен
**собираться** (`dotnet build` без ошибок). Пиши чистый, сопровождаемый код.

---

## 1. Факты о игре и мод-экосистеме (проверь себя по ним, не выдумывай)

- BB+ — Unity-игра (Mono, Windows x64). Папка игры содержит `BALDI_Data/`,
  исполняемый `*.exe` рядом с `BALDI_Data/`, BepInEx живёт в корне игры:
  `winhttp.dll`, `doorstop_config.ini`, `BepInEx/core/`, плагины в `BepInEx/plugins/`,
  лог `BepInEx/LogOutput.log`.
- Моддинг-стек: **BepInEx 5.x + Harmony (0Harmony.dll) + MTM101BaldAPI +
  PixelInternalAPI + Plus Level Loader (+ опционально Plus Level Studio)**.
- GUID'ы плагинов (нужны для BepInDependency генерируемых модов):
  - `mtm101.rulerp.bbplus.baldidevapi` — MTM101BaldAPI (Dev API)
  - `mtm101.rulerp.baldiplus.levelstudioloader` — Plus Level Loader
  - `mtm101.rulerp.baldiplus.levelstudio` — Plus Level Studio
  - `pixelguy.pixelmodding.baldiplus.pixelinternalapi` — PixelInternalAPI
  - `pixelguy.pixelmodding.baldiplus.moddedmodesapi` — ModdedModesAPI
- Автоустановка BepInEx: релиз **v5.4.23.6**, ассет
  `https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.6/BepInEx_win_x64_5.4.23.6.zip`
  (распаковать в корень игры). MTM101BaldAPI / PLS / Loader — скачивать по URL из
  настраиваемого `downloads.json` (пользователь может подменить на Thunderstore/GameBanana).
- **Путь ресурсов мода в игре** (родной для API):
  `BALDI_Data/StreamingAssets/Modded/{GUID_плагина}/` —
  в декомпиляте API это `AssetLoader.GetModPath(plug) =
  Path.Combine(Application.streamingAssetsPath, "Modded", plug.Info.Metadata.GUID)`,
  рядом `AssetLoader.TextureFromMod/AudioClipFromMod/...` (декомпилят, строка ~167509).
- Ключевые API-классы (из декомпилята `ultimate_dev_mods_decompile.cs`, секция MTM101BaldAPI, строка 162782+):
  `NPCBuilder<T> where T : NPC` (`.Build()`), `ItemBuilder`, `EntityBuilder`,
  `RandomEventBuilder`, `StickerBuilder`, `OBJLoader`/`OBJObjectBuilder` (импорт 3D OBJ),
  `TGALoader`/`DDSLoader`/`BMPLoader`, `SpriteSheetLoader`, `LoadingEvents.RegisterOnAssetsLoaded(PluginInfo, Action, LoadingEventOrder)`,
  `ObjectCreators.CreatePosterObject/CreateSoundObject/CreateWindowObject/CreateDoorDataObject`,
  `NPCMetaStorage`/`NPCMetadata`, `ItemMetaStorage`/`ItemMetaData`, `RandomEventMetaStorage`,
  `CustomLevelObject`, `LevelTypeMetaStorage`, `ValueModifier`.
- Ключевые классы игры (секация BB+ 0.14.5, строка 47197+):
  `NPC : MonoBehaviour, IEntityTrigger` с виртуалками `Initialize, VirtualUpdate, Hear(GameObject, Vector3, int),
  PlayerInSight/PlayerSighted/PlayerLost, VirtualOnTriggerEnter/Stay/Exit, DoorHit, WindowHit,
  TargetPosition(Vector3), SetGuilt/ClearGuilt, SentToDetention, Despawn`, поле `Navigator`, `Character`;
  enum `Character { Null, Baldi, Playtime, Crafters, Bully, Principal, Prize, Sweep, Cumulo, Chalkles, Beans, Pomp, LookAt, DrReflex }`;
  `Item` с `virtual bool Use(PlayerManager)`, `virtual void PostUse(PlayerManager)`;
  `RoomFunction.Build(LevelBuilder, Random)`; `EnvironmentController` (CreateCell/SwapCell/MergeCell/BuildNavMesh);
  события `RandomEvent` и `BalderEvent/FloodEvent/FogEvent/GravityEvent/PartyEvent/...`;
  `PlayerManager`, `ItemManager`, `AudioManager`/`PropagatedAudioManager`, `HudManager`, `SubtitleManager`, `LocalizationManager`.
- **Формат уровней Plus Level Format v10** (секация PlusStudioLevelFormat, строка 155646+):
  `BaldiLevel.Write(BinaryWriter)/Read(BinaryReader)`, поля: `cells[,]`, `rooms (RoomInfo)`,
  `lights`, `tileObjects`, `doors (DoorInfo)`, `windows`, `exits`, `structures (StructureInfo)`,
  `randomStructures`, `npcs (NPCInfo{npc, position})`, `posters`, `premadeRoomPlacements`,
  `spawnPoint`, `spawnDirection`, `timeLimit`, `levelTitle`, `version = 10`.
- Референс «как пишется мод руками»: файл `file.cs` в репозитории (BepInEx-плагин
  `KnoxumsChaosMode` с `[HarmonyPatch(typeof(Baldi), "TakeApple")] static bool Prefix(Baldi __instance)`).
- Декомпилят всего стека лежит в репозитории: `ultimate_dev_mods_decompile.cs` (5.6 МБ),
  секции по строкам: BBCR @1, **BB+ 0.14.5 @47197**, **Plus Level Studio 1.8.3.3 @122761**,
  **Plus Level Loader + формат .lvl @155646**, PixelInternalAPI @159503, **MTM101BaldAPI @162782**,
  ModdedModesAPI @181354. Используй его как справочник сигнатур (но НЕ копируй код игры в приложение).

---

## 2. Пользовательский workflow (это ТЗ — приложение должно водить пользователя по этим шагам)

1. При запуске — окно выбора **папки игры BB+**. Автопроверка: `BALDI_Data/`, BepInEx,
   MTM101BaldAPI, опционально PLS/Loader. Если BepInEx/API нет — предлагаем **одной кнопкой
   скачать и установить** (BepInEx win x64 zip + распаковать; API в `BepInEx/plugins/`).
2. **Новый мод**: имя + GUID (дефолт `com.knoxum.baldiplus.simplemodder.<имя-mода>`),
   версия/автор/описание. Создаётся проект `*.bbmod`.
3. **Главное окно**: слева дерево компонентов мода (как иерархия в Unity), справа Inspector,
   в центре вкладки (Логика кирпичиками / Ассеты / Уровень / Сгенерированный код / Лог).
   Узлы дерева: Этажи (внутри — компоненты генерации), Объекты (шкафчики, столы, двери…),
   Персонажи (значения + логика кирпичиками), Предметы, События, Спрайты, 3D-модели, Аудио,
   UI сцен (меню/HUD/пауза — элементы как в Unity: спрайт, текст, кнопка), Патчи (Harmony), Настройки.
   Везде: Add / Rename / Duplicate / Delete, drag&drop ассетов из проводника.
4. Кнопка **«Собрать»**: графы → C# (Roslyn) → `{GUID}.dll` + портативный `.pdb` + `.xml`;
   ресурсы → `BALDI_Data/StreamingAssets/Modded/{GUID}/{sprites,models,audio,data}/` + `mod.json`;
   плагин → по настройке: flat `BepInEx/plugins/{GUID}.dll(.pdb/.xml)` ИЛИ папка `BepInEx/plugins/{GUID}/`.
5. Кнопка **«Играть»**: запуск exe игры (рядом с `BALDI_Data/`, либо через Steam), хвост
   `BepInEx/LogOutput.log` показываем в окне утилиты.
6. Готово — мод в игре.

---

## 3. Технический стек и ограничения

- Приложение: **.NET 8, WPF (`net8.0-windows`)**, XAML-first — проект должен открываться
  в **Blend for Visual Studio**. MVVM (CommunityToolkit.Mvvm). JSON — System.Text.Json.
- UI-стиль: **минимализм + шрифт Comic Sans MS**, крупные контролы, drag&drop, подсказки.
  Тема — `Themes/MinimalComicTheme.xaml` (FontFamily Comic Sans MS по умолчанию для всех контролов).
- Отдельная тестируемая библиотека-ядро (графы, кодоген, формат .lvl, метаданные референсов).
- Генерируемые моды: таргет `net472`/`netstandard2.0`, референсы берутся **из папки игры
  пользователя** (`BALDI_Data/Managed/Assembly-CSharp.dll`, `UnityEngine.*.dll`,
  `BepInEx/core/BepInEx.dll`, `0Harmony.dll`, `BepInEx/plugins/MTM101BaldAPI.dll` и т.д.),
  компиляция — **Microsoft.CodeAnalysis.CSharp (Roslyn) in-memory**, эмит с
  `DebugType.Portable` (пишем .pdb) и `GenerateDocumentationFile` (пишем .xml).
- НЕ включай в приложение никакие DLL/код игры — только используем их как reference при сборке.
- assumed internet на машине пользователя для автоустановки; в своём коде не хардкодь
  токены/пароли.

---

## 4. Структура solution

```
BBPlusSimpleModder.sln
├─ src/BBPlusSimpleModder/                 WPF-приложение (net8.0-windows)
│  ├─ App.xaml(.cs)                        StartupUri=Views/StartupWindow.xaml, merged theme
│  ├─ Themes/MinimalComicTheme.xaml        Comic Sans, цвета, стили кнопок/дерева/канваса
│  ├─ Models/            ModProject, ModComponent (узел дерева), Graph, BlockNode, Port, Wire,
│  │                     AssetEntry, LvlModels…, SettingsModel
│  ├─ ViewModels/      StartupVM, NewModVM, MainVM, SettingsVM, BuildVM, ModBrowserVM,
│  │                     ComponentTreeVM, InspectorVM, GraphEditorVM, AssetBrowserVM, LevelEditorVM
│  ├─ Views/           окна: StartupWindow, NewModWindow, MainWindow, SettingsWindow,
│  │                     BuildWindow, ModBrowserWindow
│  └─ Views/Controls/  ComponentTreeView, InspectorView, GraphEditorView (канвас кирпичиков),
│                        AssetBrowserView, LevelEditorView, LogView
│  ├─ Services/        GameEnvironmentService (детект/установка BepInEx+API, запуск игры, log tail),
│  │                     ProjectService (создать/открыть/сохранить .bbmod),
│  │                     ReferenceResolver (поиск DLL игры/API по папке игры),
│  │                     BlockCatalogService (каталог блоков: ручные + авто из Mono.Cecil),
│  │                     CodegenService (граф→C# через Roslyn SyntaxFactory),
│  │                     BuildService (emit dll/pdb/xml, раскладка по папкам игры),
│  │                     AssetImportService (png/obj/wav/ogg → AssetEntry), LvlFormatService (.lvl v10 read/write),
│  │                     ModImportService (чужой DLL → Cecil+ICSharpCode.Decompiler → графы/raw-блоки)
│  └─ BlockEngine/     BlockDefinition (порты + делегат генерации C#), GraphValidator
├─ src/BBPlusSimpleModder.Core/            netstandard2.0: BlockEngine, Codegen, LvlFormat, CecilMetadata
└─ tests/BBPlusSimpleModder.Tests/        юнит-тесты ядра (граф→C#, .lvl roundtrip)
```

Подробная раскладка окон с полным XAML и code-behind — в приложении `UI_XAML_WINDOWS.md`.
Полный дизайн — `BLOCK_MOD_STUDIO_DESIGN.md`. Следуй им.

---

## 5. Спецификация ключевых модулей (сокращённо; детали в приложениях)

### 5.1. Блоковый движок (сердце редактора)
- Graph = узлы + соединения + переменные; сериализация в JSON (human-readable).
- Узел: exec-порты (in/out) и data-порты (типы: int, float, bool, string, Vector3, Color,
  GameObject, NPC, Entity, PlayerManager, Item, RoomController, Cell, EnvironmentController,
  SoundObject, Texture2D, AudioClip, enum…). Типы портов контролируются при соединении.
- Типы графов (хуки): NPC (override виртуалок NPC), Item (Use/PostUse), RoomFunction (Build),
  RandomEvent, HarmonyPatch (prefix/postfix на метод из дерева сборок), LoadingEvent, «функция».
- Встроенные блоки: Start, Branch, Switch, For/While/ForEach, Wait (→ корутина), Return,
  литералы, переменные, math, Random.Range, Try/Catch, Raw C#.
- Каталог = ручные «кривые» блоки + **авто-блоки из метаданных сборок** (Mono.Cecil,
  read-only): каждый публичный метод/свойство → блок «Вызвать/GET/SET».
- Валидатор: недостижимый код, циклы в Update, raycast каждый кадр — предупреждения.
- Редактор GraphEditorView: канвас с pan/zoom, drag узлов, рисование проводов мышью
  (временный bezier), палитра с поиском слева, контекстное меню (добавить блок сюда),
  undo/redo, double-click → свойства узла в Inspector.

### 5.2. Кодогенерация (граф → C# → DLL+pdb+xml)
- Каждый граф → partial-класс. NPC-граф → `class <Имя>NPC : NPC { override … }`,
  регистрация `new NPCBuilder<T>(Info).Build()` + метаданные; Item → `ItemBuilder`;
  Event → `RandomEventBuilder`; патч → `[HarmonyPatch(typeof(X),"M")] static … Prefix(…)`
  со спец-параметрами `__instance/__result/аргументы` как data-порты.
- Plugin-класс: `[BepInPlugin("{GUID}","{Имя}","{Версия}")]` +
  `[BepInDependency("mtm101.rulerp.bbplus.baldidevapi", HardDependency)]` и остальные GUID из §1.
- Roslyn: `CSharpCompilation` с референсами из папки игры + `Emit` → stream dll,
  `EmitDebugInformation` portable pdb, documentation file → .xml. Ошибки компиляции показать в BuildWindow.

### 5.3. Раскладка при «Собрать»
- `<Игра>/BALDI_Data/StreamingAssets/Modded/{GUID}/sprites|models|audio|data/*.json` + `mod.json`.
- `<Игра>/BepInEx/plugins/{GUID}.dll` (+`.pdb/.xml`) — flat; или `BepInEx/plugins/{GUID}/…` —
  если в настройках выбрана папка.

### 5.4. Интеграции
- .lvl: reader/writer формата v10 по `PlusStudioLevelFormat` (BaldiLevel и Info-классы).
- PLS: EditorCustomContent-пакет, чтобы кастомные NPC/item'ы были в тулбоксах редактора PLS.
- Импорт чужих модов: Mono.Cecil → поиск BepInEx/Harmony → ICSharpCode.Decompiler →
  pattern-match в блоки, остальное — Raw C# блоки.

---

## 6. Соглашения о коде

- Язык UI — **русский** (тексты в XAML/C# — RU), комментарии — RU, имена классов/методов — англ.
- File-scoped namespaces, nullable enable, async/await для IO/сети, cancellation где уместно.
- Каждый сервис — интерфейс + реализация ( DI-lite через App.xaml.cs singleton-реестр, без внешнего DI-контейнера на старте).
- Логирование — простой `Logger` (в окно + файл), уровни Info/Warn/Error.
- Без телеметрии. Без хардкода путей — всё через Settings.

---

## 7. Поэтапный план (делай строго по порядку, после каждой фазы — сборка)

1. **Фаза 0 — скелет**: solution по §4, App.xaml + тема Comic Sans, StartupWindow
   (выбор папки игры, детект BepInEx/API, кнопки установки, recent projects),
   NewModWindow, MainWindow (дерево/инспектор/вкладки-заглушки), Settings, `project.bbmod.json`.
2. **Фаза 1 — блоковое ядро + кодоген + «Hello Mod»**: BlockEngine (модель/валидация/JSON),
   ~20 базовых блоков, GraphEditorView (канвас, провода, палитра), CodegenService (Roslyn),
   BuildService (emit dll/pdb/xml + раскладка §5.3), BuildWindow, кнопка «Играть».
   Веха: мод «Hello» (плагин пишет в LogOutput) собирается и кладётся в игру.
3. **Фаза 2 — сущности**: NPC (все хуки, NPCBuilder<T>), Item (Use/PostUse), RandomEvent,
   метаданные/локализация; дерево компонентов с Add/Rename/Duplicate/Delete; Inspector.
4. **Фаза 3 — ассеты и уровни**: AssetImportService (png/листы/obj/wav/ogg), AssetBrowserView,
   LevelEditorView + LvlFormatService (.lvl v10), привязка уровней к этажам.
5. **Фаза 4 — Harmony-патчи из кирпичиков + импорт чужих модов**: BlockCatalogService
   (авто-блоки из Cecil), ModBrowserWindow (DLL/.lvl → проект), Raw C# блок.
6. **Фаза 5 — полировка**: поиск по каталогу, шаблоны проектов, подсказки, undo/redo везде,
   юнит-тесты ядра, README.

---

## 8. Definition of done для фазы 1

- Приложение собирается, запускается, открывается в Blend for VS.
- Можно: выбрать папку BB+ (или автоустановить BepInEx+API) → создать мод (имя+GUID) →
  в дереве добавить персонажа → открыть его логику → поставить 3–5 кирпичиков →
  «Собрать» → в папке игры появились `StreamingAssets/Modded/{GUID}/…` и
  `BepInEx/plugins/{GUID}.dll/.pdb/.xml` → «Играть» запускает игру, в LogOutput видно лог мода.

## 9. Чего НЕ делать

- Не копируй код/ресурсы игры в приложение; не распространяй Assembly-CSharp.dll.
- Не делай транспайлеры Harmony из блоков (только prefix/postfix + Raw C#).
- Не усложняй: никаких плагинов-архитектуры редактора на старте, никакого Lua-бэкенда.
- Не меняй workflow пользователя (§2) «для удобства» — он утверждён.
