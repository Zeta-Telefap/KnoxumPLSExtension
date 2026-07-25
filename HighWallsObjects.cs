using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using PlusLevelStudio.Editor;

namespace KnoxumPLSExtension.Features
{
    [Serializable]
    public struct KnoxumRampData
    {
        public Direction upDirection;
        public int riseSteps;
        public int length;
    }

    public sealed class KnoxumBoundObject : MonoBehaviour
    {
        public int cellX;
        public int cellZ;
        public Vector3 localOffset;
        public Vector3 localEuler;
        public Vector3 localScale = Vector3.one;
        public bool snapToSurface = true;
        public bool alignToRamp = false;
        public int extraHeightSteps = 0;
    }

    public static class HighWallsObjects
    {
        private const float TileSize = 10f;
        private const float HalfTile = 5f;
        private const float LayerHeight = 10f;
        private const float RampColliderSkin = 0.05f;
        private const float PlatformColliderThickness = 0.2f;
        private const float WallColliderThickness = 0.2f;
        private const string PosterShaderName = "Shader Graphs/TileStandardWPoster";
        private const string Prefix = "KnoxumObj_";

        public static readonly Dictionary<(int x, int z), int> raisedCells = new Dictionary<(int x, int z), int>();
        public static readonly Dictionary<(int x, int z), KnoxumRampData> ramps = new Dictionary<(int x, int z), KnoxumRampData>();

        private static readonly Dictionary<int, EditorAtlasCacheEntry> editorAtlasCache = new Dictionary<int, EditorAtlasCacheEntry>();
        private static readonly MaterialPropertyBlock EmptyPropertyBlock = new MaterialPropertyBlock();
        private static Mesh cachedRaisedFloorMesh;
        private static readonly Dictionary<(int dir, int steps), Mesh> cachedRampMeshes = new Dictionary<(int, int), Mesh>();
        private static readonly Mesh[] cachedWallMeshes = new Mesh[4];
        private static System.Reflection.MethodInfo editorGenerateTextureAtlasMethod;

        private struct EditorAtlasCacheEntry
        {
            public int floorTexId;
            public int wallTexId;
            public int ceilTexId;
            public Texture atlas;
        }

        private struct SurfaceMaterialState
        {
            public Material templateMaterial;
            public Texture atlasTexture;
            public ShadowCastingMode shadowCastingMode;
            public bool receiveShadows;
            public LightProbeUsage lightProbeUsage;
            public ReflectionProbeUsage reflectionProbeUsage;
            public bool valid;
        }

        public static void SetRaisedCell(int x, int z, int steps, bool refresh = true)
        {
            steps = Mathf.Clamp(steps, 0, 1);
            var key = (x, z);
            if (steps <= 0)
                raisedCells.Remove(key);
            else
                raisedCells[key] = steps;

            if (refresh)
                RefreshCellAndNeighbors(x, z);
        }

        public static int GetRaisedCellSteps(int x, int z)
        {
            int value;
            return raisedCells.TryGetValue((x, z), out value) ? Mathf.Clamp(value, 0, 1) : 0;
        }

        public static void SetRamp(int x, int z, Direction upDirection, int length = 1, int riseSteps = 1, bool refresh = true)
        {
            if (upDirection != Direction.North && upDirection != Direction.East && upDirection != Direction.South && upDirection != Direction.West)
                return;

            ramps[(x, z)] = new KnoxumRampData
            {
                upDirection = upDirection,
                riseSteps = Mathf.Clamp(riseSteps, 1, 10),
                length = Mathf.Clamp(length, 1, 10)
            };

            if (refresh)
                RefreshCellAndNeighbors(x, z);
        }

        public static void RemoveRamp(int x, int z, bool refresh = true)
        {
            ramps.Remove((x, z));
            if (refresh)
                RefreshCellAndNeighbors(x, z);
        }

        public static bool TryGetRamp(int x, int z, out KnoxumRampData ramp)
        {
            return ramps.TryGetValue((x, z), out ramp);
        }

