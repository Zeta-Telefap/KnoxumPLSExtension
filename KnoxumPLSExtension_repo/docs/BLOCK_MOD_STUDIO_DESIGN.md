# BB+ Block Mod Studio — полный дизайн утилиты (v0, обсуждение, кода пока нет)

Дата: 2026-11-02. Статус: концепт/архитектура, без кода.

Цель: настольная утилита на C#, которая позволяет собирать моды для Baldi's Basics Plus
**кирпичиками** (визуальные графы), а на выходе получать настоящий BepInEx-плагин:
`MyMod.dll` + портативный `.pdb` + `.xml` (документация), готовый к установке в игру.

---

## 0. Что уже лежит в репо и на что опираться

Главный референс — `ultimate_dev_mods_decompile.cs` (5.6 МБ). Это **склейка декомпилятов
нескольких сборок**, каждая помечена заголовком-comment со смещением строки:

| Строка | Сборка | Что внутри |
|---|---|---|
| 1 | `// BBCR 1.1a` | Baldi's Basics Classic Remastered (тоже есть NPC/Item/Enemy...) |
| 47197 | `// BB+ 0.14.5` | сама игра (Assembly-CSharp): ~671 публичный класс — NPC, Item, Entity, EnvironmentController, все Event'ы, RoomController/Cell, генераторы уровней, менеджеры |
| 122761 | `// MTM101 Plus Level Studio 1.8.3.3` | редактор-уровней: `PlusLevelStudio.Editor.*` (EditorController, инструменты BrushTool/DoorTool/ItemTool/NPCTool/PosterTool/ElevatorTool/LightTool...), `EditorCustomContent( Package)`, per-NPC property-страницы (`NPCProperties`, `PrincipalProperties`...), **Lua на MoonSharp** (`PlusLevelStudio.Lua`, `LuaHelpers`, `EditorLuaGameProxy`) |
| 155646 | `// Plus Level Loader + Plus Level Format` | `PlusStudioLevelFormat` — **бинарный формат .lvl v10**: `BaldiLevel` (cells, rooms, lights, tileObjects, doors, windows, exits, structures, randomStructures, npcs, posters, premadeRoomPlacements, spawnPoint, timeLimit...), `NPCInfo{npc, position}`, `ItemInfo{item, position}`, `RoomInfo`, `Cell`, `DoorInfo`, `StructureInfo`... и `PlusStudioLevelLoader` (`LevelImporter`, патчи загрузки) |
| 159503 | `// PixelInternalAPI` | `pixelguy.pixelmodding...`: TextureExtensions, ResourceManager, LayerStorage, PrivateCallExtensions, патчи дверей/лифта/принципала... |
| 162782 | `// MTM101BaldAPI` | главный API мода: `NPCBuilder<T>`, `ItemBuilder`, `EntityBuilder`, `RandomEventBuilder`, `StickerBuilder`, `OBJLoader`/`OBJObjectBuilder` (**готовый импорт 3D-моделей OBJ!**), `TGALoader`/`DDSLoader`/`BMPLoader`, `SpriteSheetLoader`, `LoadingEvents`, `ObjectCreators` (`CreatePosterObject`, `CreateSoundObject`, `CreateWindowObject`, `CreateDoorDataObject`...), `CustomLevelObject`, `MetaStorage`/`Metadata` (`NPCMetaStorage`/`NPCMetadata`, `ItemMetaStorage`...), `ValueModifier` (статы игрока), `GeneratorManagement` |
| 181354 | `// ModdedModesAPI` | доп. API для кастомных режимов игры |

Реальные GUID'ы BepInEx-плагинов (из атрибутов в декомпиляте) — понадобятся для
автогенерации зависимостей в манифесте нашего сгенерированного мода:

```
mtm101.rulerp.bbplus.baldidevapi          "Baldi's Basics Plus Dev API" 11.1.0.2  (MTM101BaldAPI)
mtm101.rulerp.baldiplus.levelstudioloader "Plus Level Loader"           1.12.2.0
mtm101.rulerp.baldiplus.levelstudio      "Plus Level Studio"           1.8.3.3
pixelguy.pixelmodding.baldiplus.pixelinternalapi "Pixel's Internal API" 1.2.7.1
pixelguy.pixelmodding.baldiplus.moddedmodesapi   "ModdedModesAPI"     1.2.0
```

Другие полезные файлы репо:
- `file.cs` — наш WIP-мод `KnoxumsChaosMode`: образцовый паттерн BepInEx+[[HarmonyPatch]] +
  работа с `NPC.Character` — как вручную пишется то, что мы хотим генерировать.
- `HighWallsGenerator.*.cs`, `KnoxumRampSelectionUI.cs`, `page8/page9.txt` — WIP по
  высоким стенам/рампам в редакторе PLS (полезны для понимания, как устроены
  `Cell`/`Tile` в `PlusLevelStudio.Editor`).
