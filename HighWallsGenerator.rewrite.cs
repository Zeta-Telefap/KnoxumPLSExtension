using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
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

    internal sealed class KnoxumTileMeshBaseline : MonoBehaviour
    {
        public Mesh originalMesh;
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
        private const string ModifiedTileMeshName = "Knoxum_ModifiedTileMesh";
        private const string PosterShaderName = "Shader Graphs/TileStandardWPoster";

        private static readonly MaterialPropertyBlock EmptyPropertyBlock = new MaterialPropertyBlock();
        private static readonly Dictionary<int, EditorAtlasCacheEntry> editorAtlasCache = new Dictionary<int, EditorAtlasCacheEntry>();

        private static Mesh cachedFloorMesh;
        private static readonly Mesh[] cachedWallMeshes = new Mesh[4];
        private static MethodInfo editorGenerateTextureAtlasMethod;

        private static readonly Dictionary<int, Dictionary<int, Texture>> roomLayerWallTextures = new Dictionary<int, Dictionary<int, Texture>>();

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

        public static void ProcessCell3D(global::Cell cell, int roomId)
        {
            if (cell == null || cell.Tile == null || roomId <= 0)
                return;

            Tile tile = cell.Tile;
            MeshRenderer sourceRenderer = GetTileRenderer(tile);
            MeshFilter sourceFilter = GetTileMeshFilter(tile);
            if (sourceRenderer == null || sourceFilter == null)
                return;

            Mesh originalTileMesh = GetOrCaptureOriginalTileMesh(tile, sourceFilter);
            if (originalTileMesh != null && sourceFilter.sharedMesh != originalTileMesh)
                sourceFilter.sharedMesh = originalTileMesh;

            SurfaceMaterialState materialState = ResolveSurfaceMaterialState(cell, roomId, sourceRenderer);

            int yOffset = HighWallsController.GetRoomYOffset(roomId);
            int targetHeight = Mathf.Max(1, HighWallsController.GetRoomHeight(roomId));

            float ceilingLocalY = (yOffset + targetHeight - 1) * LayerHeight;
            Vector3 tilePos = tile.transform.localPosition;
            tile.transform.localPosition = new Vector3(tilePos.x, ceilingLocalY, tilePos.z);

            if (targetHeight > 1)
                RemoveTopTileFloor(sourceFilter);

            RebuildBottomFloor(tile.transform, materialState, targetHeight);
            RebuildStackedWalls(cell, tile.transform, materialState, targetHeight, roomId);
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
                        ApplyLightFixSettings(lights[j], targetHeight);

                    MeshRenderer[] lampRenderers = controller.GetComponentsInChildren<MeshRenderer>(true);
                    for (int j = 0; j < lampRenderers.Length; j++)
                        DisableProbeInfluence(lampRenderers[j]);
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

                float top = tileTransform.InverseTransformPoint(renderer.bounds.max).y;
                if (!found || top > highestLocalY)
                {
                    highestLocalY = top;
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

                float top = tileTransform.InverseTransformPoint(light.transform.position).y;
                if (!found || top > highestLocalY)
                {
                    highestLocalY = top;
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

        private static Mesh GetOrCaptureOriginalTileMesh(Tile tile, MeshFilter meshFilter)
        {
            if (tile == null || meshFilter == null)
                return null;

            KnoxumTileMeshBaseline baseline = tile.GetComponent<KnoxumTileMeshBaseline>();
            if (baseline == null)
                baseline = tile.gameObject.AddComponent<KnoxumTileMeshBaseline>();

            Mesh currentMesh = meshFilter.sharedMesh;
            if (currentMesh != null && currentMesh.name != ModifiedTileMeshName)
                baseline.originalMesh = currentMesh;

            return baseline.originalMesh != null ? baseline.originalMesh : currentMesh;
        }

        private static SurfaceMaterialState ResolveSurfaceMaterialState(global::Cell cell, int roomId, MeshRenderer sourceRenderer)
        {
            SurfaceMaterialState state = new SurfaceMaterialState();

            Material template = ResolveTemplateMaterial(cell, sourceRenderer);
            Texture atlas = ResolveAtlasTexture(cell, roomId, sourceRenderer);

            state.templateMaterial = template;
            state.atlasTexture = atlas;
            state.shadowCastingMode = sourceRenderer != null ? sourceRenderer.shadowCastingMode : ShadowCastingMode.On;
            state.receiveShadows = sourceRenderer != null && sourceRenderer.receiveShadows;
            state.lightProbeUsage = sourceRenderer != null ? sourceRenderer.lightProbeUsage : LightProbeUsage.Off;
            state.reflectionProbeUsage = sourceRenderer != null ? sourceRenderer.reflectionProbeUsage : ReflectionProbeUsage.Off;
            state.valid = template != null;

            return state;
        }

        private static Material ResolveTemplateMaterial(global::Cell cell, MeshRenderer sourceRenderer)
        {
            if (cell != null && cell.room != null && cell.room.baseMat != null)
                return cell.room.baseMat;

            if (sourceRenderer != null)
            {
                if (sourceRenderer.sharedMaterial != null && sourceRenderer.sharedMaterial.shader != null)
                {
                    if (sourceRenderer.sharedMaterial.shader.name != PosterShaderName)
                        return sourceRenderer.sharedMaterial;
                }

                try
                {
                    if (sourceRenderer.material != null && sourceRenderer.material.shader != null)
                    {
                        if (sourceRenderer.material.shader.name != PosterShaderName)
                            return sourceRenderer.material;
                    }
                }
                catch
                {
                }

                if (sourceRenderer.sharedMaterial != null)
                    return sourceRenderer.sharedMaterial;
            }

            return null;
        }

        private static Texture ResolveAtlasTexture(global::Cell cell, int roomId, MeshRenderer sourceRenderer)
        {
            EditorController editor = Singleton<EditorController>.Instance;
            if (editor != null && editor.levelData != null)
            {
                EditorRoom editorRoom = GetEditorRoomById(editor, roomId);
                Texture editorAtlas = ResolveEditorAtlasTexture(editor, editorRoom);
                if (editorAtlas != null)
                    return editorAtlas;
            }

            if (cell != null && cell.room != null)
            {
                if (cell.room.textureAtlas != null)
                    return cell.room.textureAtlas;

                if (cell.room.baseMat != null)
                {
                    Texture roomMatTex = GetMaterialMainTexture(cell.room.baseMat);
                    if (roomMatTex != null)
                        return roomMatTex;
                }
            }

            if (sourceRenderer != null)
            {
                Texture rendererTex = GetRendererMainTexture(sourceRenderer);
                if (rendererTex != null)
                    return rendererTex;
            }

            return null;
        }

        private static EditorRoom GetEditorRoomById(EditorController editor, int roomId)
        {
            if (editor == null || editor.levelData == null || roomId <= 0)
                return null;

            if (editor.levelData.rooms != null)
            {
                int idx = roomId - 1;
                if (idx >= 0 && idx < editor.levelData.rooms.Count)
                    return editor.levelData.rooms[idx];
            }

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

            EditorAtlasCacheEntry cacheEntry;
            if (editorAtlasCache.TryGetValue(roomKey, out cacheEntry))
            {
                if (cacheEntry.floorTexId == floorId && cacheEntry.wallTexId == wallId && cacheEntry.ceilTexId == ceilId && cacheEntry.atlas != null)
                    return cacheEntry.atlas;
            }

            Texture atlas = InvokeEditorGenerateTextureAtlas(editor, room.floorTex, room.wallTex, room.ceilTex);
            if (atlas == null)
                return null;

            cacheEntry.floorTexId = floorId;
            cacheEntry.wallTexId = wallId;
            cacheEntry.ceilTexId = ceilId;
            cacheEntry.atlas = atlas;
            editorAtlasCache[roomKey] = cacheEntry;
            return atlas;
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
                    typeof(Texture2D),
                    typeof(Texture2D),
                    typeof(Texture2D)
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
                    Texture fromMaterial = GetMaterialMainTexture(renderer.material);
                    if (fromMaterial != null)
                        return fromMaterial;
                }
            }
            catch
            {
            }

            if (renderer.sharedMaterial != null)
                return GetMaterialMainTexture(renderer.sharedMaterial);

            return null;
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

        private static void RemoveTopTileFloor(MeshFilter meshFilter)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return;

            Mesh sourceMesh = meshFilter.sharedMesh;
            Vector3[] vertices = sourceMesh.vertices;
            Vector3[] normals = sourceMesh.normals;
            int[] triangles = sourceMesh.triangles;

            Mesh meshCopy = UnityEngine.Object.Instantiate(sourceMesh);
            meshCopy.name = ModifiedTileMeshName;

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
            meshFilter.sharedMesh = meshCopy;
        }

        private static void RebuildBottomFloor(Transform tileTransform, SurfaceMaterialState materialState, int targetHeight)
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
            ApplySurfaceMaterial(meshRenderer, materialState, ShadowCastingMode.On);

            if (ShouldGeneratePhysicalColliders())
                EnsureFloorCollider(floorObject);
            else
                RemoveComponentIfExists<BoxCollider>(floorObject);
        }

        private static void RebuildStackedWalls(global::Cell cell, Transform tileTransform, SurfaceMaterialState materialState, int targetHeight, int currentRoomId)
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
                    ApplySurfaceMaterial(meshRenderer, materialState, ShadowCastingMode.Off);

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
                    targetMaterial.name = "Knoxum_Generated_" + state.templateMaterial.name;
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

        private static Vector3 GetWallLocalCenter(int dir, int layer)
        {
            float localY = -((layer + 1) * LayerHeight) + HalfTile;

            switch (dir)
            {
                case 0: return new Vector3(0f, localY, HalfTile - SurfaceInset);
                case 1: return new Vector3(HalfTile - SurfaceInset, localY, 0f);
                case 2: return new Vector3(0f, localY, -HalfTile + SurfaceInset);
                case 3: return new Vector3(-HalfTile + SurfaceInset, localY, 0f);
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
                case 0: inwardNormal = Vector3.back; break;
                case 1: inwardNormal = Vector3.left; break;
                case 2: inwardNormal = Vector3.forward; break;
                case 3: inwardNormal = Vector3.right; break;
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

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
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

            EnvironmentController workerEc = GetWorkerEnvironment(editor);
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

        public static void RefreshAllEditorGeneratedVisuals(EditorController editor)
        {
            if (editor == null || editor.levelData == null)
                return;

            EnvironmentController workerEc = GetWorkerEnvironment(editor);
            if (workerEc == null || workerEc.cells == null)
                return;

            foreach (var kv in HighWallsController.roomHeights)
            {
                int roomId = kv.Key;
                if (roomId <= 0)
                    continue;
                if (HighWallsController.GetRoomHeight(roomId) <= 1 && HighWallsController.GetRoomYOffset(roomId) <= 0)
                    continue;

                EditorRoom room = GetEditorRoomById(editor, roomId);
                if (room != null)
                    RefreshEditorRoomGeneratedVisuals(room);
            }
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

    [HarmonyPatch(typeof(EditorController), "RefreshCells")]
    internal static class KnoxumHighWalls_EditorRefreshCellsPatch
    {
        private static void Postfix(EditorController __instance)
        {
            if (__instance == null)
                return;

            HighWallsGenerator.RefreshAllEditorGeneratedVisuals(__instance);
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