        public static bool TryGetRampOwnerAtCell(int x, int z, out (int x, int z) ownerKey, out KnoxumRampData ramp)
        {
            ownerKey = (0, 0);
            ramp = default(KnoxumRampData);

            if (ramps.TryGetValue((x, z), out ramp))
            {
                ownerKey = (x, z);
                return true;
            }

            Direction[] dirs = new Direction[] { Direction.North, Direction.East, Direction.South, Direction.West };
            for (int i = 0; i < dirs.Length; i++)
            {
                int nx = x, nz = z;
                ApplyDirectionOffset(dirs[i], ref nx, ref nz);

                KnoxumRampData neighborRamp;
                if (ramps.TryGetValue((nx, nz), out neighborRamp))
                {
                    if (RampCoversCell(nx, nz, neighborRamp, x, z))
                    {
                        ownerKey = (nx, nz);
                        ramp = neighborRamp;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool RampCoversCell(int ownerX, int ownerZ, KnoxumRampData ramp, int targetX, int targetZ)
        {
            int dx = targetX - ownerX;
            int dz = targetZ - ownerZ;

            switch (ramp.upDirection)
            {
                case Direction.North: return dx == 0 && dz >= 0 && dz < ramp.length;
                case Direction.East: return dz == 0 && dx >= 0 && dx < ramp.length;
                case Direction.South: return dx == 0 && dz <= 0 && dz > -ramp.length;
                case Direction.West: return dz == 0 && dx <= 0 && dx > -ramp.length;
                default: return false;
            }
        }

        public static void ClearAll(bool refresh = false)
        {
            raisedCells.Clear();
            ramps.Clear();

            if (refresh)
            {
                EditorController editor = Singleton<EditorController>.Instance;
                if (editor != null)
                    RefreshAllEditorCells(editor);

                EnvironmentController runtimeEc = Singleton<BaseGameManager>.Instance != null ? Singleton<BaseGameManager>.Instance.Ec : null;
                if (runtimeEc != null)
                    RefreshAllRuntimeCells(runtimeEc);
            }
        }

        public static void ProcessCell(global::Cell cell)
        {
            if (cell == null || cell.Tile == null)
                return;

            int roomId = HighWallsGenerator.GetRoomIdForCell(cell);
            if (roomId <= 0)
                return;

            ProcessCell(cell, roomId);
        }

        public static void ProcessCell(global::Cell cell, int roomId)
        {
            if (cell == null || cell.Tile == null || roomId <= 0)
                return;

            ClearGeneratedChildren(cell.Tile.transform);

            int raisedSteps = GetRaisedCellSteps(cell.position.x, cell.position.z);
            KnoxumRampData ramp;
            bool hasRamp = TryGetRamp(cell.position.x, cell.position.z, out ramp);

            if (raisedSteps <= 0 && !hasRamp)
            {
                RefreshBoundObjectsOnCell(cell.position.x, cell.position.z);
                return;
            }

            SurfaceMaterialState materialState = ResolveSurfaceMaterialState(cell, roomId, cell.Tile.MeshRenderer);
            float baseFloorLocalY = GetBaseFloorLocalY(roomId);

            if (hasRamp)
            {
                BuildRampSurface(cell, roomId, baseFloorLocalY, ramp, materialState);
            }
            else if (raisedSteps > 0)
            {
                BuildRaisedPlatform(cell, roomId, baseFloorLocalY, raisedSteps, materialState);
            }

            RefreshBoundObjectsOnCell(cell.position.x, cell.position.z);
        }

        public static float GetBaseFloorLocalY(int roomId)
        {
            int roomHeight = Mathf.Max(1, HighWallsController.GetRoomHeight(roomId));
            return -((roomHeight - 1) * LayerHeight);
        }

        public static float EvaluateSurfaceLocalY(global::Cell cell, int roomId, float localX, float localZ)
        {
            if (cell == null)
                return 0f;

            float baseY = GetBaseFloorLocalY(roomId);

            KnoxumRampData ramp;
            if (TryGetRamp(cell.position.x, cell.position.z, out ramp))
            {
                float t = 0f;
                switch (ramp.upDirection)
                {
                    case Direction.North:
                        t = Mathf.InverseLerp(-HalfTile, HalfTile, localZ);
                        break;
                    case Direction.East:
                        t = Mathf.InverseLerp(-HalfTile, HalfTile, localX);
                        break;
                    case Direction.South:
                        t = Mathf.InverseLerp(HalfTile, -HalfTile, localZ);
                        break;
                    case Direction.West:
                        t = Mathf.InverseLerp(HalfTile, -HalfTile, localX);
                        break;
                }
                return baseY + t * Mathf.Clamp(ramp.riseSteps, 1, 10) * LayerHeight;
            }

            return baseY + GetRaisedCellSteps(cell.position.x, cell.position.z) * LayerHeight;
        }

        public static float GetSurfaceTopWorldY(global::Cell cell)
        {
            if (cell == null || cell.TileTransform == null)
                return 0f;

            int roomId = HighWallsGenerator.GetRoomIdForCell(cell);
            if (roomId <= 0)
                return cell.TileTransform.position.y;

            float localTop = EvaluateSurfaceLocalY(cell, roomId, 0f, 0f);
            KnoxumRampData ramp;
            if (TryGetRamp(cell.position.x, cell.position.z, out ramp))
                localTop = GetBaseFloorLocalY(roomId) + Mathf.Clamp(ramp.riseSteps, 1, 10) * LayerHeight;

            return cell.TileTransform.position.y + localTop;
        }

        public static void BindObjectToCell(Transform target, int cellX, int cellZ, Vector3 localOffset, bool snapToSurface = true, bool alignToRamp = false, int extraHeightSteps = 0)
        {
            if (target == null)
                return;

            KnoxumBoundObject binding = target.GetComponent<KnoxumBoundObject>();
            if (binding == null)
                binding = target.gameObject.AddComponent<KnoxumBoundObject>();

            binding.cellX = cellX;
            binding.cellZ = cellZ;
            binding.localOffset = localOffset;
            binding.snapToSurface = snapToSurface;
            binding.alignToRamp = alignToRamp;
            binding.extraHeightSteps = extraHeightSteps;

            ApplyBinding(binding);
        }

        public static void ApplyBinding(KnoxumBoundObject binding)
        {
            if (binding == null)
                return;

            global::Cell cell;
            if (!TryGetCell(binding.cellX, binding.cellZ, out cell) || cell == null || cell.TileTransform == null)
                return;

            int roomId = HighWallsGenerator.GetRoomIdForCell(cell);
            if (roomId <= 0)
                return;

            binding.transform.SetParent(cell.TileTransform, false);
            binding.transform.localScale = binding.localScale;
            binding.transform.localEulerAngles = binding.localEuler;

            Vector3 localPosition = binding.localOffset;
            if (binding.snapToSurface)
            {
                float surfaceLocalY = EvaluateSurfaceLocalY(cell, roomId, localPosition.x, localPosition.z);
                localPosition.y = surfaceLocalY + binding.localOffset.y + binding.extraHeightSteps * LayerHeight;
            }
            binding.transform.localPosition = localPosition;

            if (binding.alignToRamp)
                AlignObjectToRamp(binding.transform, cell);
        }

        public static void RefreshAllBoundObjectsInScene()
        {
            KnoxumBoundObject[] objects = UnityEngine.Object.FindObjectsOfType<KnoxumBoundObject>(true);
            for (int i = 0; i < objects.Length; i++)
                ApplyBinding(objects[i]);
        }

        public static void RefreshBoundObjectsOnCell(int x, int z)
        {
            KnoxumBoundObject[] objects = UnityEngine.Object.FindObjectsOfType<KnoxumBoundObject>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].cellX == x && objects[i].cellZ == z)
                    ApplyBinding(objects[i]);
            }
        }

        private static void AlignObjectToRamp(Transform target, global::Cell cell)
        {
            if (target == null || cell == null)
                return;

            KnoxumRampData ramp;
            if (!TryGetRamp(cell.position.x, cell.position.z, out ramp))
                return;

            float angle = Mathf.Atan2(ramp.riseSteps * LayerHeight, TileSize) * Mathf.Rad2Deg;
            float savedY = target.localEulerAngles.y;

            switch (ramp.upDirection)
            {
                case Direction.North:
                    target.localEulerAngles = new Vector3(-angle, savedY, 0f);
                    break;
                case Direction.East:
                    target.localEulerAngles = new Vector3(0f, savedY, angle);
                    break;
                case Direction.South:
                    target.localEulerAngles = new Vector3(angle, savedY, 0f);
                    break;
                case Direction.West:
                    target.localEulerAngles = new Vector3(0f, savedY, -angle);
                    break;
            }
        }

        private static void BuildRaisedPlatform(global::Cell cell, int roomId, float baseFloorLocalY, int raisedSteps, SurfaceMaterialState materialState)
        {
            Transform tileTransform = cell.Tile.transform;
            float platformLocalY = baseFloorLocalY + raisedSteps * LayerHeight;

            GameObject top = CreateOrReuseChild(tileTransform, Prefix + "RaisedTop");
            top.transform.localPosition = new Vector3(0f, platformLocalY, 0f);
            top.transform.localRotation = Quaternion.identity;
            top.transform.localScale = Vector3.one;

            MeshFilter topMf = EnsureComponent<MeshFilter>(top);
            MeshRenderer topMr = EnsureComponent<MeshRenderer>(top);
            topMf.sharedMesh = GetOrCreateRaisedFloorMesh();
            ApplySurfaceMaterial(topMr, materialState, ShadowCastingMode.On);

            if (ShouldGeneratePhysicalColliders())
            {
                BoxCollider col = EnsureComponent<BoxCollider>(top);
                col.center = new Vector3(0f, -PlatformColliderThickness * 0.5f, 0f);
                col.size = new Vector3(TileSize, PlatformColliderThickness, TileSize);
                col.isTrigger = false;
            }
            else
            {
                RemoveComponentIfExists<BoxCollider>(top);
            }

            BuildRaisedSideWalls(cell, roomId, platformLocalY, materialState);
        }

        private static void BuildRaisedSideWalls(global::Cell cell, int roomId, float currentTopLocalY, SurfaceMaterialState materialState)
        {
            Direction[] dirs = new Direction[] { Direction.North, Direction.East, Direction.South, Direction.West };
            for (int i = 0; i < dirs.Length; i++)
            {
                Direction dir = dirs[i];
                int nx = cell.position.x;
                int nz = cell.position.z;
                ApplyDirectionOffset(dir, ref nx, ref nz);

                global::Cell neighbor;
                float neighborTopWorldY = 0f;
                bool hasNeighbor = TryGetCell(nx, nz, out neighbor) && neighbor != null && !neighbor.Null;
                if (hasNeighbor)
                    neighborTopWorldY = GetSurfaceTopWorldY(neighbor);

                float currentTopWorldY = cell.TileTransform.position.y + currentTopLocalY;
                float visibleHeight = currentTopWorldY - neighborTopWorldY;
                if (!hasNeighbor)
                    visibleHeight = currentTopWorldY;

                if (visibleHeight <= 0.01f)
                    continue;

                CreateRaisedWallSegment(cell.Tile.transform, dir, neighborTopWorldY - cell.TileTransform.position.y, currentTopLocalY, materialState, Prefix + "Wall_" + dir);
            }
        }

        private static void BuildRampSurface(global::Cell cell, int roomId, float baseFloorLocalY, KnoxumRampData ramp, SurfaceMaterialState materialState)
        {
            Transform tileTransform = cell.Tile.transform;

            GameObject rampObject = CreateOrReuseChild(tileTransform, Prefix + "Ramp");
            rampObject.transform.localPosition = Vector3.zero;
            rampObject.transform.localRotation = Quaternion.identity;
            rampObject.transform.localScale = Vector3.one;

            MeshFilter mf = EnsureComponent<MeshFilter>(rampObject);
            MeshRenderer mr = EnsureComponent<MeshRenderer>(rampObject);
            Mesh rampMesh = GetOrCreateRampMesh(ramp.upDirection, ramp.riseSteps);
            mf.sharedMesh = rampMesh;
            ApplySurfaceMaterial(mr, materialState, ShadowCastingMode.On);

            rampObject.transform.localPosition = new Vector3(0f, baseFloorLocalY, 0f);

            if (ShouldGeneratePhysicalColliders())
            {
                MeshCollider col = EnsureComponent<MeshCollider>(rampObject);
                col.sharedMesh = rampMesh;
                col.convex = false;
            }
            else
            {
                RemoveComponentIfExists<MeshCollider>(rampObject);
            }

            float rampTopLocalY = baseFloorLocalY + ramp.riseSteps * LayerHeight;
            BuildRampSideWalls(cell, roomId, baseFloorLocalY, rampTopLocalY, materialState);
        }

        private static void BuildRampSideWalls(global::Cell cell, int roomId, float rampBottomLocalY, float rampTopLocalY, SurfaceMaterialState materialState)
        {
            Direction[] dirs = new Direction[] { Direction.North, Direction.East, Direction.South, Direction.West };
            for (int i = 0; i < dirs.Length; i++)
            {
                Direction dir = dirs[i];
                int nx = cell.position.x;
                int nz = cell.position.z;
                ApplyDirectionOffset(dir, ref nx, ref nz);

                global::Cell neighbor;
                float neighborTopWorldY = 0f;
                bool hasNeighbor = TryGetCell(nx, nz, out neighbor) && neighbor != null && !neighbor.Null;
                if (hasNeighbor)
                    neighborTopWorldY = GetSurfaceTopWorldY(neighbor);

                float currentTopWorldY = cell.TileTransform.position.y + rampTopLocalY;
                float visibleHeight = currentTopWorldY - neighborTopWorldY;
                if (!hasNeighbor)
                    visibleHeight = currentTopWorldY;

                if (visibleHeight <= 0.01f)
                    continue;

                CreateRaisedWallSegment(cell.Tile.transform, dir, neighborTopWorldY - cell.TileTransform.position.y, rampTopLocalY, materialState, Prefix + "RampWall_" + dir);
            }
        }

        private static void CreateRaisedWallSegment(Transform parent, Direction dir, float bottomLocalY, float topLocalY, SurfaceMaterialState materialState, string name)
        {
            float height = topLocalY - bottomLocalY;
            if (height <= 0.01f)
                return;

            GameObject wall = CreateOrReuseChild(parent, name);
            wall.transform.localRotation = Quaternion.identity;
            wall.transform.localScale = Vector3.one;

            MeshFilter mf = EnsureComponent<MeshFilter>(wall);
            MeshRenderer mr = EnsureComponent<MeshRenderer>(wall);
            mf.sharedMesh = GetOrCreateWallMesh(dir);
            ApplySurfaceMaterial(mr, materialState, ShadowCastingMode.Off);

            float localCenterY = (bottomLocalY + topLocalY) * 0.5f;
            Vector3 localPosition = GetEdgeLocalPosition(dir, localCenterY);
            wall.transform.localPosition = localPosition;

            Vector3 localScale = wall.transform.localScale;
            localScale.y = height / TileSize;
            wall.transform.localScale = localScale;

            if (ShouldGeneratePhysicalColliders())
            {
                BoxCollider col = EnsureComponent<BoxCollider>(wall);
                col.isTrigger = false;
                col.center = Vector3.zero;
                if (dir == Direction.North || dir == Direction.South)
                    col.size = new Vector3(TileSize, height, WallColliderThickness);
                else
                    col.size = new Vector3(WallColliderThickness, height, TileSize);
            }
            else
            {
                RemoveComponentIfExists<BoxCollider>(wall);
            }
        }

        private static Vector3 GetEdgeLocalPosition(Direction dir, float y)
        {
            switch (dir)
            {
                case Direction.North: return new Vector3(0f, y, HalfTile - RampColliderSkin);
                case Direction.East: return new Vector3(HalfTile - RampColliderSkin, y, 0f);
                case Direction.South: return new Vector3(0f, y, -HalfTile + RampColliderSkin);
                case Direction.West: return new Vector3(-HalfTile + RampColliderSkin, y, 0f);
                default: return new Vector3(0f, y, 0f);
            }
        }

        private static void ApplyDirectionOffset(Direction dir, ref int x, ref int z)
        {
            switch (dir)
            {
                case Direction.North: z += 1; break;
                case Direction.East: x += 1; break;
                case Direction.South: z -= 1; break;
                case Direction.West: x -= 1; break;
            }
        }

        private static void ApplySurfaceMaterial(MeshRenderer targetRenderer, SurfaceMaterialState state, ShadowCastingMode overrideShadowMode)
        {
            if (targetRenderer == null)
                return;

            if (state.templateMaterial != null)
            {
                Material targetMaterial = targetRenderer.sharedMaterial;
                if (targetMaterial == null || targetMaterial.shader != state.templateMaterial.shader)
                {
                    targetMaterial = new Material(state.templateMaterial);
                    targetMaterial.name = Prefix + "Mat_" + state.templateMaterial.name;
                    targetRenderer.sharedMaterial = targetMaterial;
                }
                else
                {
                    targetMaterial.CopyPropertiesFromMaterial(state.templateMaterial);
                }

                SetMaterialMainTexture(targetMaterial, state.atlasTexture);
            }

            targetRenderer.SetPropertyBlock(EmptyPropertyBlock);
            targetRenderer.shadowCastingMode = overrideShadowMode;
            targetRenderer.receiveShadows = state.receiveShadows;
            targetRenderer.lightProbeUsage = state.lightProbeUsage;
            targetRenderer.reflectionProbeUsage = state.reflectionProbeUsage;
        }

        private static SurfaceMaterialState ResolveSurfaceMaterialState(global::Cell cell, int roomId, MeshRenderer sourceRenderer)
        {
            SurfaceMaterialState state = new SurfaceMaterialState();
            state.templateMaterial = ResolveTemplateMaterial(cell, sourceRenderer);
            state.atlasTexture = ResolveAtlasTexture(cell, roomId, sourceRenderer);
            state.shadowCastingMode = sourceRenderer != null ? sourceRenderer.shadowCastingMode : ShadowCastingMode.On;
            state.receiveShadows = sourceRenderer != null && sourceRenderer.receiveShadows;
            state.lightProbeUsage = sourceRenderer != null ? sourceRenderer.lightProbeUsage : LightProbeUsage.Off;
            state.reflectionProbeUsage = sourceRenderer != null ? sourceRenderer.reflectionProbeUsage : ReflectionProbeUsage.Off;
            state.valid = state.templateMaterial != null;
            return state;
        }

        private static Material ResolveTemplateMaterial(global::Cell cell, MeshRenderer sourceRenderer)
        {
            if (cell != null && cell.room != null && cell.room.baseMat != null)
                return cell.room.baseMat;

            if (sourceRenderer == null)
                return null;

            if (sourceRenderer.sharedMaterial != null && sourceRenderer.sharedMaterial.shader != null && sourceRenderer.sharedMaterial.shader.name != PosterShaderName)
                return sourceRenderer.sharedMaterial;

            try
            {
                if (sourceRenderer.material != null && sourceRenderer.material.shader != null && sourceRenderer.material.shader.name != PosterShaderName)
                    return sourceRenderer.material;
            }
            catch
            {
            }

            return sourceRenderer.sharedMaterial;
        }

        private static Texture ResolveAtlasTexture(global::Cell cell, int roomId, MeshRenderer sourceRenderer)
        {
            EditorController editor = Singleton<EditorController>.Instance;
            if (editor != null && editor.levelData != null)
            {
                EditorRoom room = GetEditorRoomById(editor, roomId);
                Texture atlas = ResolveEditorAtlasTexture(editor, room);
                if (atlas != null)
                    return atlas;
            }

            if (cell != null && cell.room != null)
            {
                if (cell.room.textureAtlas != null)
                    return cell.room.textureAtlas;
                if (cell.room.baseMat != null)
                {
                    Texture tex = GetMaterialMainTexture(cell.room.baseMat);
                    if (tex != null)
                        return tex;
                }
            }

            return sourceRenderer != null ? GetRendererMainTexture(sourceRenderer) : null;
        }

        private static EditorRoom GetEditorRoomById(EditorController editor, int roomId)
        {
            if (editor == null || editor.levelData == null || roomId <= 0)
                return null;

            int idx = roomId - 1;
            if (editor.levelData.rooms != null && idx >= 0 && idx < editor.levelData.rooms.Count)
                return editor.levelData.rooms[idx];

            try
            {
                return editor.levelData.RoomFromId((ushort)roomId);
            }
            catch
            {
                return null;
            }
        }

        private static Texture ResolveEditorAtlasTexture(EditorController editor, EditorRoom room)
        {
            if (editor == null || room == null)
                return null;

            int roomKey = GetStableEditorRoomKey(editor, room);
            int floorId = room.floorTex != null ? room.floorTex.GetInstanceID() : 0;
            int wallId = room.wallTex != null ? room.wallTex.GetInstanceID() : 0;
            int ceilId = room.ceilTex != null ? room.ceilTex.GetInstanceID() : 0;

            EditorAtlasCacheEntry entry;
            if (editorAtlasCache.TryGetValue(roomKey, out entry))
            {
                if (entry.floorTexId == floorId && entry.wallTexId == wallId && entry.ceilTexId == ceilId && entry.atlas != null)
                    return entry.atlas;
            }

            Texture atlasTexture = InvokeEditorGenerateTextureAtlas(editor, room.floorTex, room.wallTex, room.ceilTex);
            if (atlasTexture == null)
                return null;

            entry.floorTexId = floorId;
            entry.wallTexId = wallId;
            entry.ceilTexId = ceilId;
            entry.atlas = atlasTexture;
            editorAtlasCache[roomKey] = entry;
            return atlasTexture;
        }

        private static int GetStableEditorRoomKey(EditorController editor, EditorRoom room)
        {
            if (editor != null && editor.levelData != null && editor.levelData.rooms != null)
            {
                int idx = editor.levelData.rooms.IndexOf(room);
                if (idx >= 0)
                    return idx + 1;
            }
            return room.GetHashCode();
        }

        private static Texture InvokeEditorGenerateTextureAtlas(EditorController editor, Texture2D floorTex, Texture2D wallTex, Texture2D ceilTex)
        {
            if (editor == null)
                return null;

            if (editorGenerateTextureAtlasMethod == null)
            {
                editorGenerateTextureAtlasMethod = AccessTools.Method(typeof(EditorController), "GenerateTextureAtlas", new Type[]
                {
                    typeof(Texture2D), typeof(Texture2D), typeof(Texture2D)
                });
            }

            if (editorGenerateTextureAtlasMethod == null)
                return null;

            try
            {
                return editorGenerateTextureAtlasMethod.Invoke(editor, new object[] { floorTex, wallTex, ceilTex }) as Texture;
            }
            catch
            {
                return null;
            }
        }

        private static Texture GetRendererMainTexture(Renderer renderer)
        {
            if (renderer == null)
                return null;

            try
            {
                if (renderer.material != null)
                {
                    Texture tex = GetMaterialMainTexture(renderer.material);
                    if (tex != null)
                        return tex;
                }
            }
            catch
            {
            }

            return renderer.sharedMaterial != null ? GetMaterialMainTexture(renderer.sharedMaterial) : null;
        }

        private static Texture GetMaterialMainTexture(Material material)
        {
            if (material == null)
                return null;

            if (material.HasProperty("_MainTex"))
                return material.GetTexture("_MainTex");

            return material.mainTexture;
        }

        private static void SetMaterialMainTexture(Material material, Texture texture)
        {
            if (material == null || texture == null)
                return;

            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            else
                material.mainTexture = texture;
        }

        private static Mesh GetOrCreateRaisedFloorMesh()
        {
            if (cachedRaisedFloorMesh != null)
                return cachedRaisedFloorMesh;

            Mesh mesh = new Mesh();
            mesh.name = Prefix + "RaisedFloorMesh";
            mesh.vertices = new Vector3[]
            {
                new Vector3(-HalfTile, 0f, -HalfTile),
                new Vector3(-HalfTile, 0f, HalfTile),
                new Vector3(HalfTile, 0f, -HalfTile),
                new Vector3(HalfTile, 0f, HalfTile)
            };
            mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.tangents = new Vector4[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f)
            };
            mesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();
            cachedRaisedFloorMesh = mesh;
            return mesh;
        }

