using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.SettingsUI;

namespace KnoxumPLSExtension.Features
{
    internal sealed class KnoxumLightBaseline : MonoBehaviour
    {
        public bool initialized;
        public float baseIntensity;
        public float baseRange;
    }

    public static class HighWallsGenerator
    {
        private const float TileSize = 10f;
        private const float HalfTile = 5f;
        private const float LayerHeight = 10f;
        private const float LightCeilingLocalY = 9.85f;
        private const float SurfaceInset = 0.01f;
        private const float WallColliderThickness = 0.2f;
        private const float FloorColliderThickness = 0.2f;

        private static Mesh cachedFloorMesh;
        private static readonly Mesh[] cachedWallMeshes = new Mesh[4];

        private static readonly Dictionary<int, Dictionary<int, Texture>> roomLayerWallTextures = new Dictionary<int, Dictionary<int, Texture>>();

        private struct RendererState
        {
            public Material effectiveMaterial;
            public Material sharedMaterial;
            public MaterialPropertyBlock propertyBlock;
            public ShadowCastingMode shadowCastingMode;
            public bool receiveShadows;
            public LightProbeUsage lightProbeUsage;
            public ReflectionProbeUsage reflectionProbeUsage;
        }

        public static void ProcessCell3D(global::Cell cell, int roomId)
        {
            if (cell == null || cell.Tile == null || roomId <= 0)
                return;

            Tile tile = cell.Tile;
            MeshRenderer sourceRenderer = GetTileRenderer(tile);
            MeshFilter sourceFilter = GetTileMeshFilter(tile);
            if (sourceRenderer == null || sourceFilter == null)
                return;

            int yOffset = HighWallsController.GetRoomYOffset(roomId);
            int targetHeight = Mathf.Max(1, HighWallsController.GetRoomHeight(roomId));

            float ceilingLocalY = (yOffset + targetHeight - 1) * LayerHeight;
            Vector3 tilePos = tile.transform.localPosition;
            tile.transform.localPosition = new Vector3(tilePos.x, ceilingLocalY, tilePos.z);

            RemoveTopTileFloor(sourceFilter, targetHeight);

            RendererState state = CaptureRendererState(sourceRenderer);
            RebuildBottomFloor(tile.transform, state, targetHeight);
            RebuildStackedWalls(cell, tile.transform, state, targetHeight, roomId);
            FixLightsOnly(tile, targetHeight);
        }

        public static void FixLightsOnly(global::Cell cell, int roomId)
        {
            if (cell == null || cell.Tile == null || roomId <= 0)
                return;

            FixLightsOnly(cell.Tile, Mathf.Max(1, HighWallsController.GetRoomHeight(roomId)));
        }

        private static void FixLightsOnly(Tile tile, int targetHeight)
        {
            if (tile == null)
                return;

            LightController[] controllers = tile.GetComponentsInChildren<LightController>(true);
            if (controllers != null && controllers.Length > 0)
            {
                for (int i = 0; i < controllers.Length; i++)
                {
                    LightController controller = controllers[i];
                    if (controller == null)
                        continue;

                    MagnetLightControllerToCeiling(tile.transform, controller.transform);

                    Light[] lights = controller.GetComponentsInChildren<Light>(true);
                    for (int j = 0; j < lights.Length; j++)
                    {
                        ApplyLightFixSettings(lights[j], targetHeight);
                    }

                    MeshRenderer[] lampRenderers = controller.GetComponentsInChildren<MeshRenderer>(true);
                    for (int j = 0; j < lampRenderers.Length; j++)
                    {
                        DisableProbeInfluence(lampRenderers[j]);
                    }
                }
                return;
            }

            Light[] fallbackLights = tile.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < fallbackLights.Length; i++)
            {
                Light light = fallbackLights[i];
                if (light == null)
                    continue;

                Vector3 localPos = light.transform.localPosition;
                light.transform.localPosition = new Vector3(localPos.x, LightCeilingLocalY, localPos.z);
                ApplyLightFixSettings(light, targetHeight);
            }
        }

        private static void MagnetLightControllerToCeiling(Transform tileTransform, Transform controllerTransform)
        {
            if (tileTransform == null || controllerTransform == null)
                return;

            float currentTopLocalY;
            if (!TryGetHighestLocalY(tileTransform, controllerTransform, out currentTopLocalY))
            {
                Vector3 fallback = controllerTransform.localPosition;
                controllerTransform.localPosition = new Vector3(fallback.x, LightCeilingLocalY, fallback.z);
                return;
            }

            float deltaY = LightCeilingLocalY - currentTopLocalY;
            Vector3 localPos = controllerTransform.localPosition;
            controllerTransform.localPosition = new Vector3(localPos.x, localPos.y + deltaY, localPos.z);
        }

