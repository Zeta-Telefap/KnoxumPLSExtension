using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The floor-plane geometry the detours are made of: a prop footprint and the clear-leg / clear-point tests.
    /// </summary>
    public sealed partial class PropLifter
    {


        /// <summary>The footprint of a prop on the floor plane (world x/z).</summary>
        private struct PropRect
        {
            public Transform Root;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        /// <summary>
        /// start -> corner (-> second corner) -> next: every point and every leg is clear. With relaxed, the last
        /// leg only has to clear the walls and the obstacle that is being walked around, not the other props.
        /// </summary>
        private bool DetourClear(
            EnvironmentController environment, Vector3 start, Vector3 next, int first, int second,
            bool relaxed, out float length)
        {
            length = 0f;
            Vector3 a = new Vector3(AvoidCornerX[first], start.y, AvoidCornerZ[first]);
            if (!PointClear(environment, a) || !LegClear(environment, start, a, true))
            {
                return false;
            }

            length = Distance2D(start, a);
            Vector3 last = a;
            if (second >= 0)
            {
                Vector3 b = new Vector3(AvoidCornerX[second], start.y, AvoidCornerZ[second]);
                if (!PointClear(environment, b) || !LegClear(environment, a, b, true))
                {
                    return false;
                }

                length += Distance2D(a, b);
                last = b;
            }

            bool finalLegClear = relaxed
                ? LegClear(environment, last, next, false) && !LegEntersCluster(last, next)
                : LegClear(environment, last, next, true);
            if (!finalLegClear)
            {
                return false;
            }

            length += Distance2D(last, next);
            return true;
        }

        /// <summary>The nearest point on the rectangle's edge that is clear and can be walked to.</summary>
        private bool TryPushOut(
            EnvironmentController environment, Vector3 start, Vector3 inside, PropRect corners, out Vector3 moved)
        {
            moved = inside;
            float bestDistance = float.MaxValue;
            for (int side = 0; side < 4; side++)
            {
                float x = inside.x;
                float z = inside.z;
                if (side == 0)
                {
                    x = corners.MinX;
                }
                else if (side == 1)
                {
                    x = corners.MaxX;
                }
                else if (side == 2)
                {
                    z = corners.MinZ;
                }
                else
                {
                    z = corners.MaxZ;
                }

                Vector3 candidate = new Vector3(x, inside.y, z);
                float distance = Distance2D(inside, candidate);
                if (distance < bestDistance && PointClear(environment, candidate)
                    && LegClear(environment, start, candidate, true))
                {
                    bestDistance = distance;
                    moved = candidate;
                }
            }

            return bestDistance < float.MaxValue;
        }

        /// <summary>A spot a character may stand on: clear of the walls and outside every near prop's zone.</summary>
        private bool PointClear(EnvironmentController environment, Vector3 point)
        {
            if (FlightOverlaps(environment, point, AvoidWallHalf))
            {
                return false;
            }

            for (int i = 0; i < _avoidNear.Count; i++)
            {
                if (InsideRect(_avoidNear[i].Hard, point.x, point.z))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A straight leg that crosses no wall (doors are passable) and, if asked, no near prop's zone.</summary>
        private bool LegClear(EnvironmentController environment, Vector3 origin, Vector3 target, bool checkProps)
        {
            FlightHit wall;
            if (FlightGridSweep(environment, origin, AvoidWallHalf, target - origin, false, out wall))
            {
                return false;
            }

            if (checkProps)
            {
                for (int i = 0; i < _avoidNear.Count; i++)
                {
                    float entry;
                    if (SegmentEntersRect(origin.x, origin.z, target.x, target.z, _avoidNear[i].Hard, out entry))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static float Distance2D(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceToRect(PropRect rect, float x, float z)
        {
            float dx = Mathf.Max(Mathf.Max(rect.MinX - x, 0f), x - rect.MaxX);
            float dz = Mathf.Max(Mathf.Max(rect.MinZ - z, 0f), z - rect.MaxZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static PropRect GrowRect(PropRect rect, float amount)
        {
            rect.MinX -= amount;
            rect.MaxX += amount;
            rect.MinZ -= amount;
            rect.MaxZ += amount;
            return rect;
        }

        private static bool RectsOverlap(PropRect a, PropRect b)
        {
            return a.MinX < b.MaxX && b.MinX < a.MaxX && a.MinZ < b.MaxZ && b.MinZ < a.MaxZ;
        }

        /// <summary>Strictly inside (the edge itself, where a detour runs, is outside).</summary>
        private static bool InsideRect(PropRect rect, float x, float z)
        {
            return x > rect.MinX + AvoidEpsilon && x < rect.MaxX - AvoidEpsilon
                && z > rect.MinZ + AvoidEpsilon && z < rect.MaxZ - AvoidEpsilon;
        }

        /// <summary>Does the segment run through the inside of the rectangle (touching its edge does not count)?</summary>
        private static bool SegmentEntersRect(
            float ax, float az, float bx, float bz, PropRect rect, out float entry)
        {
            entry = 0f;
            float tMin = 0f;
            float tMax = 1f;
            if (!SlabRange(ax, bx - ax, rect.MinX + AvoidEpsilon, rect.MaxX - AvoidEpsilon, ref tMin, ref tMax)
                || !SlabRange(az, bz - az, rect.MinZ + AvoidEpsilon, rect.MaxZ - AvoidEpsilon, ref tMin, ref tMax))
            {
                return false;
            }

            entry = tMin;
            return tMax - tMin > 0.0005f;
        }
    }
}