        private static Mesh GetOrCreateRampMesh(Direction direction, int riseSteps)
        {
            riseSteps = Mathf.Clamp(riseSteps, 1, 10);
            int dir = DirectionToIndex(direction);
            if (dir < 0)
                return null;

            var cacheKey = (dir, riseSteps);
            Mesh cached;
            if (cachedRampMeshes.TryGetValue(cacheKey, out cached) && cached != null)
                return cached;

            float stepDepth = TileSize / riseSteps;

            List<Vector3> verts = new List<Vector3>(riseSteps * 8);
            List<Vector3> norms = new List<Vector3>(riseSteps * 8);
            List<Vector2> uvList = new List<Vector2>(riseSteps * 8);
            List<int> tris = new List<int>(riseSteps * 12);

            for (int i = 0; i < riseSteps; i++)
            {
                float y0 = i * LayerHeight;
                float y1 = (i + 1) * LayerHeight;
                float t0 = -HalfTile + i * stepDepth;
                float t1 = -HalfTile + (i + 1) * stepDepth;

                int treadBase = verts.Count;

                switch (direction)
                {
                    case Direction.North:
                        verts.Add(new Vector3(-HalfTile, y0, t0));
                        verts.Add(new Vector3(-HalfTile, y0, t1));
                        verts.Add(new Vector3(HalfTile, y0, t0));
                        verts.Add(new Vector3(HalfTile, y0, t1));

                        norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up);

                        tris.Add(treadBase + 0); tris.Add(treadBase + 1); tris.Add(treadBase + 2);
                        tris.Add(treadBase + 2); tris.Add(treadBase + 1); tris.Add(treadBase + 3);

                        int riserN = verts.Count;
                        verts.Add(new Vector3(-HalfTile, y0, t1));
                        verts.Add(new Vector3(-HalfTile, y1, t1));
                        verts.Add(new Vector3(HalfTile, y0, t1));
                        verts.Add(new Vector3(HalfTile, y1, t1));

                        norms.Add(Vector3.back); norms.Add(Vector3.back); norms.Add(Vector3.back); norms.Add(Vector3.back);

                        tris.Add(riserN + 0); tris.Add(riserN + 1); tris.Add(riserN + 2);
                        tris.Add(riserN + 2); tris.Add(riserN + 1); tris.Add(riserN + 3);
                        break;

                    case Direction.East:
                        verts.Add(new Vector3(t0, y0, -HalfTile));
                        verts.Add(new Vector3(t0, y0, HalfTile));
                        verts.Add(new Vector3(t1, y0, -HalfTile));
                        verts.Add(new Vector3(t1, y0, HalfTile));

                        norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up);

                        tris.Add(treadBase + 0); tris.Add(treadBase + 1); tris.Add(treadBase + 2);
                        tris.Add(treadBase + 2); tris.Add(treadBase + 1); tris.Add(treadBase + 3);

                        int riserE = verts.Count;
                        verts.Add(new Vector3(t1, y0, -HalfTile));
                        verts.Add(new Vector3(t1, y1, -HalfTile));
                        verts.Add(new Vector3(t1, y0, HalfTile));
                        verts.Add(new Vector3(t1, y1, HalfTile));

                        norms.Add(Vector3.left); norms.Add(Vector3.left); norms.Add(Vector3.left); norms.Add(Vector3.left);

                        tris.Add(riserE + 0); tris.Add(riserE + 1); tris.Add(riserE + 2);
                        tris.Add(riserE + 2); tris.Add(riserE + 1); tris.Add(riserE + 3);
                        break;

                    case Direction.South:
                        verts.Add(new Vector3(-HalfTile, y0, t1));
                        verts.Add(new Vector3(-HalfTile, y0, t0));
                        verts.Add(new Vector3(HalfTile, y0, t1));
                        verts.Add(new Vector3(HalfTile, y0, t0));

                        norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up);