- `pdf_context_extracted.txt` — выжимка из PDF «куча контекста для высоких стен»
  (что такое BB+ и Plus Level Studio человеческим языком).

---

## 1. Концепция: два слоя

1. **Block Mod Studio** (настольное приложение, .NET) — редактор проектов:
   кирпичики логики, ассеты, уровни, настройки; кнопка Build.
2. **Сгенерированный BepInEx-плагин** (DLL+pdb+xml) — то, что уходит в
   `BepInEx/plugins/` игры и работает через Harmony + MTM101BaldAPI + Plus Level Loader.

Плюс «обратное» направление: открыть **чужой** мод (DLL) или **чужой** уровень (.lvl)
и посмотреть его в кирпичиках/редакторе (см. §8).

Имя: **BB+ Simple Modder**. GUID генерируемых плагинов: `com.knoxum.baldiplus.simplemodder.<имяМода>`.
Проектный файл: `*.bbmod`. Приложение — WPF, собирается/оформляется в **Blend for Visual Studio**.

---

## 2. Пользовательский сценарий (workflow) — принято 2026-11-02

End-to-end, как видит пользователь (приложение — WPF + Blend for Visual Studio):

1. **Запуск приложения** → окно выбора папки игры BB+.
   - Автопроверка окружения: есть ли `BALDI_Data/`, BepInEx (`winhttp.dll`,
     `doorstop_config.ini`, `BepInEx/core/`), MTM101BaldAPI в `BepInEx/plugins/`,
     опционально Plus Level Studio/Loader.
   - Если игра «голая» (без BepInEx) — утилита **сама накатывает BepInEx 5.x win x64**
     (напр. v5.4.23.6, ассет `BepInEx_win_x64_5.4.23.6.zip` с github.com/BepInEx/BepInEx/releases)
     и **MTM101BaldAPI** в `BepInEx/plugins/`; **Plus Level Studio / Loader — опционально**
     (чекбокс). URL релизов лежат в настраиваемом `downloads.json` утилиты.
2. **Новый мод**: вводишь имя и GUID (дефолт `com.knoxum.baldiplus.simplemodder.<имя>`,
   валидация reverse-DNS). Создаётся проект `*.bbmod`.
3. **Главное окно** — слева дерево компонентов мода (как иерархия сцен в Unity),
   справа Inspector свойств выбранного узла, по центру/вкладками — редактор кирпичиков
   и превью ассета. Узлы дерева:
   - **Этажи (Floors)** — сколько угодно; в каждом этаже — компоненты генерации
     (комнаты, коридоры, веса структур, спавны...), привязка `.lvl` или генератор;
   - **Объекты** — шкафчики, столы, двери, постеры и т.д. (vanilla как база + свои
     structure/tile-объекты); у каждого — свойства и опциональная логика кирпичиками;
   - **Персонажи** — vanilla (клонировать/править) + свои; у каждого: **значения**
     (скорость, слух, цвет, здоровье…) и **логика кирпичиками** по хукам
     (`VirtualUpdate`, `Hear`, `PlayerSighted`, `OnTrigger`…);
   - **Предметы** — то же: значения + логика `Use`/`PostUse`;
   - **События**, **Спрайты/Текстуры**, **3D-модели (OBJ)**, **Аудио**;
   - **UI сцен** (главное меню, HUD во время игры, пауза, экран победы/поражения…) —
     добавлять/двигать/удалять элементы «как в Unity»: спрайт, текст, кнопка;
   - **Патчи (Harmony)** кирпичиками, **Настройки** мода.
   - Везде контекстное меню и кнопки: **Add / Rename / Duplicate / Delete**;
     ассеты — drag&drop из проводника прямо в дерево.
4. **Кнопка «Собрать» (компиляция)**:
   - графы → C# → Roslyn → `{GUID}.dll` + портативный `.pdb` + `.xml`;
   - ресурсы → `BALDI_Data/StreamingAssets/Modded/{GUID}/` с подпапками
     `sprites/ models/ audio/ data/*.json` + `mod.json` (манифест мода). Это родной путь
     MTM101BaldAPI: `AssetLoader.GetModPath(plug) == StreamingAssets/Modded/{GUID}`
     (декомпилят, строка 167509; рядом `TextureFromMod`, `AudioClipFromMod` и др.);
   - плагин — по выбору в настройках: **flat** `BepInEx/plugins/{GUID}.dll (+.pdb/.xml)`
     или **отдельная папка** `BepInEx/plugins/{GUID}/` с dll/pdb/xml внутри.
5. **Кнопка «Играть»**: автоопределение exe рядом с `BALDI_Data/` (или запуск через Steam),
   хвост `BepInEx/LogOutput.log` прямо в окне утилиты.
