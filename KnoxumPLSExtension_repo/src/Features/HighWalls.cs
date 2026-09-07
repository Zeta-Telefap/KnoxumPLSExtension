// ============================================================================
//  KnoxumPLSExtension — HighWalls.cs
//  Высокие стены и поднятые комнаты для Plus Level Studio / Baldi's Basics Plus.
//
//  Один файл = вся фича: данные, геометрия, редактор, рантайм, сохранение, UI.
//
//  Основано на разборе декомпиляций (см. docs/DECOMPILE_ANALYSIS.md):
//   • Cell.FloorWorldPosition / CenterWorldPosition (стр. 56602/56612) считаются
//     в координатах сетки и НЕ зависят от позиции тайла → пол комнаты, на котором
//     стоят игрок/NPC/предметы, всегда на высоте сетки. Поэтому тайл
//     поднимается только на yOffset, а стены наращиваются ВВЕРХ от него.
//   • Стены/пол/потолок — это один меш тайла из ec.TileMesh[16] (стр. 71226).
//     Атлас комнаты 2x2: пол [0..0.5]², стена [0.5..1]², потолок u[0..0.5] v[0.5..1]
//     (RoomController.GenerateTextureAtlas, стр. 106023).
//   • В редакторе у каждой плитки инстанс материала с текстурой "_LightMap"
//     (EditorController.HighlightCells/UnhighlightAllCells, стр. 134371/134384).
//   • Двери — дети tile.ObjectBase (EnvironmentController.SetupDoor, стр. 70630),
//     лампы — дети TileTransform (стр. 85460): всё это едет вместе с тайлом.
//   • Undo редактора = сериализация EditorLevelData.Write/ReadFrom (стр. 133060),
//     так что наши данные в этих же методах автоматически попадают в undo.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusStudioLevelFormat;
using UnityEngine;
using UnityEngine.Rendering;

namespace KnoxumPLSExtension.Features
{
    // ========================================================================
    //  1. Данные
    // ========================================================================

    /// <summary>Настройки одной комнаты. Хранятся по roomId редактора (1..N, 0 = нет комнаты).</summary>
    public sealed class HighWallsRoomSettings
    {
        /// <summary>Высота комнаты в слоях по 10 юнитов. 1 = обычная комната.</summary>
        public int height = 1;

        /// <summary>На сколько слоёв (по 10 юнитов) комната поднята над землёй.</summary>
        public int yOffset = 0;

        /// <summary>Рисовать ли «юбку» — стены под полом поднятой комнаты.</summary>
        public bool skirt = true;

        public bool IsDefault => height <= 1 && yOffset <= 0;
    }

    /// <summary>
    /// Центральный контроллер: данные по комнатам, персистентность, маппинг рантайма.
    /// Имена членов совместимы со старыми WIP-файлами (GetRoomHeight/GetRoomYOffset/roomHeights).
    /// </summary>
    public static class HighWallsController
    {
        /// <summary>Высота одного слоя стены в юнитах (совпадает с ec.tileSize).</summary>
        public const float LayerHeight = 10f;

        public const int MinHeight = 1;
        public const int MaxHeight = 8;
        public const int MinOffset = 0;
        public const int MaxOffset = 8;

        /// <summary>Метка, которой помечаются комнаты при компиляции, чтобы найти roomId на рантайме.</summary>
        public const string RoomTag = "#KHW:";

        // roomId -> настройки. Обратная совместимость с WIP-кодом.
        public static readonly Dictionary<int, HighWallsRoomSettings> roomHeights =
            new Dictionary<int, HighWallsRoomSettings>();

        /// <summary>Рантайм-карта: (x, z) ячейки -> roomId редактора.</summary>
        public static readonly Dictionary<(int x, int z), ushort> runtimeCellRoomIdMap =
            new Dictionary<(int x, int z), ushort>();

        /// <summary>Поле используется UI рамп из KnoxumRampSelectionUI.cs.</summary>
        public static int currentEditorRampHeightSelection = 1;

        /// <summary>Вызывается при изменении настроек комнаты (для UI).</summary>
        public static event Action<int> OnRoomChanged;

        private static readonly HighWallsRoomSettings _default = new HighWallsRoomSettings();

        public static HighWallsRoomSettings Get(int roomId)
        {
            if (roomId <= 0) return _default;
            HighWallsRoomSettings s;
            if (roomHeights.TryGetValue(roomId, out s) && s != null) return s;
            return _default;
        }

        public static HighWallsRoomSettings GetOrCreate(int roomId)
        {
            if (roomId <= 0) return _default;
            HighWallsRoomSettings s;
            if (!roomHeights.TryGetValue(roomId, out s) || s == null)
            {
                s = new HighWallsRoomSettings();
                roomHeights[roomId] = s;
            }
            return s;
        }

        public static int GetRoomHeight(int roomId) => Mathf.Clamp(Get(roomId).height, MinHeight, MaxHeight);

        public static int GetRoomYOffset(int roomId) => Mathf.Clamp(Get(roomId).yOffset, MinOffset, MaxOffset);

        public static bool GetSkirt(int roomId) => Get(roomId).skirt;

        public static void SetRoomHeight(int roomId, int height)
        {
            if (roomId <= 0) return;
            int v = Mathf.Clamp(height, MinHeight, MaxHeight);
            HighWallsRoomSettings s = GetOrCreate(roomId);
            if (s.height == v) return;
            s.height = v;
            RaiseChanged(roomId);
        }

        public static void SetRoomYOffset(int roomId, int offset)
        {
            if (roomId <= 0) return;
            int v = Mathf.Clamp(offset, MinOffset, MaxOffset);
            HighWallsRoomSettings s = GetOrCreate(roomId);
            if (s.yOffset == v) return;
            s.yOffset = v;
            RaiseChanged(roomId);
        }

        public static void SetSkirt(int roomId, bool skirt)
        {
            if (roomId <= 0) return;
            HighWallsRoomSettings s = GetOrCreate(roomId);
            if (s.skirt == skirt) return;
            s.skirt = skirt;
            RaiseChanged(roomId);
        }