                        tris.Add(treadBase + 0); tris.Add(treadBase + 1); tris.Add(treadBase + 2);
                        tris.Add(treadBase + 2); tris.Add(treadBase + 1); tris.Add(treadBase + 3);

                        int riserS = verts.Count;
                        verts.Add(new Vector3(-HalfTile, y0, t0));
                        verts.Add(new Vector3(-HalfTile, y1, t0));
                        verts.Add(new Vector3(HalfTile, y0, t0));
                        verts.Add(new Vector3(HalfTile, y1, t0));

                        norms.Add(Vector3.forward); norms.Add(Vector3.forward); norms.Add(Vector3.forward); norms.Add(Vector3.forward);

                        tris.Add(riserS + 0); tris.Add(riserS + 1); tris.Add(riserS + 2);
                        tris.Add(riserS + 2); tris.Add(riserS + 1); tris.Add(riserS + 3);
                        break;

                    case Direction.West:
                        verts.Add(new Vector3(t1, y0, -HalfTile));
                        verts.Add(new Vector3(t1, y0, HalfTile));
                        verts.Add(new Vector3(t0, y0, -HalfTile));
                        verts.Add(new Vector3(t0, y0, HalfTile));

                        norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up); norms.Add(Vector3.up);

                        tris.Add(treadBase + 0); tris.Add(treadBase + 1); tris.Add(treadBase + 2);
                        tris.Add(treadBase + 2); tris.Add(treadBase + 1); tris.Add(treadBase + 3);