6. **ВСЁ!** — мод лежит в игре.

**UI-стиль**: минимализм + **Comic Sans**, крупные контролы, drag&drop, подсказки.
Тема лежит в `Themes/MinimalComicTheme.xaml` (шрифт, палитра, шаблоны контролов).

---

## 3. Модель проекта (что хранится на диске)

```
MyMod/
  project.bbmod.json        // имя, GUID плагина, версия, зависимости, целевая версия BB+/API
  graphs/                   // логика кирпичиками, один JSON на граф
    npc_BaldiRampage.graph.json
    item_ZestyCannon.graph.json
    patch_Principal.graph.json
    event_Flood.graph.json
  assets/
    sprites/   (.png, листы, повороты)
    models/    (.obj — нативно поддержан MTM101BaldAPI.OBJLoader)
    audio/     (.wav/.ogg)
    posters/, textures/, fonts/
  levels/
    floor1.lvl ...          // бинарный формат Plus Level Format v10 (или наш промежуточный JSON → .lvl)
  out/                      // собранный MyMod.dll + .pdb + .xml + манифест
```

`project.bbmod.json` хранит в т.ч. пути к папке игры (автоопределение Steam/BepInEx),
версии целевых сборок и список зависимостей BepInDependency (дефолты — GUID'ы из §0).

---

## 4. Система кирпичиков (ядро редактора)

### 3.1. Модель графа
- **Graph** = узлы (Node) + соединения (Connection) + переменные + комментарии/группы.
- **Node** имеет:
  - exec-порты (вход/выход потока выполнения, белые «провода»),
  - data-порты (типизированные: `int, float, bool, string, Vector3, Color, GameObject, NPC, Entity, PlayerManager, Item, RoomController, Cell, EnvironmentController, SoundObject, Texture2D, AudioClip, enum…`), цветные «провода»;
  - заголовок, категорию, иконку, подсказку (берутся из декомпилята/XML-доков).
- **Типы графов** (каждый — «слот хука», куда вешается логика):
  - `NPC` (переопределения виртуалок класса персонажа),
  - `Item` (`Use`/`PostUse`),
  - `RoomFunction` (`Build`),
  - `RandomEvent` (событие),
  - `GameManager` / `EnvironmentController` хуки,
  - `HarmonyPatch` (произвольный prefix/postfix на метод любой сборки — см. §6.4),
  - `LoadingEvent` (стадии загрузки ассетов/уровня),
  - «свободный» скрипт (вызывается из других графов как подпрограмма/функция).
- **Сериализация графа** — JSON ( human-readable, diff-friendly, merge-friendly в git).

### 3.2. Встроенные конструкции языка кирпичиков
- Поток: `Start`, последовательность, `Branch (if/else)`, `Switch`, циклы
  (`For`, `While`, `ForEach`), `Wait` (секунды — превращается в корутину),
  `Delay frames`, `Return`, `Break`.
- Данные: литералы, переменные (локальные/поля объекта/глобальные статики проекта),
  cast/конверсия типов, тернарник, math-операции, `Random.Range`, векторная математика.
- Исключения: `Try/Catch`-нода (оборачивает участок в try/catch).

### 3.3. Каталог блоков = производное от декомпилята
Каталог строится **двумя способами**:
1. **Автогенерация**: утилита читает метаданные сборок игры (через Mono.Cecil,
   read-only) — `Assembly-CSharp.dll`, `UnityEngine.*.dll`, `MTM101BaldAPI.dll`,
   `PlusLevelLoader.dll` и т.д. — и для каждого публичного метода/свойства/поля
   создаёт блок «Вызвать метод»/«Get/Set свойства». Это даёт покрытие «всё-всё»
   автоматически и страхует от смены версий API.
2. **Кривые (hand-crafted) блоки** — удобные обёртки над частыми паттернами
   (см. §5), с красивыми именами, иконками и валидацией.

### 3.4. Валидация и подсказки
- Контроль типов на этапе соединения портов (нельзя воткнуть `NPC` в `int` без cast-ноды).
- Предупреждения: недостижимый код, потенциальный бесконечный цикл в `VirtualUpdate`,
  raycast/поиск объектов каждый кадр ( costly в Update ), null-ссылки.
- Поиск по каталогу (Fuzzy-search по имени/классу/методу/сигнатуре).

---

## 5. Что именно можно «натянуть на кирпичики» (каталог сценариев)

Всё ниже — с прямыми ссылками на классы из `ultimate_dev_mods_decompile.cs` (секция BB+ 0.14.5, строка 47197+).

### 4.1. Персонажи (NPC) — база `NPC : MonoBehaviour, IEntityTrigger` (строка 98337)
Хуки кирпичиками (virtual-методы, которые переопределяем):
`Initialize`, `VirtualUpdate` (тик), `Hear(GameObject, Vector3, int)` (шум),
`PlayerInSight / PlayerSighted / PlayerLost`, `VirtualOnTriggerEnter/Stay/Exit`,
`DoorHit(StandardDoor)`, `WindowHit(Window)`, `TargetPosition(Vector3)`,
`SetGuilt/ClearGuilt`, `SentToDetention`, `Sighted/Unsighted`, `Despawn`,
`AddDetour/ActivateDetour/RemoveDetour` (объезд препятствий), поле `Navigator`, `Character`.

Ванильные персонажи, которых можно клонировать/модифицировать блоками:
`Character`-enum: Null, **Baldi, Playtime, Crafters, Bully, Principal, Prize, Sweep,
Cumulo, Chalkles, Beans, Pomp, LookAt, DrReflex** + классы `Baldi, Playtime, Principal,
Bully, Beans, ArtsAndCrafters, GottaSweep, Cumulo, ChalkFace(Chalkles), DrReflex,
NoLateTeacher, LookAtGuy, TestNPC` (+ BBCR-варианты `Balder_Entity`).

Блоки поведения: «гнаться за игроком», «слышать шум и идти туда», «бросаться в detention",
«ломать двери/окна», «патрулировать waypoint'ы», «менять скорость по notebook'ам»,
«телепортироваться», «играть анимацию/звук», «реагировать на конкретный предмет» и т.д.

Регистрация кастомного персонажа в рантайме: наш сгенерированный класс
`class MyNpc : NPC { override VirtualUpdate() { /* сгенерированный из графа код */ } }`
→ `new NPCBuilder<MyNpc>(PluginInfo).Build()` + метаданные в `NPCMetaStorage`
(имя/описание/локализация) → спавн: либо в `npcsToSpawn` менеджера, либо в .lvl через
`NPCInfo{npc = "guid", position}`, либо авто-спавн (`GameManagerNPCAutomaticSpawn`).

### 4.2. Предметы (Item) — база `Item` (строка 78945): `Use(PlayerManager)`, `PostUse(PlayerManager)`
Ванильные: `ITM_BSODA, ITM_Quarter, ITM_ZestyBar, ITM_Acceptable, ITM_Boots,
ITM_GrapplingHook, ITM_Nametag, ITM_NoSquee, ITM_PortalPoster, ITM_PrincipalWhistle,
ITM_Scissors, ITM_SwingDoorLock, ITM_Tape, ITM_Teleporter` (+ BBCR: GlueStick,
InvisibilityElixir, Map, ReachExtender, StickerPack, YTPs, ChalkEraser...).
Кастомный предмет = графы `Use`/`PostUse` + регистрация через `ItemBuilder` +
`ItemMetaStorage`/`ItemMetaData` + `ItemFlags`; раскладка в инвентаре — `ItemManager`/`ItemSlotsManager`;
размещение на уровне — `ItemInfo{item, position}` в .lvl.

### 4.3. Комнаты, клетки, двери, окружение
- `RoomController`, `Cell` (IHeapItem), `RoomFunction` и все его реализации:
  `BalloonSpawner, FacultyTrigger, RuleFreeZone, AmbienceRoomFunction, BeltRoomFunction,
  BlockNavigationRoomFunction, CellBlockRoomFunction, Chalkboard, DetentionRoomFunction,
  LightbulbTestingRoomFunction, LockedRoomFunction, MinigameBaseRoomFunction, SilenceRoomFunction,
  SkyboxRoomFunction, StaminaBoostRoomFunction, StoreRoomFunction, TeleporterRoomFunction,
  WormholeRoomFunction, RespawnItemsRoomFunction, PreventRoomSpawnsRoomFunction,
  CoverRoomFunction, RandomAmbienceRoomFunction...` — каждая комната = свой граф `Build(LevelBuilder, Random)`.
- `EnvironmentController`: `BeginPlay`, `CreateCell/SwapCell/MergeCell/DestroyCell/UpdateCell`,
  `BuildNavMesh`, `SetSpawn` — блоки «создать/заменить клетку», «построить navmesh»...
- Двери/окна: `StandardDoor`, `SwingDoorBuilder`, `LockdownDoorBuilder`, `OneWayDoorBuilder`,
  `CoinDoorBuilder`, `Window`/`WindowObject`, `Door`, `Elevator`/`ClassicExit`.
- Строители коридоров: `GenericHallBuilder, RandomHallBuilder, WeightedHallBuilder,
  RotoHallBuilder, OneWayWallBuilder, BeltBuilder, VentBuilder` (`ObjectBuilder`-система).
- Механизмы: `BeltManager` (конвейеры), `VentController`, `CraneController`,
  `BreakerController`/`PowerLeverController` (свет), `TeleporterController`, `WormholeController`.

### 4.4. События (events) — `EnvironmentController` + event-классы
`RandomEvent`, `RandomEventType`, `WeightedRandomEvent`, `BalderEvent, FloodEvent, FogEvent,
GravityEvent, PartyEvent, StudentEvent, RulerEvent, LockdownEvent, Detour_GravityEvent...`
Кастомное событие = класс-наследник `RandomEvent` + граф логики + регистрация через
`RandomEventBuilder` + `RandomEventMetaStorage/RandomEventMetadata/RandomEventFlags`;
запуск — «поставить в очередь EnvironmentController» блоком.

### 4.5. Этажи / уровни / режимы игры
- `LevelAsset/LevelObject/LevelData/LevelBuilder/LevelLoader/GameLoader`, генераторы:
  `LevelGenerator, MazeGenerator`, `ExtraLevelData`, `ElevatorManager` (переходы между этажами).
- `BaseGameManager/CoreGameManager/MainGameManager/ClassicGameManager/EndlessGameManager...`:
  notebook'и, жизни (power tubes), таймер, победа/поражение, score.
- Кастомные уровни: `CustomLevelObject` + `LevelTypeMetaStorage` + параметры генерации
  (`CustomLevelGenerationParameters`, `GeneratorManagement`, `GenerationStageFlags`) —
  **или** просто .lvl-файл через Plus Level Loader (см. §7).
- Режимы: Hide-and-Seek / Endless / Challenges / Field Trip (+ ModdedModesAPI для своих).

### 4.6. Игрок
`PlayerManager`: урон/жизни, `PlayerMovement` (скорость, stamina — модифицируется через
`ValueModifier`/`PlayerMovementStatModifier`), телепорт, зрение/стелс, инвентарь,
взаимодействие (`ItemAcceptor`, `IItemAcceptor`).

### 4.7. UI / HUD / звук / текст
- `HudManager/HudGaugeManager` (полоски), `SubtitleManager/SubtitleController` (субтитры),
  `LocalizationManager` (ключи локализации), попапы (`PopupManager` из PixelInternalAPI),
  `BaldiTVCharacter` (телевизор), `StickerManager`/`StickerBuilder` (стикеры),
  `PosterObject`, `Chalkboard`, карта (`MapTile`), `BillboardManager`.
- `AudioManager`, `PropagatedAudioManager` (**система распространения шума** —
  NPC слышат через `Hear(...)`), `MusicManager`, `SoundObject` (`ObjectCreators.CreateSoundObject`).

### 4.8. Прочее
- Активности/мини-игры: `Activity`, `MathMachine`, `MinigameBaseRoomFunction`.
- Сохранения: `ModdedSaveGame*` из API, highscores.
- «Кирпичики для Kirпичиков»: наш редактор умеет и **читать** .lvl/чужие DLL (§8).

---

## 6. Генерация BepInEx-плагина (DLL + pdb + xml)

### 5.1. Пайплайн сборки (кнопка Build)
1. Разрешить референсы: автопоиск папки игры (Steam-реестр / `steam://` / путь в настройках),
   референсы: `Assembly-CSharp.dll`, `UnityEngine.*.dll`, `UnityEngine.UI`, `TMPro`,
   `BepInEx.dll`, `0Harmony.dll`, `MTM101BaldAPI.dll`, `PixelInternalAPI.dll`,
   `PlusLevelLoader.dll` (+ опционально Studio), `netstandard`/reference assemblies .NET Framework 4.7.2/4.8 (BB+ на Unity 2021/2022, Mono — таргет `net472` или `netstandard2.0`).
2. Сгенерировать C# из графов (один partial-класс на граф; см. §6.2–6.4).
3. Скомпилировать **Roslyn (Microsoft.CodeAnalysis.CSharp)** прямо в памяти:
   на выходе `MyMod.dll` + **портативный PDB** (`DebugType.Portable`, чтобы стек-трейсы
   и дебаг в dnSpy/Rider читались) + **XML** (`GenerateDocumentationFile = true` —
   из doc-комментариев кирпичиков/описаний сущностей).
4. Посчитать зависимости и записать атрибуты в сгенерированный `Plugin` класс:
   `[BepInPlugin("knoxum.bbplus.<имя>", "<имя>", "<версия>")]` +
   `[BepInDependency("mtm101.rulerp.bbplus.baldidevapi", HardDependency)]` и т.д. (GUID'ы из §0).
5. Скопировать `dll/pdb/xml` в `<Игра>/BepInEx/plugins/` — **flat** (`{GUID}.dll`) или
   **папка** `{GUID}/` (выбор в настройках проекта, §2 п.4).
6. Ресурсы мода (спрайты/модели/аудио/json) — в
   `<Игра>/BALDI_Data/StreamingAssets/Modded/{GUID}/{sprites,models,audio,data}/`
   + `mod.json`; это путь, который читает `AssetLoader.GetModPath` (декомпилят, строка 167509).
7. (Опционально) запустить игру (exe рядом с `BALDI_Data/`, либо через Steam); показать
   хвост `BepInEx/LogOutput.log` прямо в утилите.

### 5.2. NPC из кирпичиков → `NPCBuilder<T>`
Граф персонажа компилируется в `public class <Имя>NPC : NPC { … override … }`;
в `Plugin.Awake()` генерируется:
```csharp
var npc = new NPCBuilder<ИмяNPC>(Info).Build();      // из MTM101BaldAPI
npc.name = "..."; Metadata<NPCMetadata>... / NPCMetaStorage — имя, описание, локализация
```
Дальше — спавн в уровне (.lvl `NPCInfo.npc` = GUID метаданных) или в `npcsToSpawn`.

### 5.3. Предмет / событие / комнатная функция
- Item: `class <Имя>Item : Item { public override bool Use(PlayerManager pm) {…} }` + `ItemBuilder` + `ItemMetaStorage`.
- Event: `class <Имя>Event : RandomEvent {…}` + `RandomEventBuilder` + `RandomEventMetaStorage`.
- RoomFunction: `class … : RoomFunction { public override void Build(LevelBuilder b, Random rng) {…} }`.

### 5.4. Harmony-патчи из кирпичиков (ядро «моддинга BB+»)
В редакторе выбираешь: сборку → тип → метод (дерево из Mono.Cecil по декомпилированным
сборкам, с сигнатурами и XML-доками) → тип патча (Prefix/Postfix; Transpiler — только
через «raw code»-блок). Генерируется ровно то, что сейчас пишется руками в `file.cs`:
```csharp
[HarmonyPatch(typeof(Baldi), "TakeApple")]
static bool Prefix(Baldi __instance) { /* блоки */ }
```
Специальные параметры (`__instance`, `__result`, аргументы метода по имени) — это
data-порты блока патча. «Просмотр логики в кирпичиках» для существующих модов — §8.

### 5.5. Ассеты → рантайм
- **Спрайты**: PNG → `Texture2D` (в рантайме — `TGALoader`/`DDSLoader`/`BMPLoader`/`ImageLoader`
  из API, или встроенный ресурс). Поддержка sprite-листов (`SpriteSheetLoader`, Aseprite),
  поворотов (`SpriteRotatedAnimation`, `CustomSpriteRotatorAnimator`), анимаций
  (`SpriteAnimation`, `Texture2DAnimation`, `CustomAnimator`), billboard'ов.
- **3D-модели**: OBJ — нативно в API (`MTM101BaldAPI.OBJImporter`: `OBJLoader`,
  `OBJObjectBuilder`) — утилита валидирует OBJ, считает bounding box, генерирует код загрузки;
  в редакторе — 3D-превью (например, HelixToolkit). Можно расширить под glTF позже.
- **Аудио**: WAV/OGG → `AudioClip` (NAudio/Vorbis на стороне редактора для превью/конвертации),
  `SoundObject` через `ObjectCreators.CreateSoundObject(clip, subtitle, SoundType, color)` + субтитры.
- **Постеры/текстуры**: `ObjectCreators.CreatePosterObject(tex, PosterTextData[])`,
  `ExtendedPosterObject`, кастомные chalkboard'ы (`CreateLevelTypeChalkboard`).
- **Локализация**: генерация ключей в `LocalizationManager` (en.txt и др.).

---

## 7. Интеграция с MTM101BaldAPI и Plus Level Studio / Loader

### 6.1. MTM101BaldAPI (основной рантайм-API)
- Регистрация ассетов на стадиях загрузки: `LoadingEvents.RegisterOnAssetsLoaded(PluginInfo, Action/IEnumerator, LoadingEventOrder)` —
  в утилите это отдельный тип графа «OnAssetsLoaded (order: First/BeforeVanilla/AfterVanilla/Last...)».
- `ObjectCreators` — двери (`CreateDoorDataObject`), окна (`CreateWindowObject`), звуки, постеры, материалы тайлов карты.
- `MetaStorage`/`Metadata` — «паспорт» каждой сущности (GUID, имя, описание, флаги):
  `NPCMetaStorage`, `ItemMetaStorage`, `RandomEventMetaStorage`, `LevelTypeMetaStorage`, `StickerMetaStorage`, `SceneObjectMetaStorage`.
- Кастомные уровни-генераторы: `CustomLevelObject`, `GeneratorManagement`, `CustomLevelGenerationParameters`.
- Статы игрока: `ValueModifier` / `PlayerMovementStatModifier`.
- Врапперы: `Extensions`, `ReflectionHelpers`, `PrivateCallExtensions` (PixelInternalAPI) —
  блоки «вызвать приватный метод».

### 6.2. Plus Level Loader — пишем .lvl напрямую
Мы реализуем в утилите writer по формату из секции `PlusStudioLevelFormat` (строка 155646+):
`BaldiLevel.Write(BinaryWriter)` версии **10**: сетка `cells[,]`, `rooms` (`RoomInfo`),
`lights`, `tileObjects`, `doors`, `windows`, `exits`, `structures`/`randomStructures`,
`npcs` (`NPCInfo{npc, position}`), `posters`, `premadeRoomPlacements`, `spawnPoint`,
`spawnDirection`, `timeLimit`, `levelTitle`, coverage... Чтение — `BaldiLevel.Read` для импорта
существующих уровней в визуальный редактор.
→ Утилита сама собирает валидные `.lvl`, совместимые с Plus Level Loader 1.12.2.0,
и мы можем распространять уровни отдельно от DLL-мода.

### 6.3. Plus Level Studio — встраиваем свой контент в редактор PLS
- `EditorCustomContent` / `EditorCustomContentPackage` / `EditorCustomContentEntry` —
  регистрируем наши NPC/item'ы/постеры/структуры, чтобы они появлялись в тулбоксах
  PLS (`NPCTool`, `ItemTool`, `PosterTool`, structure tools) и их можно было расставлять.
- Property-страницы: `NPCProperties`, `NPCPropertyPage`, `NPCPropertyExchangeHandler`
  (+ `PrincipalProperties`, `BaldiProperties` и т.д. как образцы) — кирпичики
  «настройки персонажа» → вкладка свойств в PLS.
- Playtest: `PlayableEditorLevel` / `EditorPlayModeManager` — «Play» прямо из PLS.
- **Lua (MoonSharp)**: в PLS уже встроен скриптовый слой (`PlusLevelStudio.Lua`,
  `LuaHelpers`, `EditorLuaGameProxy`). Решено 2026-11-02: **генерируем только C#** —
  BepInEx DLL+pdb+xml с полным доступом к API; Lua-бэкенд (hot-reload превью в PLS) —
  возможное расширение позже.

---

## 8. Просмотр чужих модов и уровней «в кирпичиках»

1. **Импорт DLL-мода**: Mono.Cecil читает метаданные → находим BepInEx-плагины и
   `[HarmonyPatch]`-классы → ICSharpCode.Decompiler декомпилирует методы →
   pattern-matching: известные вызовы (`Navigator.TargetPosition(...)`, `ec.QueueEvent(...)`,
   переопределения `VirtualUpdate` и т.п.) распознаём и превращаем в соответствующие
   кирпичики; нераспознанное — блок **Raw C#** (свободный код внутри графа).
   Результат — редактируемый проект, из которого можно пересобрать мод.
2. **Импорт .lvl**: `BaldiLevel.Read` → визуальный редактор уровня (сетка комнат/дверей/NPC/item'ов),
   редактируем → обратно в .lvl.
3. **Просмотр vanilla-логики**: наш каталог блоков строится прямо из декомпилята
   (§4.3), т.е. можно открыть «Baldi как он есть» и смотреть его логику кирпичиками
   (reference-режим, read-only).

---

## 9. Архитектура самой утилиты (приложения-редактора)

- **Стек**: .NET 8+, UI — **WPF (Windows-only)**, XAML-first, чтобы проект открывался в
  **Blend for Visual Studio**; тема «минимализм + Comic Sans» — `Themes/MinimalComicTheme.xaml`.
  MVVM (CommunityToolkit.Mvvm), хранение — JSON (System.Text.Json), граф-движок — свой
  маленький (Node/Port/Connection) + канвас с pan/zoom/поиском; 3D-превью OBJ — HelixToolkit.
- **Ядро (netstandard-библиотека, тестируемая отдельно)**: модель графа, сериализация,
  валидация, каталог блоков (`IBlockDefinition`: порты + функция генерации C#),
  кодоген (Roslyn SyntaxFactory — безопаснее шаблонов), сборщик (references → emit dll/pdb/xml).
- **Модули UI**: Project Explorer | Graph Editor (канвас кирпичиков + палитра + инспектор) |
  Asset Browser (спрайты/модели/аудио с превью, drag&drop в графы) | Level Editor
  (2D-сетка уровня, как в PLS: комнаты, двери, NPC, item'ы, постеры, структуры) |
  Build & Run (лог сборки, LogOutput.log tail, запуск игры) | Mod Browser (импорт чужих DLL/.lvl) | Settings.
- **Плагинная система блоков**: каталог — отдельные сборки/JSON-манифесты, так что
  комьюнити может добавлять свои «кривые» блоки.

---

## 10. Дорожная карта (фазы)

1. **Фаза 0 — скелет по §2**: solution (WPF + Blend), выбор папки игры с автопроверкой
   BepInEx/API и **автоустановкой BepInEx 5.x + MTM101BaldAPI** (PLS опционально),
   мастер нового мода (имя+GUID), главное окно с деревом компонентов + Inspector,
   модель проекта `.bbmod`, граф-движок + 20 базовых блоков, кодоген,
   сборка «Hello Mod» с раскладкой по §2: ресурсы → `StreamingAssets/Modded/{GUID}/`,
   dll+pdb+xml → `BepInEx/plugins/` (flat или папкой — настройка), кнопка «Играть».
2. **Фаза 1 — персонажи**: NPC-графы (все хуки §5.1), `NPCBuilder<T>`-генерация,
   метаданные/локализация, спавн; первый тестовый NPC «машет рукой и бежит на шум».
3. **Фаза 2 — предметы и события**: Item-графы, RandomEvent-графы, регистрация.
4. **Фаза 3 — ассеты и уровни**: импорт/экспорт спрайтов, OBJ-моделей, аудио; Level Editor;
   writer/читатель .lvl (формат v10); интеграция с PLS (EditorCustomContent-пакеты).
5. **Фаза 4 — обратный инжиниринг**: импорт чужих DLL → кирпичики (Cecil + Decompiler + raw-блоки).
6. **Фаза 5 — полировка**: поиск, шаблоны проектов, туториалы, локализация UI, Lua-бэкенд (опционально).

---

## 11. Риски и решения

- **Версии API**: MTM101BaldAPI/PLS активно меняются → авто-каталог блоков из метаданных
  (а не захардкоженный) + пин зависимостей в манифесте + graceful-degradation (блок помечается «недоступно в этой версии»).
- **Транспайлеры Harmony** из кирпичиков не делаем (только Prefix/Postfix + Raw C#).
- **Производительность**: предупреждения на дорогие блоки в Update; Wait → корутины.
- **Легальность**: декомпилят используем только как reference для сигнатур; в поставку
  утилиты Assembly-CSharp не включаем — референсы берутся из папки игры пользователя.
- **Сложность UI графов**: mitigations — мини-карта, группировка, «collapsed»-подграфы,
  Undo/Redo, автосохранение.

---

## 12. Принятые решения (2026-11-02)

1. **UI: WPF**, проект собирается/оформляется в **Blend for Visual Studio**; тема —
   минимализм + Comic Sans (`Themes/MinimalComicTheme.xaml`).
2. **Бэкенд генерации: только C# → BepInEx DLL + портативный PDB + XML**. Lua-бэкенд отложен.
3. **Имя: BB+ Simple Modder**, GUID генерируемых модов: `com.knoxum.baldiplus.simplemodder.<имя>`.
4. **Workflow end-to-end зафиксирован в §2** (папка игры → автоустановка BepInEx/API →
   имя+GUID → дерево компонентов → «Собрать» → ресурсы в
   `StreamingAssets/Modded/{GUID}/`, dll+pdb+xml в `BepInEx/plugins/` flat или папкой —
   выбор в настройках → «Играть»).
5. **Первая фича не фиксируется** — требования диктуем по ходу процесса моддинга.
6. Формат хранения графов: свой human-readable JSON (diff/merge-friendly в git).
7. Созданы два рабочих документа для передачи другой AI/агенту:
   - `BB_SIMPLE_MODDER_PROMPT.md` — гигантский самодостаточный промпт (контекст, факты,
     workflow §2, solution, спеки, план фаз, definition of done);
   - `UI_XAML_WINDOWS.md` — структура всех окон: App.xaml + тема Comic Sans, StartupWindow,
     NewModWindow, MainWindow, ComponentTreeView, InspectorView, GraphEditorView,
     AssetBrowserView, LevelEditorView, BuildWindow, SettingsWindow, ModBrowserWindow, LogView —
     с полным XAML и code-behind `.xaml.cs` для каждого.
8. Написан исходный код приложения — папка `BBPlusSimpleModder/` (66 файлов, ~4.8k строк):
   solution из 4 проектов (WPF-app `net8.0-windows`, Core `net8.0` без зависимостей,
   Compiler с Roslyn/Cecil/Decompiler, консольные тесты ядра), все окна и контролы
   из `UI_XAML_WINDOWS.md` — `App.xaml`, тема Comic Sans, StartupWindow, NewModWindow,
   MainWindow, ComponentTreeView, InspectorView, GraphEditorView, AssetBrowserView,
   LevelEditorView, BuildWindow, SettingsWindow, ModBrowserWindow, InputDialog, LogView,
   сервисы (GameEnvironment/Project/Build/AssetImport/BlockCatalog/LvlFormat/ModImport…),
   блоковый движок + кодоген + формат .lvl в Core.

Осталось открытым: точный состав «Фазы 1» (определится первыми требованиями),
упаковка ассетов (файлы рядом vs встроенные ресурсы DLL — по умолчанию файлы в
`StreamingAssets/Modded/{GUID}/`, т.к. так их читает `AssetLoader`).
