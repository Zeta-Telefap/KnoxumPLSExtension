using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The sweep: the segment a prop wants to travel this step against the cells, doors, windows, lockers and
    /// characters, and what a hit does (bounce, squish, push, hammer).
    /// </summary>
    public sealed partial class PropLifter
    {


        private bool FindFlightHit(CarryState state, Vector3 c, Vector3 half, Vector3 d, out FlightHit best)
        {
            best = new FlightHit();
            for (int attempt = 0; attempt < FlightMaxDoorRetries; attempt++)
            {
                FlightHit candidate;
                FlightHit other;
                bool found = FlightGridSweep(state.Environment, c, half, d, true, out candidate);
                if (FlightPlaneSweep(state, c, half, d, out other) && (!found || other.T < candidate.T))
                {
                    candidate = other;
                    found = true;
                }

                if (FlightColliderSweep(state, c, half, d, out other) && (!found || other.T < candidate.T))
                {
                    candidate = other;
                    found = true;
                }

                // A locker is a bare mesh: no collider stops a prop thrown at it, so its box is swept here.
                if (FlightLockerSweep(state, c, half, d, out other) && (!found || other.T < candidate.T))
                {
                    candidate = other;
                    found = true;
                }

                if (!found)
                {
                    return false;
                }

                best = candidate;
                if (candidate.Kind == FlightHitKind.Door && FlightDoorLetsPass(state, candidate.Door, c))
                {
                    // The door (or window) gave way; look again for whatever is behind it.
                    continue;
                }

                return true;
            }

            // Never treat a chain of doors as free space: the last hit stays a contact.
            return true;
        }

        private static Vector3 FlightReflect(Vector3 velocity, Vector3 normal)
        {
            float into = -Vector3.Dot(velocity, normal);
            if (into <= 0f)
            {
                return velocity;
            }

            Vector3 tangent = velocity + normal * into;
            float away = into * FlightBounceRestitution;
            if (away < FlightBounceMinNormalSpeed)
            {
                away = 0f;
            }

            return tangent * FlightBounceFriction + normal * away;
        }

        private static void RegisterFlightImpact(CarryState state, FlightHit hit, Vector3 velocity, Vector3 point)
        {
            state.WallHits++;
            PlayImpactSound(state, hit, velocity, point);
            if (state.WallHits != 1)
            {
                return;
            }

            string what;
            switch (hit.Kind)
            {
                case FlightHitKind.Wall:
                    what = "a wall (cell " + hit.EdgeX + "," + hit.EdgeZ
                        + (hit.EdgeAxis == 0 ? ", east edge)" : ", north edge)");
                    break;
                case FlightHitKind.Door:
                    what = hit.Door != null && hit.Door.Door != null
                        ? "the closed " + DescribePath(hit.Door.Door.transform)
                        : "a closed door";
                    break;
                case FlightHitKind.Ceiling:
                    what = "the ceiling";
                    break;
                case FlightHitKind.Collider:
                    what = hit.Collider != null
                        ? DescribePath(hit.Collider.transform)
                        : (hit.Piece != null ? DescribePath(hit.Piece) : "a collider");
                    break;
                default:
                    what = hit.Kind.ToString();
                    break;
            }

            float into = Mathf.Max(0f, -Vector3.Dot(velocity, hit.Normal));
            Debug.Log("[Lifter Frenzy] " + (state.Thrown ? "Thrown" : "Dropped") + " prop hit " + what
                + " at " + into.ToString("0.0") + " u/s; bounced.");
        }

        /// <summary>
        /// A prop that hits a wall, a closed door, the ceiling or a loose collider plays Bang. Grazing contacts
        /// and a quick series of bounces stay silent.
        /// </summary>
        private static void PlayImpactSound(CarryState state, FlightHit hit, Vector3 velocity, Vector3 point)
        {
            float into = Mathf.Max(0f, -Vector3.Dot(velocity, hit.Normal));
            if (into < ImpactSoundMinSpeed || Time.time < state.NextImpactSoundTime)
            {
                return;
            }

            state.NextImpactSoundTime = Time.time + ImpactSoundInterval;
            PlaySoundAt(point, BangSoundName, 1f);
        }

        // axis 0 = the east edge of cell (ax, az) (plane x = 10 (ax + 1)); axis 1 = its north edge (plane z = 10 (az + 1)).
        private static long FlightEdgeKey(int ax, int az, int axis)
        {
            return (((long)(ax + 1000)) * 100000L + (long)(az + 1000)) * 2L + axis;
        }

        private static bool TryFlightEdgeKey(IntVector2 position, Direction direction, out long key)
        {
            key = 0L;
            switch (direction)
            {
                case Direction.North:
                    key = FlightEdgeKey(position.x, position.z, 1);
                    return true;
                case Direction.East:
                    key = FlightEdgeKey(position.x, position.z, 0);
                    return true;
                case Direction.South:
                    key = FlightEdgeKey(position.x, position.z - 1, 1);
                    return true;
                case Direction.West:
                    key = FlightEdgeKey(position.x - 1, position.z, 0);
                    return true;
                default:
                    return false;
            }
        }

        // A cell that is outside the map, Null, an empty filler (ConstBin 16) or in an event spawner room (props never go
        // there, not even through its open door) is void: all its edges are solid.
        private static Cell FlightCell(EnvironmentController environment, int x, int z)
        {
            IntVector2 position = new IntVector2(x, z);
            if (!environment.ContainsCoordinates(position))
            {
                return null;
            }

            Cell cell = environment.CellFromPosition(position);
            return cell == null || cell.Null || cell.room == null || cell.ConstBin == 16
                || cell.room.category == RoomCategory.EventSpawner ? null : cell;
        }

        private static bool FlightEdgeSolid(EnvironmentController environment, int ax, int az, int axis)
        {
            Cell a = FlightCell(environment, ax, az);
            Cell b = axis == 0 ? FlightCell(environment, ax + 1, az) : FlightCell(environment, ax, az + 1);
            if (a == null || b == null)
            {
                return true;
            }

            Direction direction = axis == 0 ? Direction.East : Direction.North;
            return a.HasWallInDirection(direction) || b.HasWallInDirection(direction.GetOpposite());
        }

        private FlightEdgeKind FlightEdgeCandidate(
            EnvironmentController environment, int ax, int az, int axis, bool includeDoors, out FlightDoor door)
        {
            door = null;
            if (FlightEdgeSolid(environment, ax, az, axis))
            {
                return FlightEdgeKind.Wall;
            }

            List<FlightDoor> list;
            if (includeDoors && _flightDoors.TryGetValue(FlightEdgeKey(ax, az, axis), out list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (FlightDoorIsSolid(list[i]))
                    {
                        door = list[i];
                        return FlightEdgeKind.Door;
                    }
                }
            }

            return FlightEdgeKind.None;
        }

        /// <summary>Strict test: does the box overlap any wall or void edge (doors are not counted)?</summary>
        private static bool FlightOverlaps(EnvironmentController environment, Vector3 c, Vector3 half)
        {
            float margin = FlightSkin + FlightWallHalfThickness;
            int k0 = Mathf.CeilToInt((c.x - half.x - margin) / WorldTileWidth);
            int k1 = Mathf.FloorToInt((c.x + half.x + margin) / WorldTileWidth);
            int j0 = Mathf.FloorToInt((c.z - half.z - margin) / WorldTileWidth);
            int j1 = Mathf.FloorToInt((c.z + half.z + margin) / WorldTileWidth);
            for (int k = k0; k <= k1; k++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    float plane = k * WorldTileWidth;
                    if (plane - FlightWallHalfThickness - half.x < c.x && c.x < plane + FlightWallHalfThickness + half.x
                        && j * WorldTileWidth - half.z < c.z && c.z < (j + 1) * WorldTileWidth + half.z
                        && FlightEdgeSolid(environment, k - 1, j, 0))
                    {
                        return true;
                    }
                }
            }

            int m0 = Mathf.CeilToInt((c.z - half.z - margin) / WorldTileWidth);
            int m1 = Mathf.FloorToInt((c.z + half.z + margin) / WorldTileWidth);
            int i0 = Mathf.FloorToInt((c.x - half.x - margin) / WorldTileWidth);
            int i1 = Mathf.FloorToInt((c.x + half.x + margin) / WorldTileWidth);
            for (int m = m0; m <= m1; m++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    float plane = m * WorldTileWidth;
                    if (plane - FlightWallHalfThickness - half.z < c.z && c.z < plane + FlightWallHalfThickness + half.z
                        && i * WorldTileWidth - half.x < c.x && c.x < (i + 1) * WorldTileWidth + half.x
                        && FlightEdgeSolid(environment, i, m - 1, 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Where a landed prop may rest: its full-size box overlaps no wall, void edge, door or window edge.
        /// A flight can end with a smaller box than the prop's (it started squeezed against a wall), the
        /// placement can turn the prop, and a doorway that is open now closes again with the prop in it, so
        /// the check uses the largest footprint the prop can have at that spot. Doors count open or shut.
        /// </summary>
        private bool FlightBoxClear(CarryState state, Vector3 rootPosition)
        {
            Vector3 half = state.FlightFullHalf;
            if (state.Environment == null || half.x <= 0f || half.z <= 0f)
            {
                return true;
            }

            Vector3 offset = state.FlightCenterOffset;
            if (state.Root != null && Quaternion.Angle(state.Root.rotation, state.TargetRotation) > 1f)
            {
                // Turned on landing: a square that holds the footprint at any angle, centred on the pivot.
                float reach = Mathf.Sqrt(half.x * half.x + half.z * half.z)
                    + new Vector2(offset.x, offset.z).magnitude;
                half.x = reach;
                half.z = reach;
                offset = Vector3.zero;
            }

            Vector3 centre = rootPosition + offset;
            return !FlightOverlaps(state.Environment, centre, half) && !FlightDoorSlabOverlaps(centre, half);
        }

        /// <summary>
        /// Earliest contact of the box (centre c, half extents, displacement d) with a wall, a void edge or
        /// a closed door/window. Every solid edge is a thin rectangle, Minkowski-expanded by the half
        /// extents, so the box is a point swept against it: an exact slab test, no tunnelling at any speed.
        /// </summary>
        private bool FlightGridSweep(
            EnvironmentController environment, Vector3 c, Vector3 half, Vector3 d, bool includeDoors, out FlightHit best)
        {
            best = new FlightHit();
            bool found = false;
            float margin = FlightSkin + FlightWallHalfThickness;
            float minX = Mathf.Min(c.x, c.x + d.x) - half.x - margin;
            float maxX = Mathf.Max(c.x, c.x + d.x) + half.x + margin;
            float minZ = Mathf.Min(c.z, c.z + d.z) - half.z - margin;
            float maxZ = Mathf.Max(c.z, c.z + d.z) + half.z + margin;

            // x-normal planes at x = 10 k: the east edge of cells (k - 1, j)
            int k0 = Mathf.CeilToInt(minX / WorldTileWidth);
            int k1 = Mathf.FloorToInt(maxX / WorldTileWidth);
            int j0 = Mathf.FloorToInt(minZ / WorldTileWidth);
            int j1 = Mathf.FloorToInt(maxZ / WorldTileWidth);
            for (int k = k0; k <= k1; k++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    FlightDoor door;
                    FlightEdgeKind kind = FlightEdgeCandidate(environment, k - 1, j, 0, includeDoors, out door);
                    if (kind == FlightEdgeKind.None)
                    {
                        continue;
                    }

                    float t;
                    float na;
                    float nl;
                    bool inside;
                    if (!FlightSlabHit(
                            c.x, c.z, d.x, d.z, k * WorldTileWidth, j * WorldTileWidth, (j + 1) * WorldTileWidth,
                            half.x, half.z, out t, out na, out nl, out inside))
                    {
                        continue;
                    }

                    if (!found || t < best.T)
                    {
                        found = true;
                        best.T = t;
                        best.Normal = new Vector3(na, 0f, nl);
                        best.Kind = kind == FlightEdgeKind.Door ? FlightHitKind.Door : FlightHitKind.Wall;
                        best.Inside = inside;
                        best.Door = door;
                        best.Collider = null;
                        best.EdgeX = k - 1;
                        best.EdgeZ = j;
                        best.EdgeAxis = 0;
                    }
                }
            }

            // z-normal planes at z = 10 m: the north edge of cells (i, m - 1)
            int m0 = Mathf.CeilToInt(minZ / WorldTileWidth);
            int m1 = Mathf.FloorToInt(maxZ / WorldTileWidth);
            int i0 = Mathf.FloorToInt(minX / WorldTileWidth);
            int i1 = Mathf.FloorToInt(maxX / WorldTileWidth);
            for (int m = m0; m <= m1; m++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    FlightDoor door;
                    FlightEdgeKind kind = FlightEdgeCandidate(environment, i, m - 1, 1, includeDoors, out door);
                    if (kind == FlightEdgeKind.None)
                    {
                        continue;
                    }

                    float t;
                    float na;
                    float nl;
                    bool inside;
                    if (!FlightSlabHit(
                            c.z, c.x, d.z, d.x, m * WorldTileWidth, i * WorldTileWidth, (i + 1) * WorldTileWidth,
                            half.z, half.x, out t, out na, out nl, out inside))
                    {
                        continue;
                    }

                    if (!found || t < best.T)
                    {
                        found = true;
                        best.T = t;
                        best.Normal = new Vector3(nl, 0f, na);
                        best.Kind = kind == FlightEdgeKind.Door ? FlightHitKind.Door : FlightHitKind.Wall;
                        best.Inside = inside;
                        best.Door = door;
                        best.Collider = null;
                        best.EdgeX = i;
                        best.EdgeZ = m - 1;
                        best.EdgeAxis = 1;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Sweeps the box centre (ca along the plane normal, cl along the wall) by (da, dl) against one wall
        /// rectangle [plane +- thickness] x [latLo, latHi], expanded by the half extents (ha, hl). When the
        /// centre is already inside (the box overlaps the wall) the position is never moved: the wall only
        /// blocks motion that goes deeper through the nearest exit face, so the centre cannot cross it.
        /// </summary>
        private static bool FlightSlabHit(
            float ca, float cl, float da, float dl, float plane, float latLo, float latHi, float ha, float hl,
            out float t, out float na, out float nl, out bool inside)
        {
            t = 0f;
            na = 0f;
            nl = 0f;
            inside = false;
            float aLo = plane - FlightWallHalfThickness - ha;
            float aHi = plane + FlightWallHalfThickness + ha;
            float lLo = latLo - hl;
            float lHi = latHi + hl;
            if (aLo < ca && ca < aHi && lLo < cl && cl < lHi)
            {
                float paLo = ca - aLo;
                float paHi = aHi - ca;
                float plLo = cl - lLo;
                float plHi = lHi - cl;
                float least = Mathf.Min(Mathf.Min(paLo, paHi), Mathf.Min(plLo, plHi));
                if (least == paLo)
                {
                    na = -1f;
                }
                else if (least == paHi)
                {
                    na = 1f;
                }
                else if (least == plLo)
                {
                    nl = -1f;
                }
                else
                {
                    nl = 1f;
                }

                if (da * na + dl * nl >= 0f)
                {
                    na = 0f;
                    nl = 0f;
                    return false;
                }

                inside = true;
                return true;
            }

            float tMin = -1e30f;
            float tMax = 1e30f;
            if (Mathf.Abs(da) < FlightParallelEpsilon)
            {
                if (ca <= aLo || ca >= aHi)
                {
                    return false;
                }
            }
            else
            {
                float t1 = (aLo - ca) / da;
                float t2 = (aHi - ca) / da;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                if (t1 > tMin)
                {
                    tMin = t1;
                    na = da > 0f ? -1f : 1f;
                    nl = 0f;
                }

                tMax = Mathf.Min(tMax, t2);
            }

            if (Mathf.Abs(dl) < FlightParallelEpsilon)
            {
                if (cl <= lLo || cl >= lHi)
                {
                    return false;
                }
            }
            else
            {
                float t1 = (lLo - cl) / dl;
                float t2 = (lHi - cl) / dl;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                if (t1 > tMin)
                {
                    tMin = t1;
                    na = 0f;
                    nl = dl > 0f ? -1f : 1f;
                }

                tMax = Mathf.Min(tMax, t2);
            }

            if (tMin > tMax || tMax <= 0f || tMin > 1f)
            {
                return false;
            }

            if (tMin < 0f)
            {
                tMin = 0f;
                if (na == 0f && nl == 0f)
                {
                    return false;
                }
            }

            t = tMin;
            return true;
        }

        /// <summary>
        /// The lockers of the floor (BeltManager-like furniture such as a locker row is only a mesh in the cell's
        /// base: it has no collider, so a plain box cast flies straight through it). The prop's box is grown by the
        /// locker's box (a point sweep against the expanded box, as HitNpcs does with characters) and the place it
        /// enters is a contact like a wall's. Only the lockers near the step are looked at, and only lockers: every
        /// other prop of the registry has colliders of its own.
        /// </summary>
        private bool FlightLockerSweep(CarryState state, Vector3 c, Vector3 half, Vector3 d, out FlightHit best)
        {
            best = new FlightHit();
            if (_proxies.Count == 0 || d.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            float reach = d.magnitude + half.magnitude + FlightSkin;
            float bestT = float.MaxValue;
            bool found = false;
            for (int i = 0; i < _proxies.Count; i++)
            {
                ProxyProp proxy = _proxies[i];
                Bounds box;
                if (!proxy.Locker || proxy.Root == null || proxy.Root == state.Root
                    || (proxy.Position - c).sqrMagnitude > reach * reach
                    || !ProxyBox(proxy, out box))
                {
                    continue;
                }

                box.Expand(half * 2f);
                // A prop that already touches the locker (it lies against it) is not stopped by it again.
                if (box.Contains(c))
                {
                    continue;
                }

                float entry;
                Vector3 normal;
                if (!SegmentEntersBox(c, d, box, out entry, out normal) || entry >= bestT)
                {
                    continue;
                }

                bestT = entry;
                best.T = entry;
                best.Normal = normal;
                best.Kind = FlightHitKind.Collider;
                best.Piece = proxy.Root;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Does the segment a -> a + delta start outside the box and enter it? entry is the fraction where it enters
        /// and normal the face it enters through.
        /// </summary>
        private static bool SegmentEntersBox(Vector3 a, Vector3 delta, Bounds box, out float entry, out Vector3 normal)
        {
            entry = 0f;
            normal = Vector3.up;
            float tMin = 0f;
            float tMax = 1f;
            int axis = -1;
            Vector3 low = box.min;
            Vector3 high = box.max;
            for (int i = 0; i < 3; i++)
            {
                float origin = i == 0 ? a.x : (i == 1 ? a.y : a.z);
                float step = i == 0 ? delta.x : (i == 1 ? delta.y : delta.z);
                float lo = i == 0 ? low.x : (i == 1 ? low.y : low.z);
                float hi = i == 0 ? high.x : (i == 1 ? high.y : high.z);
                if (Mathf.Abs(step) < 1e-6f)
                {
                    if (origin < lo || origin > hi)
                    {
                        return false;
                    }

                    continue;
                }

                float t1 = (lo - origin) / step;
                float t2 = (hi - origin) / step;
                float nearest = Mathf.Min(t1, t2);
                // 0..2: entering through the low face of the axis, 3..5: through the high face.
                if (nearest > tMin)
                {
                    tMin = nearest;
                    axis = t1 < t2 ? i : i + 3;
                }

                tMax = Mathf.Min(tMax, Mathf.Max(t1, t2));
                if (tMin > tMax)
                {
                    return false;
                }
            }

            if (axis < 0 || tMax <= 0f || tMin >= 1f)
            {
                return false;
            }

            entry = Mathf.Max(0f, tMin);
            int positiveAxis = axis % 3;
            float sign = axis < 3 ? -1f : 1f;
            normal = positiveAxis == 0 ? new Vector3(sign, 0f, 0f)
                : positiveAxis == 1 ? new Vector3(0f, sign, 0f)
                : new Vector3(0f, 0f, sign);
            return true;
        }

        private static bool FlightPlaneSweep(CarryState state, Vector3 c, Vector3 half, Vector3 d, out FlightHit best)
        {
            best = new FlightHit();
            bool found = false;
            if (d.y < 0f)
            {
                float gap = (c.y - half.y) - state.FlightFloorY;
                float t = gap < 0f ? 0f : gap / -d.y;
                if (t <= 1f)
                {
                    found = true;
                    best.T = t;
                    best.Normal = Vector3.up;
                    best.Kind = FlightHitKind.Floor;
                }
            }

            // A prop taller than the room has no ceiling contact; it would never fit under it anyway.
            if (d.y > 0f && 2f * half.y + 0.2f < FlightCeilingHeight)
            {
                float gap = state.FlightFloorY + FlightCeilingHeight - (c.y + half.y);
                float t = gap < 0f ? 0f : gap / d.y;
                if (t <= 1f && (!found || t < best.T))
                {
                    found = true;
                    best.T = t;
                    best.Normal = Vector3.down;
                    best.Kind = FlightHitKind.Ceiling;
                }
            }

            return found;
        }

        /// <summary>
        /// Loose props and furniture: a plain box cast. Walls (Tile colliders) and doors/windows come from the
        /// analytic sweep above, entities and the player are ignored, and hits that start inside a collider
        /// are skipped so a prop can never get stuck in one.
        /// </summary>
        private bool FlightColliderSweep(CarryState state, Vector3 c, Vector3 half, Vector3 d, out FlightHit best)
        {
            best = new FlightHit();
            float distance = d.magnitude;
            if (distance < 0.0005f)
            {
                return false;
            }

            Vector3 direction = d / distance;
            int count = Physics.BoxCastNonAlloc(
                c,
                half,
                direction,
                SweepHits,
                Quaternion.identity,
                distance + FlightSkin,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                return false;
            }

            try
            {
                if (count > 1)
                {
                    Array.Sort(SweepHits, 0, count, RayHitOrder);
                }

                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = SweepHits[i];
                    Collider collider = hit.collider;
                    if (collider == null || !collider.enabled || hit.distance <= 0.0001f
                        || Vector3.Dot(direction, hit.normal) >= 0f
                        || IsPartOf(collider.transform, state.Root)
                        || (state.Player != null && IsPartOf(collider.transform, state.Player.transform))
                        || collider.GetComponentInParent<Entity>() != null
                        || collider.GetComponentInParent<Door>() != null
                        || collider.GetComponentInParent<Tile>() != null)
                    {
                        continue;
                    }

                    best.T = Mathf.Clamp01(hit.distance / distance);
                    best.Normal = hit.normal.normalized;
                    best.Collider = collider;
                    best.Kind = d.y <= 0f && best.Normal.y >= GroundSupportNormal
                        ? FlightHitKind.Support
                        : FlightHitKind.Collider;
                    return true;
                }

                return false;
            }
            finally
            {
                Array.Clear(SweepHits, 0, count);
            }
        }

        private static bool IsPartOf(Transform candidate, Transform root)
        {
            return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
        }

        // --- thrown props hit characters ---

        /// <summary>
        /// Thrown props only. The segment the prop's centre travelled this step is tested against the bounds
        /// of every character (the prop's half extents added), so no speed can skip one. A character that is
        /// hit is pushed once per flight, the Bang sound plays and the prop bounces back. Walls, doors and the
        /// floor were decided before this runs and are never changed by it.
        /// </summary>
        private void HitNpcs(
            CarryState state, Vector3 start, Vector3 end, Vector3 half, Vector3 impactVelocity, ref Vector3 velocity)
        {
            try
            {
                Vector3 flat = new Vector3(impactVelocity.x, 0f, impactVelocity.z);
                List<NPC> npcs = state.Environment != null ? state.Environment.Npcs : null;
                if (npcs == null || flat.magnitude < NpcMinHitSpeed)
                {
                    return;
                }

                bool hit = false;
                for (int i = 0; i < npcs.Count; i++)
                {
                    NPC npc = npcs[i];
                    if (IsHammerer(npc))
                    {
                        // A boulder and Dr Reflex are not pushed: they hammer the prop at once (the Events files).
                        if (HitHammerer(state, npc, start, end, half, flat))
                        {
                            hit = true;
                        }

                        continue;
                    }

                    Entity entity;
                    Bounds bounds;
                    if (!TryGetPushableNpc(npc, out entity, out bounds)
                        || (state.PushedNpcs != null && state.PushedNpcs.Contains(npc)))
                    {
                        continue;
                    }

                    bounds.Expand(half * 2f);
                    float entry;
                    if (!SegmentHitsBounds(start, end, bounds, out entry))
                    {
                        continue;
                    }

                    // Only a prop that is moving toward the character hits it.
                    Vector3 away = entity.transform.position - Vector3.Lerp(start, end, entry);
                    away.y = 0f;
                    if (Vector3.Dot(flat, away) <= 0f)
                    {
                        continue;
                    }

                    if (state.PushedNpcs == null)
                    {
                        state.PushedNpcs = new List<NPC>();
                    }

                    state.PushedNpcs.Add(npc);
                    float push = PushNpc(npc, entity, away, flat);
                    BreakBullyingRule(state);
                    hit = true;
                    Debug.Log("[Lifter Frenzy] Thrown prop hit " + npc.name + " at "
                        + flat.magnitude.ToString("0.0") + " u/s; pushed it away at "
                        + push.ToString("0.0") + " u/s (Bang); the player broke the Bullying rule.");
                }

                if (hit)
                {
                    velocity.x = -velocity.x * NpcHitBounce;
                    velocity.z = -velocity.z * NpcHitBounce;
                }
            }
            catch (Exception exception)
            {
                if (Time.unscaledTime >= _nextSweepErrorLogTime)
                {
                    _nextSweepErrorLogTime = Time.unscaledTime + 5f;
                    Debug.LogError("[Lifter Frenzy] Pushing a character with a thrown prop failed: " + exception);
                }
            }
        }

        /// <summary>
        /// The push of Bully.Push: Entity.AddForce(new Force(direction, speed, acceleration)). The direction runs
        /// from the point of impact through the character (the Bully uses its own position instead).
        /// </summary>
        private static float PushNpc(NPC npc, Entity entity, Vector3 away, Vector3 flatVelocity)
        {
            if (away.sqrMagnitude < 0.0001f)
            {
                away = flatVelocity;
            }

            float push = Mathf.Clamp(flatVelocity.magnitude * NpcPushPerPropSpeed, NpcPushMinSpeed, NpcPushMaxSpeed)
                * NpcPushStrength;
            entity.AddForce(new Force(away.normalized, push, -push));
            PlayBang(npc);
            return push;
        }

        /// <summary>
        /// A character can be pushed while it is active, visible, not frozen or squished (a frozen entity keeps
        /// its forces unused) and has an enabled collider; its bounds are those of its colliders.
        /// </summary>
        private static bool TryGetPushableNpc(NPC npc, out Entity entity, out Bounds bounds)
        {
            entity = null;
            bounds = new Bounds();
            if (npc == null || !npc.isActiveAndEnabled || npc.Navigator == null)
            {
                return false;
            }

            entity = npc.Navigator.Entity;
            if (entity == null || !entity.isActiveAndEnabled || entity.Frozen || entity.Squished || !entity.Visible)
            {
                return false;
            }

            // The solid colliders decide; a character that only has trigger colliders uses those.
            bool found = false;
            entity.GetComponents(NpcColliders);
            for (int pass = 0; pass < 2 && !found; pass++)
            {
                for (int i = 0; i < NpcColliders.Count; i++)
                {
                    Collider collider = NpcColliders[i];
                    if (collider == null || !collider.enabled || collider.isTrigger != (pass == 1))
                    {
                        continue;
                    }

                    if (found)
                    {
                        bounds.Encapsulate(collider.bounds);
                    }
                    else
                    {
                        bounds = collider.bounds;
                        found = true;
                    }
                }
            }

            NpcColliders.Clear();
            return found;
        }

        /// <summary>Does the segment a -> b touch the box? entry is the segment fraction where it first does.</summary>
        private static bool SegmentHitsBounds(Vector3 a, Vector3 b, Bounds box, out float entry)
        {
            entry = 0f;
            float tMin = 0f;
            float tMax = 1f;
            Vector3 delta = b - a;
            Vector3 low = box.min;
            Vector3 high = box.max;
            if (!SlabRange(a.x, delta.x, low.x, high.x, ref tMin, ref tMax)
                || !SlabRange(a.y, delta.y, low.y, high.y, ref tMin, ref tMax)
                || !SlabRange(a.z, delta.z, low.z, high.z, ref tMin, ref tMax))
            {
                return false;
            }

            entry = tMin;
            return true;
        }

        private static bool SlabRange(
            float origin, float delta, float low, float high, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(delta) < 1e-6f)
            {
                return origin >= low && origin <= high;
            }

            float t1 = (low - origin) / delta;
            float t2 = (high - origin) / delta;
            if (t1 > t2)
            {
                float swap = t1;
                t1 = t2;
                t2 = swap;
            }

            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            return tMin <= tMax;
        }

        private static void PlayBang(NPC npc)
        {
            SoundObject bang = GetSound(BangSoundName);
            if (bang == null)
            {
                return;
            }

            // Played by the character's own audio manager, like 1st Prize plays its audBang.
            AudioManager manager = npc.GetComponentInChildren<AudioManager>();
            if (manager != null)
            {
                manager.PlaySingle(bang);
            }
        }

        /// <summary>The player breaks the "Bullying" rule; the Principal reacts when he sees it.</summary>
        private static void BreakBullyingRule(CarryState state)
        {
            if (state.Player != null)
            {
                state.Player.RuleBreak(BullyingRule, BullyingLinger, BullyingSensitivity);
            }
        }
    }
}