                        int riserW = verts.Count;
                        verts.Add(new Vector3(t0, y0, -HalfTile));
                        verts.Add(new Vector3(t0, y1, -HalfTile));
                        verts.Add(new Vector3(t0, y0, HalfTile));
                        verts.Add(new Vector3(t0, y1, HalfTile));

                        norms.Add(Vector3.right); norms.Add(Vector3.right); norms.Add(Vector3.right); norms.Add(Vector3.right);

                        tris.Add(riserW + 0); tris.Add(riserW + 1); tris.Add(riserW + 2);
                        tris.Add(riserW + 2); tris.Add(riserW + 1); tris.Add(riserW + 3);
                        break;
                }

                // Tread UVs: floor region (0,0)-(0.5,0.5)
                uvList.Add(new Vector2(0f, 0f));
                uvList.Add(new Vector2(0f, 0.5f));
                uvList.Add(new Vector2(0.5f, 0f));
                uvList.Add(new Vector2(0.5f, 0.5f));

                // Riser UVs: wall region (0.5,0.5)-(1,1)
                uvList.Add(new Vector2(0.5f, 0.5f));
                uvList.Add(new Vector2(0.5f, 1f));
                uvList.Add(new Vector2(1f, 0.5f));
                uvList.Add(new Vector2(1f, 1f));
            }

            Mesh mesh = new Mesh();
            mesh.name = Prefix + "RampStair_" + direction + "_S" + riseSteps;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvList);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            cachedRampMeshes[cacheKey] = mesh;
            return mesh;
        }

        private static Mesh GetOrCreateWallMesh(Direction direction)
        {
            int dir = DirectionToIndex(direction);
            if (dir < 0 || dir >= cachedWallMeshes.Length)
                return null;

            if (cachedWallMeshes[dir] != null)
                return cachedWallMeshes[dir];

            Vector3 inwardNormal;
            switch (direction)
            {
                case Direction.North: inwardNormal = Vector3.back; break;
                case Direction.East: inwardNormal = Vector3.left; break;
                case Direction.South: inwardNormal = Vector3.forward; break;
                case Direction.West: inwardNormal = Vector3.right; break;
                default: inwardNormal = Vector3.forward; break;
            }

            Vector3 right = Vector3.Cross(Vector3.up, inwardNormal).normalized * HalfTile;
            Vector3 up = Vector3.up * HalfTile;

            Mesh mesh = new Mesh();
            mesh.name = Prefix + "WallMesh_" + direction;
            mesh.vertices = new Vector3[]
            {
                -right - up,
                right - up,
                -right + up,
                right + up
            };
            mesh.normals = new Vector3[] { inwardNormal, inwardNormal, inwardNormal, inwardNormal };
            mesh.tangents = new Vector4[]
            {
                new Vector4(right.normalized.x, right.normalized.y, right.normalized.z, 1f),
                new Vector4(right.normalized.x, right.normalized.y, right.normalized.z, 1f),
                new Vector4(right.normalized.x, right.normalized.y, right.normalized.z, 1f),
                new Vector4(right.normalized.x, right.normalized.y, right.normalized.z, 1f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();
            cachedWallMeshes[dir] = mesh;
            return mesh;
        }

        private static int DirectionToIndex(Direction direction)
        {
            switch (direction)
            {
                case Direction.North: return 0;
                case Direction.East: return 1;
                case Direction.South: return 2;
                case Direction.West: return 3;
                default: return -1;
            }
        }

        private static bool TryGetCell(int x, int z, out global::Cell cell)
        {
            cell = null;

            EditorController editor = Singleton<EditorController>.Instance;
            if (editor != null)
            {
                EnvironmentController workerEc = GetWorkerEnvironment(editor);
                if (workerEc != null && workerEc.cells != null)
                    return TryGetCell(workerEc, x, z, out cell);
            }

            EnvironmentController runtimeEc = Singleton<BaseGameManager>.Instance != null ? Singleton<BaseGameManager>.Instance.Ec : null;
            if (runtimeEc != null && runtimeEc.cells != null)
                return TryGetCell(runtimeEc, x, z, out cell);

            return false;
        }

        private static bool TryGetCell(EnvironmentController ec, int x, int z, out global::Cell cell)
        {
            cell = null;
            if (ec == null || ec.cells == null)
                return false;

            int width = ec.cells.GetLength(0);
            int height = ec.cells.GetLength(1);
            if (x < 0 || z < 0 || x >= width || z >= height)
                return false;

            cell = ec.cells[x, z];
            return cell != null;
        }

        private static EnvironmentController GetWorkerEnvironment(EditorController editor)
        {
            if (editor == null)
                return null;

            var workerField = AccessTools.Field(typeof(EditorController), "workerEc");
            if (workerField == null)
                return null;

            return workerField.GetValue(editor) as EnvironmentController;
        }

        public static void RefreshCellAndNeighbors(int x, int z)
        {
            RefreshSingleCell(x, z);
            RefreshSingleCell(x + 1, z);
            RefreshSingleCell(x - 1, z);
            RefreshSingleCell(x, z + 1);
            RefreshSingleCell(x, z - 1);
        }

        private static void RefreshSingleCell(int x, int z)
        {
            global::Cell cell;
            if (!TryGetCell(x, z, out cell) || cell == null || cell.Tile == null)
                return;

            ProcessCell(cell);
        }

        public static void RefreshAllEditorCells(EditorController editor)
        {
            EnvironmentController ec = GetWorkerEnvironment(editor);
            if (ec == null || ec.cells == null)
                return;

            int width = ec.cells.GetLength(0);
            int height = ec.cells.GetLength(1);
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    RefreshSingleCell(x, z);
        }

        public static void RefreshAllRuntimeCells(EnvironmentController ec)
        {
            if (ec == null || ec.cells == null)
                return;

            int width = ec.cells.GetLength(0);
            int height = ec.cells.GetLength(1);
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    RefreshSingleCell(x, z);
        }

        private static void ClearGeneratedChildren(Transform parent)
        {
            if (parent == null)
                return;

            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name.StartsWith(Prefix))
                    toDestroy.Add(child.gameObject);
            }

            for (int i = 0; i < toDestroy.Count; i++)
                SafeDestroy(toDestroy[i]);
        }

        private static GameObject CreateOrReuseChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
                component = go.AddComponent<T>();
            return component;
        }

        private static void RemoveComponentIfExists<T>(GameObject go) where T : Component
        {
            if (go == null)
                return;

            T component = go.GetComponent<T>();
            if (component != null)
                SafeDestroy(component);
        }

        private static bool ShouldGeneratePhysicalColliders()
        {
            return Singleton<EditorController>.Instance == null;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }

        #region Save / Load

        [Serializable]
        private class SaveContainer
        {
            public List<RaisedCellEntry> raisedCellEntries = new List<RaisedCellEntry>();
            public List<RampEntry> rampEntries = new List<RampEntry>();
        }

        [Serializable]
        private class RaisedCellEntry
        {
            public int x;
            public int z;
            public int steps;
        }

        [Serializable]
        private class RampEntry
        {
            public int x;
            public int z;
            public int direction;
            public int riseSteps;
            public int length;
        }

        public static string SerializeData()
        {
            SaveContainer container = new SaveContainer();

            foreach (var kv in raisedCells)
            {
                container.raisedCellEntries.Add(new RaisedCellEntry
                {
                    x = kv.Key.x,
                    z = kv.Key.z,
                    steps = kv.Value
                });
            }

            foreach (var kv in ramps)
            {
                container.rampEntries.Add(new RampEntry
                {
                    x = kv.Key.x,
                    z = kv.Key.z,
                    direction = DirectionToIndex(kv.Value.upDirection),
                    riseSteps = kv.Value.riseSteps,
                    length = kv.Value.length
                });
            }

            return JsonUtility.ToJson(container, false);
        }

        public static void DeserializeData(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            raisedCells.Clear();
            ramps.Clear();

            SaveContainer container = JsonUtility.FromJson<SaveContainer>(json);
            if (container == null)
                return;

            if (container.raisedCellEntries != null)
            {
                for (int i = 0; i < container.raisedCellEntries.Count; i++)
                {
                    var entry = container.raisedCellEntries[i];
                    raisedCells[(entry.x, entry.z)] = Mathf.Clamp(entry.steps, 0, 1);
                }
            }

            if (container.rampEntries != null)
            {
                for (int i = 0; i < container.rampEntries.Count; i++)
                {
                    var entry = container.rampEntries[i];
                    Direction dir = IndexToDirection(entry.direction);
                    if (dir == Direction.North || dir == Direction.East || dir == Direction.South || dir == Direction.West)
                    {
                        ramps[(entry.x, entry.z)] = new KnoxumRampData
                        {
                            upDirection = dir,
                            riseSteps = Mathf.Clamp(entry.riseSteps, 1, 10),
                            length = Mathf.Clamp(entry.length, 1, 10)
                        };
                    }
                }
            }
        }

        private static Direction IndexToDirection(int index)
        {
            switch (index)
            {
                case 0: return Direction.North;
                case 1: return Direction.East;
                case 2: return Direction.South;
                case 3: return Direction.West;
                default: return Direction.North;
            }
        }

        #endregion
    }

    [HarmonyPatch(typeof(global::Cell), "LoadTile")]
    internal static class KnoxumHighWallsObjects_CellLoadTilePatch
    {
        private static void Postfix(global::Cell __instance)
        {
            if (__instance == null || __instance.Tile == null)
                return;

            HighWallsObjects.ProcessCell(__instance);
        }
    }

    [HarmonyPatch(typeof(global::Cell), "SetShape")]
    internal static class KnoxumHighWallsObjects_CellSetShapePatch
    {
        private static void Postfix(global::Cell __instance)
        {
            if (__instance == null || __instance.Tile == null)
                return;

            HighWallsObjects.ProcessCell(__instance);
        }
    }

    [HarmonyPatch(typeof(EditorController), "RefreshCells")]
    internal static class KnoxumHighWallsObjects_EditorRefreshPatch
    {
        private static void Postfix(EditorController __instance)
        {
            if (__instance == null)
                return;

            HighWallsObjects.RefreshAllEditorCells(__instance);
            HighWallsObjects.RefreshAllBoundObjectsInScene();
        }
    }
}
