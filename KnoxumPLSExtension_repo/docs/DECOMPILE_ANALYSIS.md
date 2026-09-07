# Разбор `ultimate_dev_mods_decompile.cs`

Анализ декомпилированного дампа, на котором строятся плагины `KnoxumPLSExtension`.
Все ссылки вида `стр. NNNNN` — номера строк в файле `ultimate_dev_mods_decompile.cs` (лежит в корне репозитория).
Полный список типов: `docs/reference/decompile_type_index.md`.

---

## 1. Что внутри файла

182 524 строки, 5.6 МБ, 1789 типов, 48 неймспейсов. Это **не одна игра**, а склейка нескольких декомпиляций:

| Диапазон строк | Содержимое |
|---|---|
| 1 … ~48 000 | **BBCR 1.1a** (Baldi's Basics Classic Remastered) — свой полный набор игровых типов |
| ~48 000 … ~156 000 | **Baldi's Basics Plus** — второй, почти полный дубль тех же имён (`Baldi`, `Cell`, `Door`, `SwingDoor`…) |
| 122 766 … 153 900 | **PlusLevelStudio** (редактор) + `PlusLevelStudio.Ingame` |
| 155 664 … 159 400 | **PlusStudioLevelLoader** и **PlusStudioLevelFormat** (формат `.ebpl`, импорт в рантайм) |
| 162 792 … 181 200 | **MTM101BaldAPI** (API, на котором всё это висит) |
| 161 000 … 162 000 | ModdedModesAPI, PixelInternalAPI, B83.Image.BMP, Microsoft.CodeAnalysis |

### ⚠️ Ловушка №1: дубли типов
Имена в BBCR и BB+ **совпадают**: `Cell` есть и на стр. 55726 (BBCR), и только один раз (BB+ использует тот же `Cell`, т.к. он в global). Но `Door` — стр. 12154 (BBCR) и стр. 63082 (BB+), `Baldi` — 1784 и 50652, `RoomController` — 38050 и 105957, `Tile` — 41757 и 116948.

**Правило:** для BB+ бери вхождения с **большим** номером строки; всё, что ниже ~48 000 — это в основном BBCR.

### ⚠️ Ловушка №2: два разных `Cell`
- `global::Cell` (стр. 55726) — **рантайм-ячейка игры**: у неё `Tile`, `room`, `ConstBin`, coverage, двери, свет.
- `PlusStudioLevelFormat.Cell` (стр. 158254) — **данные редактора**: `ByteVector2 position`, `ushort roomId`, `Nybble walls`.

В WIP-коде это уже учтено (`global::Cell` пишется явно) — правильно.

---

## 2. Модель мира BB+ (рантайм)

### `EnvironmentController` (стр. ~69 900, поля с 71 180)
```
public const float tileSize  = 10f;   // стр. 71215
public const float tileOffset = 5f;   // стр. 71216
public Tile  TilePre                  // стр. 71180  — префаб плитки
public Mesh  TileMesh(int index)      // стр. 71189
private Mesh[] tileMesh = new Mesh[16]; // стр. 71226
public Cell[,] cells                  // стр. 71266
```

### `Cell` (стр. 55726)
```csharp
public void LoadTile()                       // стр. 55766
{
    this.tile = Object.Instantiate<Tile>(this.room.ec.TilePre, this.room.transform);
    this.tile.transform.localPosition = new Vector3(x*10+5, 0f, z*10+5);
    this.tileLoaded = true;
    this.LoadTileMesh();
    this.HideTile(this.tileHidden);
}

private void LoadTileMesh()                  // стр. 55776
{
    this.tile.MeshFilter.sharedMesh = this.room.ec.TileMesh(this.constBin);
    for (int i = 0; i < 4; i++)
        this.tile.Collider((Direction)i).SetActive(this.constBin.ContainsDirection((Direction)i));
}
```

Важные члены: `TileTransform` (= `tile.transform`, стр. 56632), `Tile` (стр. 56647), `room` (стр. 56753),
`SetBase(Material)` (стр. 55814), `AddRenderer(Renderer)` (стр. 55757 / 55864), `AssignLightController(Transform)` (стр. 56516),
`PrepareForPoster()` / `AddPoster()` (стр. 55828+), ConstBin/NavBin/SoundBin, hardCoverage/softCoverage.

### `Tile` (стр. 116948) — намертво простой
```csharp
public MeshFilter   MeshFilter   { get; }   // стр. 116954
public MeshRenderer MeshRenderer { get; }   // стр. 116967
public GameObject Collider(Direction dir)   // стр. 116976 — 4 дочерних объекта-коллайдера
// поля: meshFilter, meshRenderer, GameObject[] collider = new GameObject[4]
```

**Вывод: отдельного объекта «стена» не существует.** Пол, стены и потолок — это один меш плитки, выбранный по бинарной маске `ConstBin` (16 вариантов = 4 бита направлений). Коллайдеры — 4 дочерних `GameObject` (по одному на сторону).

### `RoomController` (стр. 105957)
```csharp
private void Awake()                                  // стр. 105961
{
    this.baseMat   = new Material(this.defaultMat);
    this.posterMat = new Material(this.defaultPosterMat);
}

public void GenerateTextureAtlas(int size)            // стр. 106023
{
    this.textureAtlas = new Texture2D(size*2, size*2, RGBA32, false);
    this.textureAtlas.filterMode = FilterMode.Point;
    SetPixels(0,     0,     size, size, floor);      // пол      → левый нижний квадрант
    SetPixels(size,  size,  size, size, wall);       // стена    → ПРАВЫЙ ВЕРХНИЙ
    SetPixels(0,     size,  size, size, ceiling);    // потолок  → левый верхний
    // если есть альфа → baseMat = new Material(defaultAlphaMat)
    this.baseMat.SetTexture("_MainTex", this.textureAtlas);
    this.posterMat.SetTexture("_MainTex", this.textureAtlas);
}
```

Поля: `florTex / wallTex / ceilTex`, `defaultMat / defaultAlphaMat / defaultPosterMat / defaultAlphaPosterMap`,
`baseMat / posterMat`, `textureAtlas`, `lightPre` (Transform), `lights`, `doors`, `cells`, `standardLightCells`,
`potentialDoorPositions`, `forcedDoorPositions`, `windowObject`, `doorMats` (стр. 106417–106633).

> ⚠️ `wallMat / floorMat / ceilingMat` (стр. 106605–106611) **в BB+ объявлены, но нигде не присваиваются**
> (присвоение есть только в BBCR, стр. 24010). Использовать их для генерации стен **нельзя** — будут `null`.
> Рабочий материал — `room.baseMat` (уже с атласом в `_MainTex`).

---

## 3. Как на самом деле рисуются стены

### 3.1 Атлас и UV — главное
Атлас комнаты — текстура `2*size × 2*size` с раскладкой:

```
(0,1) ┌───────────┬───────────┐ (1,1)
      │  потолок  │   стена   │      ← стена: u,v ∈ [0.5 … 1.0]
      ├───────────┼───────────┤
      │    пол    │  (пусто)  │      ← пол:   u,v ∈ [0.0 … 0.5]
(0,0) └───────────┴───────────┘ (1,0)
```

Меш плитки использует эти UV. Поэтому:

- **`PrimitiveType.Quad` с материалом комнаты показывает ВЕСЬ атлас сразу** (4 текстуры разом) — ровно тот «плейсхолдерный» эффект из твоего разбора.
- Своя геометрия обязана иметь UV **внутри квадранта**: стена `[0.5..1.0]²`, пол `[0.0..0.5]²`.
- **Тайлить/повторять текстуру по вертикали через атлас нельзя**: wrap-режим Repeat на выходе из квадранта подхватит пол/потолок. Для высоких стен это критично — нужно либо резать стену на сегменты по 10 юнитов (каждый сегмент = полный квадрант), либо делать отдельный материал с «сырой» `wallTex`.

### 3.2 Свет
- Световой объект — **дочерний к тайлу**: `Object.Instantiate(room.lightPre, cell.TileTransform)` + `cell.AssignLightController(t)` (стр. 85 460, 85 490, 87 066).
- В редакторе: `workerEc.GenerateLight(cell, color, strength)` + `RegenerateLight(cell)` (стр. 133 490, в `RefreshLights`).
- **В редакторе у каждой плитки свой инстанс материала** (`Tile.MeshRenderer.material`, не `sharedMaterial`) и туда пишется `_LightMap` (стр. 134 377, 134 391) — это система подсветки/хайлайта ячеек.

### 3.3 Подсветка и рендереры в редакторе
```csharp
public class EditorRendererContainer : MonoBehaviour   // стр. 136800
{
    public void AddRenderer(Renderer render, string defaultHighlight);
    public void Highlight(string highlight)            // стр. 136823
    {
        // myRenderers[i].materials[j].SetTexture("_LightMap", LevelStudioPlugin.Instance.lightmaps[...]);
    }
    public List<Renderer> myRenderers;
    public List<string>   defaultHighlights;
}
```
Любой новый рендерер, который должен корректно светиться и подсвечиваться в редакторе, надо регистрировать здесь
(или как минимум копировать `_LightMap` с материала плитки).

### 3.4 Culling
`CullingManager` (стр. 60 700) считает окклюзию по спискам `cell.renderers` (стр. 60 791, 60 815).
Новая геометрия, не добавленная через `cell.AddRenderer(...)`, **не участвует в окклюзии** — будет «просвечивать»
или наоборот не будет считаться препятствием.

---

## 4. Редактор Plus Level Studio

### 4.1 Как редактор вообще рисует уровень
`Singleton<EditorController>` (стр. 132 938). В `AwakeFunction()` (стр. 134 597) он поднимает **настоящие игровые объекты**:

```csharp
this.workerEc = Object.Instantiate<EnvironmentController>(this.ecPrefab);  // "WorkerEnvironmentController"
this.workerEc.lightMode = LightMode.Cumulative;
this.workerRc = this.workerEc.transform.Find("NullRoom").GetComponent<RoomController>();
this.workerRc.ec = this.workerEc;
this.workerRc.ReflectionInvoke("Awake", null);
this.workerCgm = Object.Instantiate<CoreGameManager>(this.cgmPrefab);
this.RegenerateGridAndCells();
```

- `RegenerateGridAndCells()` (стр. 134 545): `InitializeCells` → `cell.LoadTile()` → **`tile.transform.SetParent(gridManager.transform, true)`** → `RefreshCells()`.
  Тайлы в редакторе — дети `GridManager`, а **не** `RoomController`.
- `RefreshCells(bool refreshLights = true, bool updateDataCells = true)` (стр. 134 500) — вызывается при **любом** изменении:
  ```csharp
  this.levelData.UpdateCells(true);
  // для каждой ячейки:
  if (levelData.cells[i,j].type == 16) { Tile.gameObject.SetActive(false); _initialized.SetValue(cell,false); }
  else {
      Tile.gameObject.SetActive(true);
      Tile.MeshRenderer.material.SetMainTexture(this.GenerateTextureAtlas(room.floorTex, room.wallTex, room.ceilTex));
      workerEc.cells[i,j].SetShape(levelData.cells[i,j].type, TileShapeMask.None);
      if (workerEc.cells[i,j].Null) workerEc.cells[i,j].Initialize();
  }
  // premadeRooms → ModifyCellDisplay; UnhighlightAllCells; UpdateStructuresWithReason(CellChange); RefreshLights()
  ```
- `EditorController.GenerateTextureAtlas(floor, wall, ceiling)` (стр. 134 490) пишет текстуры в **`workerRc`** и вызывает `RoomController.GenerateTextureAtlas()`.

**Следствия для мода:**
1. `RefreshCells` — правильная точка входа для перестройки своей геометрии (это уже сделано в `HighWallsGenerator.rewrite.cs` через Harmony-Postfix).
2. Так как атлас пересоздаётся **на каждую комнату в цикле**, `workerRc.baseMat` в любой момент содержит атлас **последней** комнаты. Брать материал/атлас надо по конкретной комнате, а не из `cell.room` (в рерайт-версии это уже обойдено через `ResolveEditorAtlasTexture`).
3. `_initialized` (приватное поле `Cell`) сбрасывается в `false` и `Cell.Initialize()` вызывается заново — значит свои данные, привязанные к `Cell`, надо переживать этот цикл.

### 4.2 Точки расширения, которые даёт сам PLS
```csharp
// стр. 147126
public static T AddRoomVisualManager<T>(string forType) where T : EditorRoomVisualManager
// создаёт GameObject, ConvertToPrefab(true), AddComponent<T>(), кладёт в LevelStudioPlugin.Instance.roomVisuals[forType]
```
- `LevelStudioPlugin.Instance.roomVisuals` — `Dictionary<string, EditorRoomVisualManager>` (стр. 148 432).
- `EditorRoomVisualManager` (абстрактный, стр. ~136 928): `Initialize()`, `Cleanup()`, `RoomUpdated()`, `ModifyLightsForEditor(EnvironmentController workerEc)` (стр. 136 951), поля `myRoom`, `myLevelData`.
- Единственный стандартный наследник — `OutsideRoomVisualManager` (стр. 140 564); он переопределяет только `ModifyLightsForEditor` и красит свет по `myLevelData.IdFromRoom(myRoom)`.
- `EditorInterface` (стр. 146 627) — фасад для модов: `AddObjectVisual`, `AddObjectVisualWithMeshCollider`, `AddRoomVisualManager<T>`, `_LightMap`-хелперы.
- `EditorInterfaceModes.AddModeCallback(...)` (стр. 147 171) — колбэки режимов редактора.
- `LevelStudioPlugin` (стр. 147 982): `Instance.editorTracks`, `lightmaps`, `premadeRoomDoors`, `doorIngameStatus`, `roomVisuals`, `modes`.

### 4.3 Данные уровня
`EditorLevelData` (стр. 135 120), поля (стр. 136 560+): `mapSize`, `cells[,]`, `areas`, `lightGroups`, `lights`,
`rooms` (`List<EditorRoom>`), `doors`, `windows`, `exits`, `items`, `objects`, `structures`, `npcs`, `posters`,
`walls` (`List<WallLocation>`), `markers`, `premadeRooms`, `meta`, `seed`, `skybox`, `lightMode`…

Методы: `UpdateCells(bool forEditor)` (стр. 135 347), `RoomFromId(ushort)` (стр. 135 219), `IdFromRoom(EditorRoom)` (стр. 135 229),
`GetCellSafe(x,y)` (стр. 135 539), `RoomFromPos(...)`, `Compile()` (стр. 135 675), `FinalizeCompile(BaldiLevel)` (стр. 135 650),
`Write(BinaryWriter)` (стр. 135 985), `ReadFrom(BinaryReader)` (стр. 136 229).

`EditorRoom` (стр. 136 852): `name`, `roomType`, `textureContainer`, `activity`, `myVisual` + геттеры `floorTex / wallTex / ceilTex`.
`WallLocation` (стр. 145 657) + `enum WallState { RemoveWall, AddWall, Oneway }` (стр. 145 754) — **уже существующая модель «стены как данных»**, на неё стоит ориентироваться.

### 4.4 Формат `.ebpl`
- `EditorFileContainer` (стр. 134 855): `Write` = `meta.Write` + `data.Write`; `Read` / `ReadMindful`.
- `EditorFileMeta` (стр. ~134 895): `version = 2`, editorMode, toolbarTools, камера.
- `EditorLevelData.ReadFrom` (стр. 136 229): первый байт — версия, **`if (b > 18) throw`** → текущий формат **v18**.
- `Write` заканчивается `this.meta.Write(writer)` (стр. 136 227) — то есть данные идут **сплошным потоком** без оглавления.

**Как добавить свои данные в сейв:** патчить `EditorLevelData.Write` (Postfix — дописать блок в конец, до/после `meta.Write`)
и `EditorLevelData.ReadFrom` (Postfix — дочитать свой блок), плюс поднять версию. Альтернатива без риска поломать
совместимость — писать рядом свой файл/секцию и хранить её отдельно (вариант «внешний sidecar»).

---

## 5. Путь «редактор → рантайм» и проблема `roomId`

```
EditorLevelData.Compile()            // стр. 135675
   └─ FinalizeCompile(BaldiLevel)    // стр. 135650
        baldiLevel.cells[i,j] = new PlusStudioLevelFormat.Cell(this.cells[i,j])
        baldiLevel.rooms.Add(new RoomInfo(roomType, textureContainer) { name = room.name })
        baldiLevel.lights.Add(new LightInfo{...})
        // двери → TileObjectInfo или DoorInfo, в зависимости от DoorIngameStatus

LevelImporter.Import(BaldiLevel)     // стр. ~156569
   for (i = 0; i < level.rooms.Count; i++)
       extendedRoomData.name = string.IsNullOrEmpty(name) ? (type + "_" + i) : name;   // стр. 156572
       ...
   → ExtendedRoomData → RoomAsset → Object.Instantiate<RoomController>(roomControllerPre, ec.transform)
       roomController.transform.position = this.ec.transform.position;                 // стр. 82253
```

**Где теряется `roomId`:**
1. В редакторе `roomId` = индекс в `levelData.rooms` **+ 1** (0 = «нет комнаты»).
2. `Compile()` кладёт комнаты в `baldiLevel.rooms` в том же порядке, но **не пишет сам roomId**.
3. На рантайме `RoomController` создаются в цикле по `level.rooms`, но в `ec.rooms` попадают **все** комнаты, включая
   служебные (`Buffer Tiles` стр. 84 402, `CornerBuffers` стр. 84 652, коридоры `halls`). Порядок/индексы уже не совпадают
   с редакторскими.
4. Единственный надёжный «якорь» — `room.name`: если имя не задано пользователем, оно равно `type + "_" + i`
   (стр. 156 572), т.е. индекс в `level.rooms` = редакторский `roomId - 1`. Но пользователь **может** задать своё имя → парсить нельзя.

**Вывод:** маппинг «комната редактора → `RoomController` рантайма» надо строить явно, до/во время плейтеста:
- вариант A: писать в имя комнаты служебный суффикс при `Compile()` (патчить `FinalizeCompile`), затем парсить его на рантайме;
- вариант B: строить карту по **набору ячеек** комнаты (пересечение `PlusStudioLevelFormat.Cell.roomId` с `RoomController.cells[].position`) — медленнее, но устойчиво;
- вариант C: передавать карту через статическое поле мода, заполняемое в момент `Compile()`/`FinalizeCompile` (EditorController и рантайм живут в одном процессе на плейтесте).

---

## 6. Разбор текущих WIP-файлов

### ✅ Что сделано правильно в `HighWallsGenerator.rewrite.cs`
| Что | Почему это верно |
|---|---|
| Свои меши с явными `normals` / `tangents` / `uv` (стр. 662–757) | Quad не годится: у него один нормал и UV 0..1 (весь атлас) |
| UV стены = `(0.5,0.5)…(1.0,1.0)`, UV пола = `(0,0)…(0.5,0.5)` | Точное попадание в квадранты атласа (см. §3.1) |
| Материал-шаблон = `cell.room.baseMat` (стр. 295) | Рабочий материал комнаты; `wallMat/floorMat/ceilingMat` в BB+ = null |
| Отдельное разрешение атласа для редактора (`ResolveEditorAtlasTexture`, стр. 379) | Иначе все стены получат атлас последней комнаты из цикла `RefreshCells` |
| `RemoveWall`-логика по `PlusStudioLevelFormat.Cell` соседей (стр. 824–877) | Учитывает `roomId` и `walls` (Nybble), а не только ConstBin |
| Harmony-патчи `Cell.AssignLightController`, `EditorController.RefreshCells`, `RoomSettingsExchangeHandler.UpdateTextures` (стр. 1012–1060) | Это ровно те места, где надо пересобирать геометрию и свет |
| Идемпотентность (`Knoxum_Wall_L{layer}_D{dir}` по имени, уничтожение лишних) | `RefreshCells` вызывается constantly |

### 🐞 Что вызывает вопросы
1. **Нет `_LightMap`.** Сгенерированные стены получают копию `baseMat`, но не копируют `_LightMap` с материала плитки и не регистрируются в `EditorRendererContainer`. В редакторе это даёт «стены светятся не так, как плитки» и отсутствие хайлайта выделения — ровно симптом из `high_walls_analysis_and_fix.md`.
2. **Нет `cell.AddRenderer(...)`** для своих рендереров → они вне окклюзии `CullingManager` (см. §3.4).
3. **Переаллокация меша плитки.** `RemoveTopTileFloor` (стр. 494) каждый вызов делает `Instantiate(sourceMesh)`. Оригинал сохраняется (`KnoxumTileMeshBaseline`), так что целостность не ломается, но при частом `RefreshCells` это мусор. Стоит кэшировать по `(originalMesh)` и переиспользовать.
4. **`EmptyPropertyBlock` вместо копирования.** Сейчас блок просто чистится (стр. 641). Для `baseMat` это ок, но если комната с постерами (материал `Shader Graphs/TileStandardWPoster`, стр. 55 828) — поведение надо проверить отдельно.
5. **`GetRoomIdForCell` (стр. 978) на рантайме.** Сначала идёт `Singleton<EditorController>.Instance` — во время плейтеста редактор может ещё существовать, и тогда карта берётся из редакторских данных, а не из `runtimeCellRoomIdMap`. Порядок проверок стоит пересмотреть (сначала явная рантайм-карта).
6. **`RoomController.transform.position = ec.transform.position`** (стр. 82 253) — комнаты сидят в начале координат, а плитки позиционируются `localPosition = (x*10+5, 0, z*10+5)` (стр. 55 769). Текущий подход двигает **каждый тайл**; альтернатива — двигать весь `RoomController.transform` (тогда свет/двери/объекты едут сами). Для «поднятой комнаты» это может быть заметно надёжнее, но сломается стыковка дверей между комнатами разной высоты — вопрос к дизайну.

### 🚧 Главный блокер
**`HighWallsController` отсутствует в репозитории.** Он используется во всех четырёх файлах
(`GetRoomHeight(roomId)`, `GetRoomYOffset(roomId)`, `roomHeights`, `runtimeCellRoomIdMap`,
`currentEditorRampHeightSelection`) и нигде не определён. Без него ни один WIP-файл **не скомпилируется**.
Именно он должен держать:
- `Dictionary<int, (height, yOffset)>` по roomId,
- построение `runtimeCellRoomIdMap` (см. §5),
- сохранение/загрузку в `.ebpl` (см. §4.4),
- UI настроек комнаты.

---

## 7. Рекомендуемый порядок работ

1. **Написать `HighWallsController`** — данные + персистентность + UI. Это база; без неё остальное нельзя собрать.
2. **Выбрать один генератор.** По коду `HighWallsGenerator.rewrite.cs` заметно лучше `.fixed.cs` (свои UV/меши, работа с редактором и атласом) — его и брать за основу, `.fixed.cs` удалить.
3. **Добавить в генератор:** `_LightMap` + `EditorRendererContainer` (редактор), `cell.AddRenderer` (окклюзия), кэш модифицированного меша плитки.
4. **Решить маппинг roomId** (вариант A/B/C из §5) и завести `runtimeCellRoomIdMap`.
5. **Разобраться с дверьми:** `Door` (стр. 63 082), `StandardDoor` (стр. 109 138), `DoorLocation`/`StandardDoorDisplay` в редакторе (стр. 132 518 / 144 200). Проём в нижнем уровне для swing-дверей + окна на верхнем — из `IDEAS.md`.
6. **Только потом** — рампы (`KnoxumRampData` стр. 11, `KnoxumRampSelectionUI`) и привязка объектов к тайлам.
7. **Инфраструктура:** завести `.csproj` со ссылками на BepInEx / UnityEngine / Assembly-CSharp / MTM101BaldAPI / PlusLevelStudio, чтобы код реально собирался, а не проверялся «на глаз».

---

## 8. Что нужно проверить вживую (дамп не отвечает)

- [ ] Действительно ли меш плитки содержит потолок (по атласу `ceilTex` — почти наверняка да, но меш из дампа не вытащить).
- [ ] Высота встроенных стен плитки: 10 юнитов или меньше (влияет на стыковку слоёв).
- [ ] Wrap mode атласа (`Repeat` vs `Clamp`) — определяет, можно ли вообще тайлить стену через атлас.
- [ ] Жив ли `Singleton<EditorController>.Instance` во время плейтеста.
- [ ] Есть ли у `Tile` коллайдер пола (похоже, что нет — пол держится нав-сеткой, а не физикой).
