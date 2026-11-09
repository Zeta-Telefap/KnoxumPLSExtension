using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The aim-assist registry: every prop of the floor with a box around it, built and refreshed a little per frame.
    /// </summary>
    public sealed partial class PropLifter
    {


        /// <summary>
        /// Builds the aim-assist registry from the places where the level builder puts props: the objects of every
        /// room (EnvironmentObject), the base of every cell that has one (lockers, vending machines) and, shallow,
        /// the room itself. It walks those trees a little each frame (ProxyScanBudgetMilliseconds) instead of
        /// asking Unity for every renderer in the scene, which on a floor with many objects took tens of
        /// thousands of lookups, again and again. Once built, the positions are refreshed a few per frame.
        /// </summary>
        private void UpdateProxyRegistry(PlayerManager player)
        {
            if (_proxyEnvironment != player.ec)
            {
                _proxyEnvironment = player.ec;
                _proxies.Clear();
                _proxyByRoot.Clear();
                _proxyBuilding = null;
                _proxyStack.Clear();
                _proxyCursor = 0;
                _proxyBuiltAt = -ProxyRefreshInterval - 1f;
            }

            float now = Time.unscaledTime;
            if (_proxyBuilding == null)
            {
                RefreshProxyPositions();
                if (now - _proxyBuiltAt < ProxyRefreshInterval)
                {
                    return;
                }

                BeginProxyBuild(player.ec);
            }

            float start = Time.realtimeSinceStartup;
            while (_proxyStack.Count > 0 || _proxyContainerIndex < _proxyContainers.Count)
            {
                if (_proxyStack.Count == 0)
                {
                    ProxyContainer container = _proxyContainers[_proxyContainerIndex];
                    _proxyContainerIndex++;
                    PushProxyChildren(container.Root, 1, container.MaxDepth);
                    continue;
                }

                ScanProxyNode(player, _proxyStack.Pop());
                if ((Time.realtimeSinceStartup - start) * 1000f >= ProxyScanBudgetMilliseconds)
                {
                    return;
                }
            }

            _proxies.Clear();
            _proxies.AddRange(_proxyBuilding);
            _proxyByRoot.Clear();
            for (int i = 0; i < _proxies.Count; i++)
            {
                _proxyByRoot[_proxies[i].Root] = _proxies[i];
            }

            _proxyBuilding = null;
            _proxyVisited.Clear();
            _proxyContainers.Clear();
            _proxyBuiltAt = now;
        }

        private void BeginProxyBuild(EnvironmentController environment)
        {
            _proxyBuilding = new List<ProxyProp>();
            _proxyVisited.Clear();
            _proxyStack.Clear();
            _proxyContainers.Clear();
            _proxyContainerIndex = 0;
            List<RoomController> rooms = environment.rooms;
            for (int i = 0; rooms != null && i < rooms.Count; i++)
            {
                RoomController room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                if (room.objectObject != null)
                {
                    AddProxyContainer(room.objectObject.transform, ProxyMaxDepth);
                }

                AddProxyContainer(room.transform, ProxyRoomDepth);
            }

            Cell[,] cells = environment.cells;
            if (cells == null)
            {
                return;
            }

            int width = cells.GetLength(0);
            int length = cells.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < length; z++)
                {
                    // ObjectBase creates itself when asked: only the cells that already have one are looked at.
                    Cell cell = cells[x, z];
                    if (cell != null && cell.HasObjectBase)
                    {
                        AddProxyContainer(cell.ObjectBase, ProxyMaxDepth);
                    }
                }
            }
        }

        private void AddProxyContainer(Transform root, int maxDepth)
        {
            if (root == null)
            {
                return;
            }

            ProxyContainer container;
            container.Root = root;
            container.MaxDepth = maxDepth;
            _proxyContainers.Add(container);
        }

        private void PushProxyChildren(Transform parent, int depth, int maxDepth)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                ProxyNode item;
                item.Node = parent.GetChild(i);
                item.Depth = depth;
                item.MaxDepth = maxDepth;
                _proxyStack.Push(item);
            }
        }

        /// <summary>
        /// One transform of the walk. An EnvironmentObject is a prop root; so is the outermost transform with a
        /// supported name (FindSupportedPropRoot, from the top down); tiles, doors, characters and activities end
        /// the branch, as they end the climb of FindSupportedPropRoot.
        /// </summary>
        private void ScanProxyNode(PlayerManager player, ProxyNode item)
        {
            Transform node = item.Node;
            if (node == null)
            {
                return;
            }

            try
            {
                EnvironmentObject environmentObject = node.GetComponent<EnvironmentObject>();
                if (environmentObject != null)
                {
                    AddProxyRoot(player, node, environmentObject);
                    return;
                }

                if (node.GetComponent<TileBasedObject>() != null || node.GetComponent<RoomController>() != null
                    || node.GetComponent<NPC>() != null || node.GetComponent<Activity>() != null
                    || node.GetComponent<Door>() != null)
                {
                    return;
                }

                if (IsSupportedPropName(node.name))
                {
                    AddProxyRoot(player, node, null);
                    return;
                }

                if (item.Depth < item.MaxDepth)
                {
                    PushProxyChildren(node, item.Depth + 1, item.MaxDepth);
                }
            }
            catch (Exception exception)
            {
                if (!_proxyErrorLogged)
                {
                    _proxyErrorLogged = true;
                    Debug.LogWarning("[Lifter Frenzy] Prop scan skipped an object: " + exception.Message);
                }
            }
        }

        private void AddProxyRoot(PlayerManager player, Transform root, EnvironmentObject environmentObject)
        {
            if (!_proxyVisited.Add(root))
            {
                return;
            }

            Cell cell;
            bool eligible = environmentObject != null
                ? CanPickupEnvironmentObject(environmentObject, player, out cell)
                : !IsForbiddenPropHierarchy(root);
            if (!eligible)
            {
                return;
            }

            ProxyProp proxy = BuildProxy(root, environmentObject, HasSolidCollider(root));
            if (proxy.Renderers.Length == 0)
            {
                return;
            }

            proxy.Position = root.position;
            _proxyBuilding.Add(proxy);
        }

        /// <summary>The positions of a few props per frame, round and round (a moved prop also reports itself, NoteProxyMoved).</summary>
        private void RefreshProxyPositions()
        {
            int count = _proxies.Count;
            int steps = Mathf.Min(count, ProxyPositionsPerFrame);
            for (int i = 0; i < steps; i++)
            {
                if (_proxyCursor >= count)
                {
                    _proxyCursor = 0;
                }

                ProxyProp proxy = _proxies[_proxyCursor];
                _proxyCursor++;
                if (proxy.Root != null)
                {
                    Vector3 position = proxy.Root.position;
                    if ((position - proxy.Position).sqrMagnitude > 0.0001f)
                    {
                        proxy.Position = position;
                        proxy.HasBox = false;
                    }
                }
            }
        }

        /// <summary>A prop of the registry that was put somewhere (landed, placed, sent home) says where it is now.</summary>
        private void NoteProxyMoved(Transform root)
        {
            ProxyProp proxy;
            if (root != null && _proxyByRoot.TryGetValue(root, out proxy) && proxy.Root != null)
            {
                proxy.Position = root.position;
                proxy.HasBox = false;
            }
        }

        /// <summary>
        /// The world box of a registry prop, worked out from its meshes (so a prop whose renderers are culled at the
        /// moment has one too) and kept until the prop moves.
        /// </summary>
        private static bool ProxyBox(ProxyProp proxy, out Bounds bounds)
        {
            if (proxy.HasBox)
            {
                bounds = proxy.Box;
                return true;
            }

            bounds = new Bounds();
            bool found = false;
            Renderer[] renderers = proxy.Renderers;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Bounds one = new Bounds();
                bool has = proxy.HasLocalBounds[i] && TryGetMeshWorldBounds(renderer, out one);
                if (!has)
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    one = renderer.bounds;
                }

                if (found)
                {
                    bounds.Encapsulate(one);
                }
                else
                {
                    bounds = one;
                    found = true;
                }
            }

            if (found)
            {
                proxy.Box = bounds;
                proxy.HasBox = true;
            }

            return found;
        }

        private static bool HasSolidCollider(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static ProxyProp BuildProxy(
            Transform root, EnvironmentObject environmentObject, bool hasSolidCollider)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            ProxyProp proxy = new ProxyProp
            {
                Root = root,
                EnvironmentObject = environmentObject,
                HasSolidCollider = hasSolidCollider,
                Locker = IsLocker(root),
                Renderers = renderers,
                LocalBounds = new Bounds[renderers.Length],
                HasLocalBounds = new bool[renderers.Length]
            };
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds local;
                if (TryGetLocalMeshBounds(renderers[i], out local))
                {
                    proxy.LocalBounds[i] = local;
                    proxy.HasLocalBounds[i] = true;
                }
            }

            return proxy;
        }

        private static bool RayHitsProxy(ProxyProp proxy, Ray ray, out float distance)
        {
            distance = float.MaxValue;
            bool hit = false;
            for (int i = 0; i < proxy.Renderers.Length; i++)
            {
                Renderer renderer = proxy.Renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float rendererDistance;
                bool intersects = proxy.HasLocalBounds[i]
                    ? RayHitsLocalBox(renderer.transform, proxy.LocalBounds[i], ray, out rendererDistance)
                    : renderer.bounds.IntersectRay(ray, out rendererDistance);
                if (intersects && rendererDistance >= 0f && rendererDistance < distance)
                {
                    distance = rendererDistance;
                    hit = true;
                }
            }

            return hit;
        }

        /// <summary>
        /// Ray against a mesh's local-space box. The ray is moved into the transform's local space
        /// (InverseTransformVector keeps the scale, so the parameter t stays a world distance), then a
        /// standard slab test is run. Unlike renderer.bounds this stays tight for rotated props.
        /// </summary>
        private static bool RayHitsLocalBox(Transform transform, Bounds box, Ray ray, out float distance)
        {
            distance = 0f;
            Vector3 origin = transform.InverseTransformPoint(ray.origin);
            Vector3 direction = transform.InverseTransformVector(ray.direction);
            Vector3 min = box.min;
            Vector3 max = box.max;
            float tMin = 0f;
            float tMax = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = origin[axis];
                float d = direction[axis];
                float lo = min[axis];
                float hi = max[axis];
                if (Mathf.Abs(d) < 1e-9f)
                {
                    if (o < lo || o > hi)
                    {
                        return false;
                    }

                    continue;
                }

                float t1 = (lo - o) / d;
                float t2 = (hi - o) / d;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                if (t1 > tMin)
                {
                    tMin = t1;
                }

                if (t2 < tMax)
                {
                    tMax = t2;
                }

                if (tMin > tMax)
                {
                    return false;
                }
            }

            distance = tMin;
            return true;
        }

        /// <summary>A movable prop in the aim-assist registry, matched by the boxes of its meshes.</summary>
        private sealed class ProxyProp
        {
            public Transform Root;
            public EnvironmentObject EnvironmentObject;
            public bool HasSolidCollider;
            // A locker: a bare mesh in the cell's base with no collider of its own, so the flight has to treat its
            // box as a wall (FlightLockerSweep); every other prop of the registry has colliders.
            public bool Locker;
            public Renderer[] Renderers;
            public Bounds[] LocalBounds;
            public bool[] HasLocalBounds;
            // Where the root was last seen: the queries that look at every prop test this, not the transform.
            public Vector3 Position;
            // The world box of the meshes, worked out once and kept until the prop moves (ProxyBox).
            public bool HasBox;
            public Bounds Box;
        }

        private struct ProxyContainer
        {
            public Transform Root;
            public int MaxDepth;
        }

        private struct ProxyNode
        {
            public Transform Node;
            public int Depth;
            public int MaxDepth;
        }
    }
}