        /// <summary>Убирает настройки, ставшие дефолтными (чтобы не раздувать сейв).</summary>
        public static void Trim()
        {
            List<int> toRemove = null;
            foreach (KeyValuePair<int, HighWallsRoomSettings> kv in roomHeights)
            {
                if (kv.Value == null || kv.Value.IsDefault)
                {
                    (toRemove ?? (toRemove = new List<int>())).Add(kv.Key);
                }
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++) roomHeights.Remove(toRemove[i]);
            }
        }

        public static void Clear()
        {
            roomHeights.Clear();
            runtimeCellRoomIdMap.Clear();
        }

        public static bool HasAnyCustomRoom()
        {
            foreach (KeyValuePair<int, HighWallsRoomSettings> kv in roomHeights)
            {
                if (kv.Value != null && !kv.Value.IsDefault) return true;
            }
            return false;
        }

        private static void RaiseChanged(int roomId)
        {
            Trim();
            Action<int> handler = OnRoomChanged;
            if (handler != null)
            {
                try { handler(roomId); }
                catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] OnRoomChanged: " + e.Message); }
            }
        }

        // ---------------- сохранение ----------------

        private const uint SaveMagic = 0x57484B6Bu; // произвольная метка блока ("KHW\0")
        private const ushort SaveVersion = 1;

        public static void Write(BinaryWriter writer)
        {
            Trim();
            writer.Write(SaveMagic);
            writer.Write(SaveVersion);
            writer.Write(roomHeights.Count);
            foreach (KeyValuePair<int, HighWallsRoomSettings> kv in roomHeights)
            {
                writer.Write(kv.Key);
                writer.Write(kv.Value.height);
                writer.Write(kv.Value.yOffset);
                writer.Write(kv.Value.skirt);
            }
        }

        /// <summary>
        /// Читает наш блок. Если блока нет (файл сохранён без мода) — тихо отматывает
        /// поток назад и очищает данные. Никогда не бросает наружу.
        /// </summary>
        public static void Read(BinaryReader reader)
        {
            Clear();
            if (reader == null || reader.BaseStream == null) return;

            long start = reader.BaseStream.Position;
            try
            {
                if (start + 6 > reader.BaseStream.Length) return; // magic(4) + version(2) — блок точно не наш
                if (reader.ReadUInt32() != SaveMagic)
                {
                    reader.BaseStream.Position = start;
                    return;
                }
                ushort version = reader.ReadUInt16();
                if (version != SaveVersion)
                {
                    reader.BaseStream.Position = start;
                    return;
                }
                int count = reader.ReadInt32();
                if (count < 0 || count > 4096)
                {
                    reader.BaseStream.Position = start;
                    return;
                }
                for (int i = 0; i < count; i++)
                {
                    int id = reader.ReadInt32();
                    HighWallsRoomSettings s = new HighWallsRoomSettings();
                    s.height = Mathf.Clamp(reader.ReadInt32(), MinHeight, MaxHeight);
                    s.yOffset = Mathf.Clamp(reader.ReadInt32(), MinOffset, MaxOffset);
                    s.skirt = reader.ReadBoolean();
                    if (id > 0) roomHeights[id] = s;
                }
            }
            catch (Exception)
            {
                try { reader.BaseStream.Position = start; } catch { /* игнорируем */ }
                Clear();
            }
        }

        // ---------------- маппинг рантайма ----------------

        /// <summary>Достаёт roomId из имени комнаты, помеченного при компиляции.</summary>
        public static int RoomIdFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int idx = name.LastIndexOf(RoomTag, StringComparison.Ordinal);
            if (idx < 0) return -1;
            int value;
            if (int.TryParse(name.Substring(idx + RoomTag.Length), out value) && value > 0) return value;
            return -1;
        }

        public static ushort GetRuntimeRoomId(int x, int z)
        {
            ushort id;
            if (runtimeCellRoomIdMap.TryGetValue((x, z), out id)) return id;
            return 0;
        }

        public static void SetRuntimeRoomId(int x, int z, ushort roomId)
        {
            runtimeCellRoomIdMap[(x, z)] = roomId;
        }
    }

    // ========================================================================
    //  2. Геометрия
    // ========================================================================

    /// <summary>
    /// Строит и перестраивает дополнительную геометрию стены/потолка на тайле.
    /// Полностью идемпотентен: можно вызывать сколько угодно раз подряд.
    ///
    /// Схема (в локальных координатах тайла, y=0 — пол комнаты):
    ///   • сам тайл поднимается на yOffset*10;
    ///   • запечённые стены тайла закрывают слой [0..10] — рядом с дверьми, окнами,
    ///     объектами и лампами, которые все дети тайла;
    ///   • если height &gt; 1 — у тайла вырезается потолок и дорисовываются
    ///     слои [10..height*10] + новый потолок на height*10;
    ///   • если yOffset &gt; 0 — под полом рисуется юбка [-yOffset*10..0] и «дно».
    /// </summary>
    internal static class HighWallsBuilder
    {
        private const float Half = 5f;
        private const float Inset = 0.012f; // чтобы не z-figth'ить с мешем тайла
        private const string RootName = "Knoxum_HighWalls";

        // --- меши ---
        private static Mesh _wallN, _wallE, _wallS, _wallW;
        private static Mesh _ceiling, _floor;

        // --- кэш модифицированных мешей тайла (оригинал -> без потолка) ---
        private static readonly Dictionary<Mesh, Mesh> _noCeilingCache = new Dictionary<Mesh, Mesh>();

        // --- все меши, созданные нами (чтобы не принять свою копию за оригинал) ---
        private static readonly HashSet<Mesh> _ourMeshes = new HashSet<Mesh>();

        // --- тайлы, на которых есть наша геометрия (быстрая проверка в обходах) ---
        private static readonly HashSet<Transform> _dirty = new HashSet<Transform>();

        // ------------------------------------------------------------------
        //  Главная точка входа
        // ------------------------------------------------------------------
        public static void Apply(global::Cell cell, int roomId)
        {
            if (cell == null || cell.Tile == null) return;

            int height = roomId > 0 ? HighWallsController.GetRoomHeight(roomId) : 1;
            int offset = roomId > 0 ? HighWallsController.GetRoomYOffset(roomId) : 0;
            bool skirt = roomId > 0 && HighWallsController.GetSkirt(roomId);

            Transform tile = cell.Tile.transform;

            if (roomId <= 0 || (height <= 1 && offset <= 0))
            {
                if (_dirty.Contains(tile)) Clear(cell);
                return;
            }

            try
            {
                ApplyInternal(cell, tile, height, offset, skirt);
                _dirty.Add(tile);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Knoxum/HighWalls] Apply failed: " + e);
            }
        }

        /// <summary>Полностью снимает нашу геометрию и возвращает тайл в исходное состояние.</summary>
        public static void Clear(global::Cell cell)
        {
            if (cell == null || cell.Tile == null) return;
            Transform tile = cell.Tile.transform;
            _dirty.Remove(tile);
            try
            {
                HighWallsTileState state = tile.GetComponent<HighWallsTileState>();
                if (state != null)
                {
                    MeshFilter mf = cell.Tile.MeshFilter;
                    if (mf != null && state.originalMesh != null) mf.sharedMesh = state.originalMesh;
                    if (state.renderers != null)
                    {
                        for (int i = 0; i < state.renderers.Count; i++)
                        {
                            if (state.renderers[i] == null) continue;
                            if (cell.renderers != null) cell.renderers.Remove(state.renderers[i]);
                        }
                    }
                    if (state.material != null) UnityEngine.Object.Destroy(state.material);
                    UnityEngine.Object.Destroy(state);
                }

                Transform root = tile.Find(RootName);
                if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Knoxum/HighWalls] Clear failed: " + e);
            }
        }

        // ------------------------------------------------------------------
        private static void ApplyInternal(global::Cell cell, Transform tile, int height, int offset, bool skirt)
        {
            // 1. Высота тайла: пол комнаты = yOffset * 10. Всё, что привязано к тайлу
            //    (двери через ObjectBase, лампы, объекты), едет вместе с ним.
            Vector3 lp = tile.localPosition;
            float targetY = offset * HighWallsController.LayerHeight;
            if (Mathf.Abs(lp.y - targetY) > 0.001f)
                tile.localPosition = new Vector3(lp.x, targetY, lp.z);

            HighWallsTileState state = tile.GetComponent<HighWallsTileState>();
            if (state == null) state = tile.gameObject.AddComponent<HighWallsTileState>();

            MeshFilter filter = cell.Tile.MeshFilter;
            MeshRenderer source = cell.Tile.MeshRenderer;
            if (filter == null || source == null) return;

            // 2. Меш тайла: запоминаем оригинал и (для высоких комнат) вырезаем потолок.
            //    Важно не принять свою же копию без потолка за оригинал.
            Mesh current = filter.sharedMesh;
            if (current != null && !_ourMeshes.Contains(current)) state.originalMesh = current;

            Mesh wanted = height > 1 ? GetMeshWithoutCeiling(state.originalMesh) : state.originalMesh;
            if (wanted != null && filter.sharedMesh != wanted) filter.sharedMesh = wanted;

            // 3. Материал и атлас комнаты.
            Material template = ResolveTemplateMaterial(cell);
            Texture atlas = ResolveAtlas(cell);
            Texture lightMap = GetLightMap(source);

            // 4. Перестройка детей.
            Transform root = tile.Find(RootName);
            if (root == null)
            {
                GameObject go = new GameObject(RootName);
                go.transform.SetParent(tile, false);
                root = go.transform;
            }

            // сбрасываем старые рендереры из списка ячейки (окклюзия)
            if (state.renderers != null)
            {
                for (int i = 0; i < state.renderers.Count; i++)
                {
                    if (state.renderers[i] != null && cell.renderers != null)
                        cell.renderers.Remove(state.renderers[i]);
                }
                state.renderers.Clear();
            }
            else state.renderers = new List<Renderer>();

            int bin = cell.ConstBin;
            var needed = new HashSet<string>();

            // --- верхние слои стен (выше запечённого первого слоя) ---
            for (int dir = 0; dir < 4; dir++)
            {
                if ((bin & (1 << dir)) == 0) continue; // стены на этой стороне нет
                if (!ShouldBuildWall(cell, dir, true)) continue;

                for (int layer = 1; layer < height; layer++)
                {
                    string name = "W_" + layer + "_" + dir;
                    needed.Add(name);
                    Transform t = FindChild(root, name);
                    if (t == null)
                    {
                        GameObject go = new GameObject(name);
                        go.transform.SetParent(root, false);
                        t = go.transform;
                    }
                    PositionWall(t, dir, layer * HighWallsController.LayerHeight);
                    SetupWallPiece(t, dir, cell, template, atlas, lightMap, state, true);
                }
            }

            // --- потолок наверху ---
            if (height > 1)
            {
                string name = "Ceiling";
                needed.Add(name);
                Transform t = FindChild(root, name);
                if (t == null)
                {
                    GameObject go = new GameObject(name);
                    go.transform.SetParent(root, false);
                    t = go.transform;
                }
                t.localPosition = new Vector3(0f, height * HighWallsController.LayerHeight, 0f);
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                SetupPiece(t, GetCeilingMesh(), cell, template, atlas, lightMap, state, -1, false);
            }

            // --- юбка + дно под поднятой комнатой ---
            if (offset > 0 && skirt)
            {
                for (int dir = 0; dir < 4; dir++)
                {
                    if ((bin & (1 << dir)) == 0) continue;
                    if (!ShouldBuildWall(cell, dir, false)) continue;

                    for (int layer = 1; layer <= offset; layer++)
                    {
                        string name = "S_" + layer + "_" + dir;
                        needed.Add(name);
                        Transform t = FindChild(root, name);
                        if (t == null)
                        {
                            GameObject go = new GameObject(name);
                            go.transform.SetParent(root, false);
                            t = go.transform;
                        }
                        PositionWall(t, dir, -layer * HighWallsController.LayerHeight);
                        SetupWallPiece(t, dir, cell, template, atlas, lightMap, state, true);
                    }
                }

                string underName = "Underside";
                needed.Add(underName);
                Transform ut = FindChild(root, underName);
                if (ut == null)
                {
                    GameObject go = new GameObject(underName);
                    go.transform.SetParent(root, false);
                    ut = go.transform;
                }
                ut.localPosition = new Vector3(0f, -Inset * 2f, 0f);
                // переворот вокруг X: нормаль пола смотрит вниз, видно снизу
                ut.localRotation = Quaternion.Euler(180f, 0f, 0f);
                ut.localScale = Vector3.one;
                SetupPiece(ut, GetFloorMesh(), cell, template, atlas, lightMap, state, -1, false);
            }

            // --- удаляем всё, что стало лишним ---
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child != null && !needed.Contains(child.name))
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        /// <summary>Точка для будущей логики «проёмы в нижних слоях для swing-дверей».</summary>
        private static bool ShouldBuildWall(global::Cell cell, int dir, bool upper)
        {
            // Двери в BB+ — дети tile.ObjectBase и занимают только нижний слой,
            // поэтому верхние слои над дверью строим всегда (это просто стена над проёмом).
            return true;
        }

        private static void PositionWall(Transform t, int dir, float baseY)
        {
            float y = baseY + Half;
            switch (dir)
            {
                case 0: t.localPosition = new Vector3(0f, y, Half - Inset); break;  // North (+Z), смотрим внутрь -Z
                case 1: t.localPosition = new Vector3(Half - Inset, y, 0f); break;  // East  (+X), внутрь -X
                case 2: t.localPosition = new Vector3(0f, y, -Half + Inset); break; // South (-Z), внутрь +Z
                case 3: t.localPosition = new Vector3(-Half + Inset, y, 0f); break; // West  (-X), внутрь +X
            }
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private static void SetupWallPiece(Transform t, int dir, global::Cell cell,
            Material template, Texture atlas, Texture lightMap,
            HighWallsTileState state, bool collider)
        {
            SetupPiece(t, GetWallMesh(dir), cell, template, atlas, lightMap, state, dir, collider);
        }

        /// <summary>dir: 0..3 — стена (нужна для ориентации коллайдера), -1 — горизонтальная деталь.</summary>
        private static void SetupPiece(Transform t, Mesh mesh, global::Cell cell,
            Material template, Texture atlas, Texture lightMap,
            HighWallsTileState state, int dir, bool collider)
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();

            if (mf.sharedMesh != mesh) mf.sharedMesh = mesh;

            ApplyMaterial(mr, state, template, atlas, lightMap);

            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (collider)
            {
                BoxCollider bc = t.GetComponent<BoxCollider>();
                if (bc == null) bc = t.gameObject.AddComponent<BoxCollider>();
                // BoxCollider — осеориентированный: толщина вдоль той оси, куда смотрит стена
                if (dir == 0 || dir == 2) bc.size = new Vector3(10f, 10f, 0.2f);
                else if (dir == 1 || dir == 3) bc.size = new Vector3(0.2f, 10f, 10f);
                else bc.size = new Vector3(10f, 0.2f, 10f);
                bc.center = Vector3.zero;
            }
            else
            {
                BoxCollider bc = t.GetComponent<BoxCollider>();
                if (bc != null) UnityEngine.Object.Destroy(bc);
            }

            if (!state.renderers.Contains(mr)) state.renderers.Add(mr);
            if (cell.renderers != null && !cell.renderers.Contains(mr)) cell.renderers.Add(mr);
        }

        /// <summary>
        /// Один материал на ячейку (а не на каждый кусок стены): шаблон, атлас и
        /// _LightMap одинаковы для всей нашей геометрии, поэтому делим его.
        /// </summary>
        private static Material ResolveSharedMaterial(HighWallsTileState state,
            Material template, Texture atlas, Texture lightMap)
        {
            if (state == null) return null;

            if (state.material != null &&
                ReferenceEquals(state.materialTemplate, template) &&
                ReferenceEquals(state.materialAtlas, atlas) &&
                ReferenceEquals(state.materialLightMap, lightMap))
            {
                return state.material;
            }

            Material mat = state.material;
            if (mat == null || (template != null && mat.shader != template.shader))
                mat = template != null ? new Material(template) : null;
            else if (template != null) mat.CopyPropertiesFromMaterial(template);

            if (mat != null)
            {
                if (atlas != null && mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
                if (mat.HasProperty("_LightMap")) mat.SetTexture("_LightMap", lightMap);
            }

            state.material = mat;
            state.materialTemplate = template;
            state.materialAtlas = atlas;
            state.materialLightMap = lightMap;
            return mat;
        }

        private static void ApplyMaterial(MeshRenderer mr, HighWallsTileState state,
            Material template, Texture atlas, Texture lightMap)
        {
            if (mr == null) return;
            Material mat = ResolveSharedMaterial(state, template, atlas, lightMap);
            if (mat != null) mr.sharedMaterial = mat;
        }

        // ------------------------------------------------------------------
        //  Материалы / атлас
        // ------------------------------------------------------------------
        private static Material ResolveTemplateMaterial(global::Cell cell)
        {
            RoomController room = cell.room;
            if (room != null)
            {
                if (room.baseMat != null) return room.baseMat;
                if (room.posterMat != null) return room.posterMat;
            }
            MeshRenderer mr = cell.Tile != null ? cell.Tile.MeshRenderer : null;
            if (mr != null)
            {
                if (mr.sharedMaterial != null) return mr.sharedMaterial;
                try { if (mr.material != null) return mr.material; } catch { /* не в редакторе */ }
            }
            return null;
        }

        private static Texture ResolveAtlas(global::Cell cell)
        {
            // В редакторе все плитки делят один workerRc, и его baseMat в любой момент
            // содержит атлас последней комнаты из цикла RefreshCells — поэтому атлас
            // берём по конкретной комнате.
            EditorController editor = IsEditor() ? Singleton<EditorController>.Instance : null;
            if (editor != null && editor.levelData != null)
            {
                int roomId = HighWallsEditorHook.RoomIdForCell(editor, cell);
                if (roomId > 0)
                {
                    Texture editorAtlas = HighWallsEditorHook.GetRoomAtlas(editor, roomId);
                    if (editorAtlas != null) return editorAtlas;
                }
            }

            RoomController room = cell.room;
            if (room != null)
            {
                if (room.textureAtlas != null) return room.textureAtlas;
                if (room.baseMat != null && room.baseMat.HasProperty("_MainTex"))
                {
                    Texture t = room.baseMat.GetTexture("_MainTex");
                    if (t != null) return t;
                }
            }

            MeshRenderer mr = cell.Tile != null ? cell.Tile.MeshRenderer : null;
            if (mr != null)
            {
                Material m = mr.sharedMaterial;
                if (m != null && m.HasProperty("_MainTex"))
                {
                    Texture t = m.GetTexture("_MainTex");
                    if (t != null) return t;
                }
            }
            return null;
        }

        private static Texture GetLightMap(MeshRenderer source)
        {
            if (source == null) return null;
            Material shared = source.sharedMaterial;
            if (shared != null && shared.HasProperty("_LightMap"))
            {
                Texture t = shared.GetTexture("_LightMap");
                if (t != null) return t;
            }
            try
            {
                Material inst = source.material;
                if (inst != null && inst.HasProperty("_LightMap")) return inst.GetTexture("_LightMap");
            }
            catch { /* рантайм: инстанса может не быть */ }
            return null;
        }

        // ------------------------------------------------------------------
        //  Меши
        // ------------------------------------------------------------------
        private static Mesh GetWallMesh(int dir)
        {
            switch (dir)
            {
                case 0: return _wallN ?? (_wallN = BuildWallMesh(Vector3.back));
                case 1: return _wallE ?? (_wallE = BuildWallMesh(Vector3.left));
                case 2: return _wallS ?? (_wallS = BuildWallMesh(Vector3.forward));
                default: return _wallW ?? (_wallW = BuildWallMesh(Vector3.right));
            }
        }

        /// <summary>
        /// Стена 10x10, нормаль смотрит внутрь ячейки (inward).
        /// UV = правый верхний квадрант атласа (там текстура стены).
        /// </summary>
        private static Mesh BuildWallMesh(Vector3 inward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, inward).normalized * Half;
            Vector3 up = Vector3.up * Half;

            Mesh mesh = new Mesh();
            mesh.name = "Knoxum_Wall";
            mesh.vertices = new[]
            {
                -right - up,
                 right - up,
                -right + up,
                 right + up
            };
            mesh.normals = new[] { inward, inward, inward, inward };
            Vector4 tangent = new Vector4(right.normalized.x, right.normalized.y, right.normalized.z, 1f);
            mesh.tangents = new[] { tangent, tangent, tangent, tangent };
            mesh.uv = new[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(1.0f, 0.5f),
                new Vector2(0.5f, 1.0f),
                new Vector2(1.0f, 1.0f)
            };
            // обход по часовой: лицевая сторона смотрит вдоль inward
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Потолок: квад 10x10 нормалью вниз, UV = левый верхний квадрант.</summary>
        private static Mesh GetCeilingMesh()
        {
            if (_ceiling != null) return _ceiling;
            Mesh mesh = new Mesh();
            mesh.name = "Knoxum_Ceiling";
            mesh.vertices = new[]
            {
                new Vector3(-Half, 0f, -Half),
                new Vector3(-Half, 0f,  Half),
                new Vector3( Half, 0f, -Half),
                new Vector3( Half, 0f,  Half)
            };
            mesh.normals = new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down };
            Vector4 t = new Vector4(1f, 0f, 0f, 1f);
            mesh.tangents = new[] { t, t, t, t };
            mesh.uv = new[]
            {
                new Vector2(0.0f, 0.5f),
                new Vector2(0.0f, 1.0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 1.0f)
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            _ceiling = mesh;
            return mesh;
        }

        /// <summary>Пол: квад 10x10 нормалью вверх, UV = левый нижний квадрант.</summary>
        private static Mesh GetFloorMesh()
        {
            if (_floor != null) return _floor;
            Mesh mesh = new Mesh();
            mesh.name = "Knoxum_Floor";
            mesh.vertices = new[]
            {
                new Vector3(-Half, 0f, -Half),
                new Vector3(-Half, 0f,  Half),
                new Vector3( Half, 0f, -Half),
                new Vector3( Half, 0f,  Half)
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            Vector4 t = new Vector4(1f, 0f, 0f, 1f);
            mesh.tangents = new[] { t, t, t, t };
            mesh.uv = new[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, 0.5f),
                new Vector2(0.5f, 0.0f),
                new Vector2(0.5f, 0.5f)
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();
            _floor = mesh;
            return mesh;
        }

        /// <summary>
        /// Копия меша тайла без потолочных треугольников (вершины сверху, нормаль вниз).
        /// Результат кэшируется по исходному мешу.
        /// </summary>
        private static Mesh GetMeshWithoutCeiling(Mesh source)
        {
            if (source == null) return null;
            Mesh cached;
            if (_noCeilingCache.TryGetValue(source, out cached) && cached != null) return cached;

            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            int[] triangles = source.triangles;
            if (vertices == null || triangles == null || vertices.Length == 0)
            {
                _noCeilingCache[source] = source;
                return source;
            }

            float top = 0f;
            for (int i = 0; i < vertices.Length; i++) if (vertices[i].y > top) top = vertices[i].y;
            float threshold = top - 0.5f;
            if (top < 1f)
            {
                // меш без явного верха (маловероятно) — ничего не режем
                _noCeilingCache[source] = source;
                return source;
            }

            List<int> kept = new List<int>(triangles.Length);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (a >= vertices.Length || b >= vertices.Length || c >= vertices.Length) continue;

                bool atTop = vertices[a].y >= threshold && vertices[b].y >= threshold && vertices[c].y >= threshold;
                bool facingDown = false;
                if (normals != null && normals.Length > a && normals.Length > b && normals.Length > c)
                {
                    Vector3 avg = (normals[a] + normals[b] + normals[c]) / 3f;
                    facingDown = avg.y < -0.5f;
                }
                if (atTop && facingDown) continue; // это потолок — выбрасываем

                kept.Add(a); kept.Add(b); kept.Add(c);
            }

            if (kept.Count == triangles.Length)
            {
                // потолка в меше нет — оставляем как есть
                _noCeilingCache[source] = source;
                return source;
            }

            Mesh copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name + "_NoCeiling";
            copy.triangles = kept.ToArray();
            copy.RecalculateBounds();
            _noCeilingCache[source] = copy;
            _ourMeshes.Add(copy);
            return copy;
        }

        // ------------------------------------------------------------------
        private static Transform FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                if (t != null && t.name == name) return t;
            }
            return null;
        }

        private static bool IsEditor()
        {
            try { return Singleton<EditorController>.Instance != null; }
            catch { return false; }
        }

        /// <summary>Выбрасывает мёртвые ссылки на уничтоженные тайлы (смена размера карты и т.п.).</summary>
        public static void PurgeDestroyedTiles()
        {
            if (_dirty.Count == 0) return;
            List<Transform> dead = null;
            foreach (Transform t in _dirty)
            {
                if (t == null) (dead ?? (dead = new List<Transform>())).Add(t);
            }
            if (dead != null) for (int i = 0; i < dead.Count; i++) _dirty.Remove(dead[i]);
        }

        /// <summary>Полный сброс кэшей (меши/материалы/атласы). Вызывать при смене уровня.</summary>
        public static void ResetCaches()
        {
            _noCeilingCache.Clear();
            _ourMeshes.Clear();
            _dirty.Clear();
        }

        /// <summary>Обновляет только материалы (после смены текстур комнаты).</summary>
        public static void RefreshMaterials(global::Cell cell)
        {
            if (cell == null || cell.Tile == null) return;
            Transform root = cell.Tile.transform.Find(RootName);
            if (root == null) return;

            Material template = ResolveTemplateMaterial(cell);
            Texture atlas = ResolveAtlas(cell);
            Texture lightMap = GetLightMap(cell.Tile.MeshRenderer);
            HighWallsTileState state = cell.Tile.transform.GetComponent<HighWallsTileState>();

            for (int i = 0; i < root.childCount; i++)
            {
                MeshRenderer mr = root.GetChild(i).GetComponent<MeshRenderer>();
                if (mr != null) ApplyMaterial(mr, state, template, atlas, lightMap);
            }
        }

        /// <summary>Синхронизирует _LightMap нашей геометрии с плиткой (подсветка в редакторе).</summary>
        public static void SyncLightMap(global::Cell cell, Texture lightMap)
        {
            if (cell == null || cell.Tile == null) return;
            Transform root = cell.Tile.transform.Find(RootName);
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                MeshRenderer mr = root.GetChild(i).GetComponent<MeshRenderer>();
                if (mr == null) continue;
                Material m = mr.sharedMaterial;
                if (m != null && m.HasProperty("_LightMap")) m.SetTexture("_LightMap", lightMap);
            }
        }
    }

    /// <summary>Состояние, привязанное к объекту тайла: оригинальный меш и наши рендереры.</summary>
    internal sealed class HighWallsTileState : MonoBehaviour
    {
        public Mesh originalMesh;
        public List<Renderer> renderers = new List<Renderer>();

        // общий материал нашей геометрии + ключи, по которым он собран
        public Material material;
        public Material materialTemplate;
        public Texture materialAtlas;
        public Texture materialLightMap;
    }

    // ========================================================================
    //  3. Редактор
    // ========================================================================

    internal static class HighWallsEditorHook
    {
        private static readonly Dictionary<int, Texture> _atlasCache = new Dictionary<int, Texture>();
        private static readonly Dictionary<int, int> _atlasKeyCache = new Dictionary<int, int>();

        public static void RefreshAll(EditorController editor)
        {
            if (editor == null || editor.levelData == null) return;
            EnvironmentController ec = editor.workerEc;
            if (ec == null || ec.cells == null) return;

            HighWallsBuilder.PurgeDestroyedTiles();

            int w = ec.cells.GetLength(0);
            int h = ec.cells.GetLength(1);

            for (int x = 0; x < w; x++)
            {
                for (int z = 0; z < h; z++)
                {
                    global::Cell cell = ec.cells[x, z];
                    if (cell == null || cell.Tile == null) continue;

                    int roomId = RoomIdForCell(editor, cell);
                    HighWallsBuilder.Apply(cell, roomId);
                }
            }
        }

        public static int RoomIdForCell(EditorController editor, global::Cell cell)
        {
            if (editor == null || editor.levelData == null || cell == null) return 0;
            PlusStudioLevelFormat.Cell editorCell = editor.levelData.GetCellSafe(cell.position.x, cell.position.z);
            if (editorCell != null && editorCell.roomId != 0) return editorCell.roomId;
            return 0;
        }

        /// <summary>
        /// Атлас конкретной комнаты редактора. EditorController.GenerateTextureAtlas
        /// внутри использует кэш EnvironmentController.GetMatchingTextureAtlas,
        /// поэтому повторные вызовы дешёвые; сверху держим свой кэш по текстурам.
        /// </summary>
        public static Texture GetRoomAtlas(EditorController editor, int roomId)
        {
            if (editor == null || editor.levelData == null || roomId <= 0) return null;

            EditorRoom room = editor.levelData.RoomFromId((ushort)roomId);
            if (room == null) return null;

            Texture2D floor = room.floorTex;
            Texture2D wall = room.wallTex;
            Texture2D ceil = room.ceilTex;

            int key = 0;
            key = (key * 397) ^ (floor != null ? floor.GetInstanceID() : 0);
            key = (key * 397) ^ (wall != null ? wall.GetInstanceID() : 0);
            key = (key * 397) ^ (ceil != null ? ceil.GetInstanceID() : 0);

            int oldKey;
            Texture cached;
            if (_atlasCache.TryGetValue(roomId, out cached) && cached != null &&
                _atlasKeyCache.TryGetValue(roomId, out oldKey) && oldKey == key)
            {
                return cached;
            }

            Texture atlas;
            try
            {
                atlas = editor.GenerateTextureAtlas(floor, wall, ceil);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Knoxum/HighWalls] GenerateTextureAtlas failed: " + e.Message);
                return cached;
            }

            if (atlas != null)
            {
                _atlasCache[roomId] = atlas;
                _atlasKeyCache[roomId] = key;
            }
            return atlas ?? cached;
        }

        public static void InvalidateAtlasCache()
        {
            _atlasCache.Clear();
            _atlasKeyCache.Clear();
        }
    }

    [HarmonyPatch(typeof(EditorController), "RefreshCells")]
    internal static class Patch_EditorController_RefreshCells
    {
        private static void Postfix(EditorController __instance)
        {
            if (__instance == null) return;
            try { HighWallsEditorHook.RefreshAll(__instance); }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] RefreshCells: " + e.Message); }
        }
    }

    // Подсветка ячеек в редакторе: тайлы получают _LightMap, наша геометрия — тоже.
    [HarmonyPatch(typeof(EditorController), "HighlightCells")]
    internal static class Patch_EditorController_HighlightCells
    {
        private static void Postfix(EditorController __instance, IntVector2[] positions, string highlight)
        {
            if (__instance == null || __instance.workerEc == null || positions == null) return;
            Texture2D map;
            try { map = LevelStudioPlugin.Instance.lightmaps[highlight]; }
            catch { return; }

            for (int i = 0; i < positions.Length; i++)
            {
                global::Cell cell = __instance.workerEc.CellFromPosition(positions[i]);
                if (cell != null) HighWallsBuilder.SyncLightMap(cell, map);
            }
        }
    }

    [HarmonyPatch(typeof(EditorController), "UnhighlightAllCells")]
    internal static class Patch_EditorController_UnhighlightAllCells
    {
        private static void Postfix(EditorController __instance)
        {
            if (__instance == null || __instance.workerEc == null || __instance.workerEc.cells == null) return;
            Texture2D map;
            try { map = LevelStudioPlugin.Instance.lightmaps["none"]; }
            catch { return; }

            global::Cell[,] cells = __instance.workerEc.cells;
            for (int x = 0; x < cells.GetLength(0); x++)
                for (int z = 0; z < cells.GetLength(1); z++)
                {
                    global::Cell cell = cells[x, z];
                    if (cell != null) HighWallsBuilder.SyncLightMap(cell, map);
                }
        }
    }

    /// <summary>При смене текстур комнаты в настройках — перекрашиваем геометрию этой комнаты.</summary>
    [HarmonyPatch(typeof(PlusLevelStudio.Editor.SettingsUI.RoomSettingsExchangeHandler), "UpdateTextures")]
    internal static class Patch_RoomSettingsExchangeHandler_UpdateTextures
    {
        private static void Postfix()
        {
            EditorController editor;
            try { editor = Singleton<EditorController>.Instance; }
            catch { return; }
            if (editor == null) return;
            HighWallsEditorHook.InvalidateAtlasCache();
            HighWallsEditorHook.RefreshAll(editor);
        }
    }

    // ========================================================================
    //  4. Сохранение / загрузка (.ebpl и undo — один и тот же путь)
    // ========================================================================

    [HarmonyPatch(typeof(EditorLevelData), "Write")]
    internal static class Patch_EditorLevelData_Write
    {
        private static void Postfix(BinaryWriter writer)
        {
            try { HighWallsController.Write(writer); }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] Write: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(EditorLevelData), "ReadFrom")]
    internal static class Patch_EditorLevelData_ReadFrom
    {
        private static void Postfix(BinaryReader reader)
        {
            try { HighWallsController.Read(reader); }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] ReadFrom: " + e.Message); }
        }
    }

    // ========================================================================
    //  5. Редактор → рантайм: проброс roomId
    // ========================================================================

    internal static class HighWallsCompileHook
    {
        /// <summary>Помечает имена комнат в скомпилированном уровне, чтобы найти roomId на рантайме.</summary>
        public static void TagRooms(BaldiLevel level)
        {
            if (level == null || level.rooms == null) return;
            for (int i = 0; i < level.rooms.Count; i++)
            {
                RoomInfo info = level.rooms[i];
                if (info == null) continue;
                string name = info.name ?? string.Empty;
                if (name.Contains(HighWallsController.RoomTag)) continue;
                info.name = name + HighWallsController.RoomTag + (i + 1);
            }
        }
    }

    [HarmonyPatch(typeof(EditorLevelData), "Compile")]
    internal static class Patch_EditorLevelData_Compile
    {
        private static void Postfix(BaldiLevel __result)
        {
            try { HighWallsCompileHook.TagRooms(__result); }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] Compile: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(EditorController), "Compile")]
    internal static class Patch_EditorController_Compile
    {
        private static void Postfix(BaldiLevel __result)
        {
            try { HighWallsCompileHook.TagRooms(__result); }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] Compile(controller): " + e.Message); }
        }
    }

    // ========================================================================
    //  6. Рантайм
    // ========================================================================

    internal static class HighWallsRuntime
    {
        /// <summary>roomId по объекту RoomController: сначала метка в имени, потом фолбэки.</summary>
        public static int RoomIdFor(RoomController room)
        {
            if (room == null) return -1;

            int tagged = HighWallsController.RoomIdFromName(room.name);
            if (tagged > 0) return tagged;

            // Имя могло не дойти — пробуем карту ячеек.
            if (room.cells != null && room.cells.Count > 0)
            {
                global::Cell cell = room.cells[0];
                if (cell != null)
                {
                    ushort mapped = HighWallsController.GetRuntimeRoomId(cell.position.x, cell.position.z);
                    if (mapped != 0) return mapped;
                }
            }

            // Последний шанс: авто-имя вида "type_i"
            string name = room.name ?? string.Empty;
            int idx = name.LastIndexOf('_');
            if (idx >= 0 && idx < name.Length - 1)
            {
                int parsed;
                if (int.TryParse(name.Substring(idx + 1), out parsed)) return parsed + 1;
            }
            return -1;
        }

        public static void ApplyToCell(EnvironmentController ec, IntVector2 position, RoomController room)
        {
            if (ec == null || room == null) return;
            if (!ec.ContainsCoordinates(position)) return;

            global::Cell cell = ec.CellFromPosition(position);
            if (cell == null || cell.Tile == null) return;

            int roomId = RoomIdFor(room);
            if (roomId <= 0)
            {
                HighWallsBuilder.Clear(cell);
                return;
            }

            HighWallsController.SetRuntimeRoomId(position.x, position.z, (ushort)roomId);
            HighWallsBuilder.Apply(cell, roomId);
        }
    }

    /// <summary>
    /// Рантайм-хук: CreateCell — единственное место, где ячейка уже получила
    /// комнату, форму и тайл (EnvironmentController.CreateCell, стр. 68951).
    /// </summary>
    [HarmonyPatch(typeof(EnvironmentController), "CreateCell",
        new[] { typeof(int), typeof(Transform), typeof(IntVector2), typeof(RoomController), typeof(bool), typeof(bool) })]
    internal static class Patch_EnvironmentController_CreateCell
    {
        private static void Postfix(EnvironmentController __instance, IntVector2 position, RoomController room)
        {
            try { HighWallsRuntime.ApplyToCell(__instance, position, room); }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] CreateCell: " + e.Message); }
        }
    }

    /// <summary>Атлас комнаты готов — перекрашиваем нашу геометрию.</summary>
    [HarmonyPatch(typeof(RoomController), "GenerateTextureAtlas", new Type[] { })]
    internal static class Patch_RoomController_GenerateTextureAtlas
    {
        private static void Postfix(RoomController __instance)
        {
            if (__instance == null || __instance.cells == null) return;
            try
            {
                for (int i = 0; i < __instance.cells.Count; i++)
                    HighWallsBuilder.RefreshMaterials(__instance.cells[i]);
            }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] GenerateTextureAtlas: " + e.Message); }
        }
    }

    // ========================================================================
    //  7. UI редактора (оверлей; позже можно перевести на нативный UI PLS)
    // ========================================================================

    internal sealed class HighWallsEditorUI : MonoBehaviour
    {
        private bool _visible;
        private Rect _rect = new Rect(24f, 24f, 340f, 420f);
        private Vector2 _scroll;
        private int _windowId = 0x4B4857;

        private void Awake()
        {
            _windowId = GetInstanceID();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            _rect = GUI.Window(_windowId, _rect, DrawWindow, "Knoxum — High Walls  (F8)");
        }

        private void DrawWindow(int id)
        {
            EditorController editor;
            try { editor = Singleton<EditorController>.Instance; }
            catch { editor = null; }

            GUILayout.BeginVertical();

            if (editor == null || editor.levelData == null)
            {
                GUILayout.Label("Зайди в редактор Plus Level Studio.");
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            List<EditorRoom> rooms = editor.levelData.rooms;
            GUILayout.Label("Комнат: " + rooms.Count);
            GUILayout.Space(4);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300f));
            for (int i = 0; i < rooms.Count; i++)
            {
                EditorRoom room = rooms[i];
                if (room == null) continue;
                int roomId = i + 1;
                DrawRoom(editor, room, roomId);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            if (GUILayout.Button("Пересобрать геометрию"))
            {
                HighWallsEditorHook.InvalidateAtlasCache();
                HighWallsBuilder.ResetCaches();
                Refresh(editor);
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _rect.width, 20f));
        }

        private void DrawRoom(EditorController editor, EditorRoom room, int roomId)
        {
            HighWallsRoomSettings s = HighWallsController.Get(roomId);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(string.IsNullOrEmpty(room.name)
                ? room.roomType + " #" + roomId
                : room.name + "  (" + room.roomType + " #" + roomId + ")");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Высота: " + s.height, GUILayout.Width(90f));
            if (GUILayout.Button("-", GUILayout.Width(28f))) Change(editor, roomId, -1, 0);
            if (GUILayout.Button("+", GUILayout.Width(28f))) Change(editor, roomId, +1, 0);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Подъём: " + s.yOffset, GUILayout.Width(90f));
            if (GUILayout.Button("-", GUILayout.Width(28f))) Change(editor, roomId, 0, -1);
            if (GUILayout.Button("+", GUILayout.Width(28f))) Change(editor, roomId, 0, +1);
            GUILayout.EndHorizontal();

            bool skirt = GUILayout.Toggle(s.skirt, " Юбка под полом");
            if (skirt != s.skirt)
            {
                AddUndo(editor);
                HighWallsController.SetSkirt(roomId, skirt);
                Refresh(editor);
            }

            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        private void Change(EditorController editor, int roomId, int dHeight, int dOffset)
        {
            AddUndo(editor);
            if (dHeight != 0) HighWallsController.SetRoomHeight(roomId, HighWallsController.GetRoomHeight(roomId) + dHeight);
            if (dOffset != 0) HighWallsController.SetRoomYOffset(roomId, HighWallsController.GetRoomYOffset(roomId) + dOffset);
            Refresh(editor);
        }

        private static void AddUndo(EditorController editor)
        {
            try { editor.AddUndo(); }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] AddUndo: " + e.Message); }
        }

        private static void Refresh(EditorController editor)
        {
            try { editor.RefreshCells(true, true); }
            catch (Exception e) { Debug.LogWarning("[Knoxum/HighWalls] RefreshCells: " + e.Message); }
        }
    }

    // ========================================================================
    //  8. Точка входа плагина
    // ========================================================================

    [BepInPlugin("knoxum.pls.extension.highwalls", "Knoxum High Walls", "1.0.0")]
    public sealed class KnoxumHighWallsPlugin : BaseUnityPlugin
    {
        public const string HarmonyId = "knoxum.pls.extension.highwalls";

        private void Awake()
        {
            // Если в твоём основном плагине уже вызывается PatchAll для этой же сборки,
            // выставь ApplyHarmonyPatches=false в конфиге, чтобы патчи не дублировались.
            bool apply = Config.Bind("General", "ApplyHarmonyPatches", true,
                "Применять Harmony-патчи из этого файла.").Value;

            if (apply)
            {
                Harmony harmony = new Harmony(HarmonyId);
                harmony.PatchAll();
                Logger.LogInfo("[Knoxum/HighWalls] patches applied");
            }

            gameObject.AddComponent<HighWallsEditorUI>();
            Logger.LogInfo("[Knoxum/HighWalls] готов. Оверлей настроек — F8 в редакторе.");
        }
    }
}
