# BB+ Simple Modder

Визуальный («кирпичиками») редактор модов для **Baldi's Basics Plus**.
Собирает из блоков настоящий BepInEx-плагин: `{GUID}.dll` + портативный `.pdb` + `.xml`,
а ресурсы кладёт в `BALDI_Data/StreamingAssets/Modded/{GUID}/`.

Стек: .NET 8, WPF (`net8.0-windows`), открывается в **Blend for Visual Studio**.
UI — минимализм + Comic Sans, русский язык.

## Состав solution

- `src/BBPlusSimpleModder` — WPF-приложение (окна, канвас кирпичиков, сервисы игры/сборки).
- `src/BBPlusSimpleModder.Core` — ядро без внешних зависимостей: модель проекта,
  блоковый движок (графы/блоки/валидация), генератор C# из графов, формат `.lvl`.
- `src/BBPlusSimpleModder.Compiler` — компиляция через Roslyn (DLL+PDB+XML),
  Mono.Cecil (каталог блоков из сборок игры, импорт чужих модов), ICSharpCode.Decompiler.
- `tests/BBPlusSimpleModder.Core.Tests` — консольные тесты ядра без внешних пакетов.

## Workflow

1. Выбрать папку игры BB+ (если BepInEx/MTM101BaldAPI нет — утилита установит сама).
2. Новый мод: имя + GUID (`com.knoxum.baldiplus.simplemodder.*`).
3. Дерево компонентов (этажи, объекты, персонажи, предметы, события, ассеты, UI, патчи),
   у каждого с логикой — редактор кирпичиков.
4. «Собрать» → ресурсы в `StreamingAssets/Modded/{GUID}/`, плагин в `BepInEx/plugins/`
   (плоско или папкой — в настройках).
5. «Играть» — запуск игры + хвост `BepInEx/LogOutput.log` в окне утилиты.

## Документация

- `../KnoxumPLSExtension_repo/docs/BLOCK_MOD_STUDIO_DESIGN.md` — полный дизайн.
- `../KnoxumPLSExtension_repo/docs/BB_SIMPLE_MODDER_PROMPT.md` — гигантский промпт.
- `../KnoxumPLSExtension_repo/docs/UI_XAML_WINDOWS.md` — все окна с XAML/C#.

## Сборка и запуск

```
dotnet restore
dotnet build -c Release
tests: dotnet run --project tests/BBPlusSimpleModder.Core.Tests
```

**В Visual Studio: запускаемый проект — `BBPlusSimpleModder` (WPF-приложение).**
Solution Explorer → правый клик на проект `BBPlusSimpleModder` →
**«Назначить запускаемым проектом» (Set as Startup Project)** → F5.
(Или: свойства решения → Common Properties → Startup Project → Single startup project → BBPlusSimpleModder.)

Если F5 ругается «библиотека классов … нельзя запустить напрямую» — значит выделен
Core/Compiler/Tests; переключите startup project на приложение, и появится окно
(StartupWindow).

Референсы для компиляции модов берутся из папки игры пользователя
(`BALDI_Data/Managed`, `BepInEx/core`, `BepInEx/plugins`) — сам код игры не распространяется.