        private static bool TryGetHighestLocalY(Transform tileTransform, Transform root, out float highestLocalY)
        {
            highestLocalY = float.NegativeInfinity;
            bool found = false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                float rendererTopLocalY = tileTransform.InverseTransformPoint(renderer.bounds.max).y;
                if (!found || rendererTopLocalY > highestLocalY)
                {
                    highestLocalY = rendererTopLocalY;
                    found = true;
                }
            }

            if (found)
                return true;

            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null)
                    continue;

                float lightLocalY = tileTransform.InverseTransformPoint(light.transform.position).y;
                if (!found || lightLocalY > highestLocalY)
                {
                    highestLocalY = lightLocalY;
                    found = true;
                }
            }

            return found;
        }

        private static void ApplyLightFixSettings(Light light, int targetHeight)
        {
            if (light == null)
                return;

            KnoxumLightBaseline baseline = light.GetComponent<KnoxumLightBaseline>();
            if (baseline == null)
                baseline = light.gameObject.AddComponent<KnoxumLightBaseline>();

            if (!baseline.initialized)
            {
                baseline.initialized = true;
                baseline.baseIntensity = light.intensity;
                baseline.baseRange = light.range;
            }

            int safeHeight = Mathf.Max(1, targetHeight);
            light.type = LightType.Point;
            light.range = Mathf.Max(baseline.baseRange, 20f) * safeHeight;
            light.intensity = Mathf.Max(baseline.baseIntensity, 3f) * Mathf.Sqrt(safeHeight);
            light.bounceIntensity = 1f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static void DisableProbeInfluence(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static MeshRenderer GetTileRenderer(Tile tile)
        {
            if (tile == null)
                return null;

            if (tile.MeshRenderer != null)
                return tile.MeshRenderer;

            return tile.GetComponent<MeshRenderer>() ?? tile.GetComponentInChildren<MeshRenderer>(true);
        }

        private static MeshFilter GetTileMeshFilter(Tile tile)
        {
            if (tile == null)
                return null;

            if (tile.MeshFilter != null)
                return tile.MeshFilter;

            return tile.GetComponent<MeshFilter>() ?? tile.GetComponentInChildren<MeshFilter>(true);
        }

        private static RendererState CaptureRendererState(MeshRenderer sourceRenderer)
        {
            RendererState state = new RendererState();
            state.effectiveMaterial = ResolveEffectiveMaterial(sourceRenderer);
            state.sharedMaterial = sourceRenderer.sharedMaterial;
            state.propertyBlock = new MaterialPropertyBlock();
            sourceRenderer.GetPropertyBlock(state.propertyBlock);
            state.shadowCastingMode = sourceRenderer.shadowCastingMode;
            state.receiveShadows = sourceRenderer.receiveShadows;
            state.lightProbeUsage = sourceRenderer.lightProbeUsage;
            state.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            return state;
        }

        private static void ApplyRendererState(MeshRenderer targetRenderer, RendererState state, ShadowCastingMode overrideShadowMode)
        {
            if (targetRenderer == null)
                return;

            Material effectiveMaterial = state.effectiveMaterial != null ? state.effectiveMaterial : state.sharedMaterial;
            if (effectiveMaterial != null)
                targetRenderer.sharedMaterial = effectiveMaterial;

            targetRenderer.SetPropertyBlock(state.propertyBlock);
            targetRenderer.shadowCastingMode = overrideShadowMode;
            targetRenderer.receiveShadows = state.receiveShadows;
            targetRenderer.lightProbeUsage = state.lightProbeUsage;
            targetRenderer.reflectionProbeUsage = state.reflectionProbeUsage;
        }

        private static Material ResolveEffectiveMaterial(MeshRenderer sourceRenderer)
        {
            if (sourceRenderer == null)
                return null;

            Material materialInstance = null;
            try
            {
                materialInstance = sourceRenderer.material;
            }
            catch
            {
            }

            if (materialInstance != null)
            {
                Texture mainTexture = null;
                if (materialInstance.HasProperty("_MainTex"))
                    mainTexture = materialInstance.GetTexture("_MainTex");
                else
                    mainTexture = materialInstance.mainTexture;

                if (mainTexture != null)
                    return materialInstance;
            }

            return sourceRenderer.sharedMaterial;
        }

        private static void RemoveTopTileFloor(MeshFilter meshFilter, int targetHeight)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null || targetHeight <= 1)
                return;

            Mesh meshCopy = Object.Instantiate(meshFilter.sharedMesh);
            Vector3[] vertices = meshCopy.vertices;
            Vector3[] normals = meshCopy.normals;
            int[] triangles = meshCopy.triangles;

            List<int> newTriangles = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];

                bool isAtFloorHeight = vertices[i1].y <= 0.5f && vertices[i2].y <= 0.5f && vertices[i3].y <= 0.5f;
                bool isFacingUp = false;
                if (normals != null && normals.Length > i3)
                {
                    Vector3 avgNormal = (normals[i1] + normals[i2] + normals[i3]) / 3f;
                    isFacingUp = avgNormal.y > 0.5f;
                }

                if (!(isAtFloorHeight && isFacingUp))
                {
                    newTriangles.Add(i1);
                    newTriangles.Add(i2);
                    newTriangles.Add(i3);
                }
            }

            meshCopy.triangles = newTriangles.ToArray();
            meshCopy.RecalculateBounds();
            meshFilter.mesh = meshCopy;
        }

        private static void RebuildBottomFloor(Transform tileTransform, RendererState state, int targetHeight)
        {
            Transform existingFloor = tileTransform.Find("Knoxum_Floor");
            if (targetHeight <= 1)
            {
                if (existingFloor != null)
                    SafeDestroy(existingFloor.gameObject);
                return;
            }

            GameObject floorObject = existingFloor != null ? existingFloor.gameObject : CreateGeneratedObject("Knoxum_Floor", tileTransform);
            floorObject.transform.localPosition = new Vector3(0f, -(targetHeight - 1) * LayerHeight, 0f);
            floorObject.transform.localRotation = Quaternion.identity;
            floorObject.transform.localScale = Vector3.one;

            MeshFilter meshFilter = EnsureComponent<MeshFilter>(floorObject);
            MeshRenderer meshRenderer = EnsureComponent<MeshRenderer>(floorObject);

            meshFilter.sharedMesh = GetOrCreateFloorMesh();
            ApplyRendererState(meshRenderer, state, ShadowCastingMode.On);

            if (ShouldGeneratePhysicalColliders())
                EnsureFloorCollider(floorObject);
            else
                RemoveComponentIfExists<BoxCollider>(floorObject);
        }

        private static void RebuildStackedWalls(global::Cell cell, Transform tileTransform, RendererState state, int targetHeight, int currentRoomId)
        {
            List<Transform> existingGenerated = new List<Transform>();
            for (int i = 0; i < tileTransform.childCount; i++)
            {
                Transform child = tileTransform.GetChild(i);
                if (child != null && child.name.StartsWith("Knoxum_Wall_"))
                    existingGenerated.Add(child);
            }

            if (targetHeight <= 1)
            {
                for (int i = 0; i < existingGenerated.Count; i++)
                    SafeDestroy(existingGenerated[i].gameObject);
                return;
            }

            bool[] hasWall = GetWallDirections(cell, currentRoomId);
            HashSet<string> requiredNames = new HashSet<string>();

            for (int dir = 0; dir < 4; dir++)
            {
                if (!hasWall[dir])
                    continue;

                for (int layer = 0; layer < targetHeight - 1; layer++)
                {
                    string objectName = "Knoxum_Wall_L" + layer + "_D" + dir;
                    requiredNames.Add(objectName);

                    Transform existing = tileTransform.Find(objectName);
                    GameObject wallObject = existing != null ? existing.gameObject : CreateGeneratedObject(objectName, tileTransform);
                    wallObject.transform.localPosition = GetWallLocalCenter(dir, layer);
                    wallObject.transform.localRotation = Quaternion.identity;
                    wallObject.transform.localScale = Vector3.one;

                    MeshFilter meshFilter = EnsureComponent<MeshFilter>(wallObject);
                    MeshRenderer meshRenderer = EnsureComponent<MeshRenderer>(wallObject);

                    meshFilter.sharedMesh = GetOrCreateWallMesh(dir);
                    ApplyRendererState(meshRenderer, state, ShadowCastingMode.Off);

                    Texture layerTex = GetRoomLayerWallTexture(currentRoomId, layer);
                    if (layerTex != null)
                    {
                        MaterialPropertyBlock layerBlock = new MaterialPropertyBlock();
                        meshRenderer.GetPropertyBlock(layerBlock);
                        layerBlock.SetTexture("_MainTex", layerTex);
                        meshRenderer.SetPropertyBlock(layerBlock);
                    }

                    if (ShouldGeneratePhysicalColliders())
                        EnsureWallCollider(wallObject, dir);
                    else
                        RemoveComponentIfExists<BoxCollider>(wallObject);
                }
            }

            for (int i = 0; i < existingGenerated.Count; i++)
            {
                Transform child = existingGenerated[i];
                if (child != null && !requiredNames.Contains(child.name))
                    SafeDestroy(child.gameObject);
            }
        }

        private static Vector3 GetWallLocalCenter(int dir, int layer)
        {
            float localY = -((layer + 1) * LayerHeight) + HalfTile;

            switch (dir)
            {
                case 0: return new Vector3(0f, localY, HalfTile - SurfaceInset);      // North edge, inward normal = south
                case 1: return new Vector3(HalfTile - SurfaceInset, localY, 0f);      // East edge, inward normal = west
                case 2: return new Vector3(0f, localY, -HalfTile + SurfaceInset);     // South edge, inward normal = north
                case 3: return new Vector3(-HalfTile + SurfaceInset, localY, 0f);     // West edge, inward normal = east
                default: return Vector3.zero;
            }
        }

        private static Mesh GetOrCreateFloorMesh()
        {
            if (cachedFloorMesh != null)
                return cachedFloorMesh;

            Mesh mesh = new Mesh();
            mesh.name = "Knoxum_FloorMesh";
            mesh.vertices = new Vector3[]
            {
                new Vector3(-HalfTile, 0f, -HalfTile),
                new Vector3(-HalfTile, 0f, HalfTile),
                new Vector3(HalfTile, 0f, -HalfTile),
                new Vector3(HalfTile, 0f, HalfTile)
            };
            mesh.normals = new Vector3[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };
            mesh.tangents = new Vector4[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, 0.5f),
                new Vector2(0.5f, 0.0f),
                new Vector2(0.5f, 0.5f)
            };
            mesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();

            cachedFloorMesh = mesh;
            return cachedFloorMesh;
        }

        private static Mesh GetOrCreateWallMesh(int dir)
        {
            if (dir < 0 || dir >= cachedWallMeshes.Length)
                return null;

            if (cachedWallMeshes[dir] != null)
                return cachedWallMeshes[dir];

            Vector3 inwardNormal;
            switch (dir)
            {
                case 0: inwardNormal = Vector3.back; break;   // north wall faces south (into the room)
                case 1: inwardNormal = Vector3.left; break;   // east wall faces west
                case 2: inwardNormal = Vector3.forward; break;// south wall faces north
                case 3: inwardNormal = Vector3.right; break;  // west wall faces east
                default: inwardNormal = Vector3.forward; break;
            }

            Vector3 right = Vector3.Cross(Vector3.up, inwardNormal).normalized * HalfTile;
            Vector3 up = Vector3.up * HalfTile;

            Mesh mesh = new Mesh();
            mesh.name = "Knoxum_WallMesh_D" + dir;
            mesh.vertices = new Vector3[]
            {
                -right - up,
                right - up,
                -right + up,
                right + up
            };
            mesh.normals = new Vector3[]
            {
                inwardNormal,
                inwardNormal,
                inwardNormal,
                inwardNormal
            };
            Vector4 tangent = new Vector4(right.normalized.x, right.normalized.y, right.normalized.z, 1f);
            mesh.tangents = new Vector4[] { tangent, tangent, tangent, tangent };
            mesh.uv = new Vector2[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(1.0f, 0.5f),
                new Vector2(0.5f, 1.0f),
                new Vector2(1.0f, 1.0f)
            };
            mesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();

            cachedWallMeshes[dir] = mesh;
            return cachedWallMeshes[dir];
        }

        private static GameObject CreateGeneratedObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static bool ShouldGeneratePhysicalColliders()
        {
            return Singleton<EditorController>.Instance == null;
        }

        private static void EnsureFloorCollider(GameObject floorObject)
        {
            if (floorObject == null)
                return;

            BoxCollider collider = EnsureComponent<BoxCollider>(floorObject);
            collider.isTrigger = false;
            collider.center = new Vector3(0f, -FloorColliderThickness * 0.5f, 0f);
            collider.size = new Vector3(TileSize, FloorColliderThickness, TileSize);
        }

        private static void EnsureWallCollider(GameObject wallObject, int dir)
        {
            if (wallObject == null)
                return;

            BoxCollider collider = EnsureComponent<BoxCollider>(wallObject);
            collider.isTrigger = false;
            collider.center = Vector3.zero;

            if (dir == 0 || dir == 2)
                collider.size = new Vector3(TileSize, LayerHeight, WallColliderThickness);
            else
                collider.size = new Vector3(WallColliderThickness, LayerHeight, TileSize);
        }

        private static void RemoveComponentIfExists<T>(GameObject gameObject) where T : Component
        {
            if (gameObject == null)
                return;

            T component = gameObject.GetComponent<T>();
            if (component != null)
                SafeDestroy(component);
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();
            return component;
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        private static bool[] GetWallDirections(global::Cell cell, int currentRoomId)
        {
            bool[] walls = new bool[] { true, true, true, true };
            int x = cell.position.x;
            int z = cell.position.z;

            var editorLevel = Singleton<EditorController>.Instance != null ? Singleton<EditorController>.Instance.levelData : null;
            if (editorLevel != null)
            {
                var n = editorLevel.GetCellSafe(x, z + 1);
                var e = editorLevel.GetCellSafe(x + 1, z);
                var s = editorLevel.GetCellSafe(x, z - 1);
                var w = editorLevel.GetCellSafe(x - 1, z);

                int currentYOffset = HighWallsController.GetRoomYOffset(currentRoomId);
                int currentHeight = HighWallsController.GetRoomHeight(currentRoomId);

                walls[0] = ShouldBuildWall(n, currentRoomId, currentYOffset, currentHeight);
                walls[1] = ShouldBuildWall(e, currentRoomId, currentYOffset, currentHeight);
                walls[2] = ShouldBuildWall(s, currentRoomId, currentYOffset, currentHeight);
                walls[3] = ShouldBuildWall(w, currentRoomId, currentYOffset, currentHeight);
                return walls;
            }

            var ec = Singleton<BaseGameManager>.Instance != null ? Singleton<BaseGameManager>.Instance.Ec : null;
            if (ec != null && ec.cells != null)
            {
                var n = GetCellRuntime(ec, x, z + 1);
                var e = GetCellRuntime(ec, x + 1, z);
                var s = GetCellRuntime(ec, x, z - 1);
                var w = GetCellRuntime(ec, x - 1, z);

                walls[0] = ShouldBuildWallRuntime(cell, n);
                walls[1] = ShouldBuildWallRuntime(cell, e);
                walls[2] = ShouldBuildWallRuntime(cell, s);
                walls[3] = ShouldBuildWallRuntime(cell, w);
            }

            return walls;
        }

        private static bool ShouldBuildWall(PlusStudioLevelFormat.Cell neighbor, int currentRoomId, int currentYOffset, int currentHeight)
        {
            if (neighbor == null)
                return true;
            if (neighbor.roomId != currentRoomId)
                return true;

            int neighborYOffset = HighWallsController.GetRoomYOffset(neighbor.roomId);
            int neighborHeight = HighWallsController.GetRoomHeight(neighbor.roomId);
            return currentYOffset != neighborYOffset || currentHeight != neighborHeight;
        }

        private static bool ShouldBuildWallRuntime(global::Cell currentCell, global::Cell neighbor)
        {
            if (neighbor == null)
                return true;

            int currentRoomId = GetRoomIdForCell(currentCell);
            int neighborRoomId = GetRoomIdForCell(neighbor);

            if (currentRoomId <= 0 || neighborRoomId <= 0)
                return true;
            if (currentRoomId != neighborRoomId)
                return true;

            return HighWallsController.GetRoomHeight(currentRoomId) != HighWallsController.GetRoomHeight(neighborRoomId)
                || HighWallsController.GetRoomYOffset(currentRoomId) != HighWallsController.GetRoomYOffset(neighborRoomId);
        }

        private static global::Cell GetCellRuntime(EnvironmentController ec, int x, int z)
        {
            if (ec == null || ec.cells == null)
                return null;

            int width = ec.cells.GetLength(0);
            int height = ec.cells.GetLength(1);
            if (x < 0 || z < 0 || x >= width || z >= height)
                return null;

            return ec.cells[x, z];
        }

        public static void RefreshEditorRoomGeneratedVisuals(EditorRoom room)
        {
            if (room == null)
                return;

            EditorController editor = Singleton<EditorController>.Instance;
            if (editor == null || editor.levelData == null)
                return;

            EnvironmentController workerEc = null;
            var workerField = AccessTools.Field(typeof(EditorController), "workerEc");
            if (workerField != null)
                workerEc = workerField.GetValue(editor) as EnvironmentController;

            if (workerEc == null || workerEc.cells == null)
                return;

            int roomId = editor.levelData.rooms.IndexOf(room) + 1;
            if (roomId <= 0)
                return;

            int width = workerEc.cells.GetLength(0);
            int height = workerEc.cells.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    var editorCell = editor.levelData.GetCellSafe(x, z);
                    if (editorCell == null || editorCell.roomId != roomId)
                        continue;

                    global::Cell workerCell = workerEc.cells[x, z];
                    if (workerCell == null || workerCell.Tile == null)
                        continue;

                    ProcessCell3D(workerCell, roomId);
                }
            }
        }

        public static int GetRoomIdForCell(global::Cell cell)
        {
            if (cell == null)
                return -1;

            var editorCtrl = Singleton<EditorController>.Instance;
            if (editorCtrl != null && editorCtrl.levelData != null)
            {
                var area = editorCtrl.levelData.GetCellSafe(cell.position.x, cell.position.z);
                if (area != null && area.roomId != 0)
                    return area.roomId;
            }

            var map = HighWallsController.runtimeCellRoomIdMap;
            if (map != null && map.Count > 0)
            {
                ushort mappedId;
                if (map.TryGetValue((cell.position.x, cell.position.z), out mappedId) && mappedId != 0)
                    return mappedId;
            }

            if (cell.room != null)
            {
                var ec = cell.room.ec;
                if (ec != null && ec.rooms != null)
                {
                    int idx = ec.rooms.IndexOf(cell.room);
                    if (idx >= 0)
                        return idx + 1;
                }
            }

            return -1;
        }

        public static void SetRoomLayerWallTexture(int roomId, int layer, Texture tex)
        {
            if (roomId <= 0)
                return;

            Dictionary<int, Texture> layerMap;
            if (!roomLayerWallTextures.TryGetValue(roomId, out layerMap))
            {
                layerMap = new Dictionary<int, Texture>();
                roomLayerWallTextures[roomId] = layerMap;
            }

            if (tex != null)
                layerMap[layer] = tex;
            else
                layerMap.Remove(layer);
        }

        public static Texture GetRoomLayerWallTexture(int roomId, int layer)
        {
            if (roomId <= 0)
                return null;

            Dictionary<int, Texture> layerMap;
            if (!roomLayerWallTextures.TryGetValue(roomId, out layerMap))
                return null;

            Texture tex;
            return layerMap.TryGetValue(layer, out tex) ? tex : null;
        }

        public static void ClearRoomLayerWallTextures(int roomId)
        {
            roomLayerWallTextures.Remove(roomId);
        }
    }

    [HarmonyPatch(typeof(global::Cell), "AssignLightController")]
    internal static class Cell_AssignLightController_KnoxumHighWallsPatch
    {
        private static void Postfix(global::Cell __instance)
        {
            if (__instance == null || __instance.Tile == null)
                return;

            int roomId = HighWallsGenerator.GetRoomIdForCell(__instance);
            if (roomId <= 0)
                return;

            if (HighWallsController.GetRoomHeight(roomId) <= 1 && HighWallsController.GetRoomYOffset(roomId) <= 0)
                return;

            HighWallsGenerator.FixLightsOnly(__instance, roomId);
        }
    }

    [HarmonyPatch(typeof(RoomSettingsExchangeHandler), "UpdateTextures")]
    internal static class KnoxumHighWalls_RoomTextureRefreshPatch
    {
        private static void Postfix(RoomSettingsExchangeHandler __instance)
        {
            if (__instance == null)
                return;

            var roomField = AccessTools.Field(typeof(RoomSettingsExchangeHandler), "myRoom");
            if (roomField == null)
                return;

            EditorRoom room = roomField.GetValue(__instance) as EditorRoom;
            if (room == null)
                return;

            HighWallsGenerator.RefreshEditorRoomGeneratedVisuals(room);
        }
    }
}
